import {
  WebHidTransport, ImpactMouse, MouseMemory, MouseError, PROFILE_COUNT, DPI_STAGE_COUNT, SIDE_BUTTON_COUNT,
  FIRST_SIDE_BUTTON, MAIN_BUTTONS, POLL_RATES, DPI_MIN, DPI_MAX, DPI_STEP, CHOICE_GROUPS, ALL_CHOICES,
  SIDE_PRESETS, PROFILE_PRESETS, applyPreset, choiceIndex, describe, keyAction,
} from "./mouse.js";
import { DEMO_MEMORY_HEX } from "./demo.js";

const $ = (id) => document.getElementById(id);
const ORIGINAL_KEY = "impactse-original";
const SWATCHES = ["#ff0000", "#ff6f03", "#ffd400", "#00ff40", "#00e5ff", "#0040ff", "#9b30ff", "#ff00b0", "#ffffff"];

let mouse = null;
let preview = false;
let memory = null;
let profile = 0;
let busy = false;

const buttonSelects = [];
const dpiRows = [];

// ---- Small helpers ----

const toHex = (bytes) => Array.from(bytes, (b) => b.toString(16).padStart(2, "0")).join("");
const fromHex = (text) => Uint8Array.from(text.match(/../g) ?? [], (h) => parseInt(h, 16));
const colorHex = ([r, g, b]) => "#" + toHex([r, g, b]);

function storage(action) {
  // Browser storage can be unavailable (private windows, blocked site data); backups still download.
  try {
    return action(window.localStorage);
  } catch {
    return null;
  }
}

function show(kind, text) {
  const banner = $("banner");
  banner.className = `banner ${kind}`;
  banner.textContent = text;
  banner.hidden = false;
}

function setStatus(text) {
  $("status").textContent = text;
}

function friendlyError(error) {
  if (error instanceof MouseError) return error.message;
  if (error?.name === "NotAllowedError") return "The browser blocked access to the mouse. Click Connect mouse, click your mouse in the list, then click Connect.";
  if (error?.name === "InvalidStateError" || error?.name === "NetworkError")
    return "The mouse is busy. Close Redragon's software and the Impact SE Windows app, close other tabs with this page, unplug and replug the mouse, then click Connect mouse again.";
  return error?.message ?? String(error);
}

// ---- Building the controls once ----

function buttonSelect(label, button) {
  const wrap = document.createElement("label");
  wrap.className = "field";
  const title = document.createElement("span");
  title.textContent = label;
  const select = document.createElement("select");
  const current = document.createElement("option");
  current.value = "current";
  current.hidden = true;
  select.append(current);
  let index = 0;
  for (const group of CHOICE_GROUPS) {
    const og = document.createElement("optgroup");
    og.label = group.name;
    for (const [, name] of group.choices) og.append(new Option(name, String(index++)));
    select.append(og);
  }
  select.dataset.button = button;
  select.addEventListener("change", () => {
    if (select.value === "current") return;
    memory.setButton(profile, button, ALL_CHOICES[Number(select.value)][0]);
    changed();
  });
  wrap.append(title, select);
  buttonSelects.push(select);
  return wrap;
}

function build() {
  for (let p = 0; p < PROFILE_COUNT; p++) $("profile").append(new Option(`Profile ${p + 1}`, String(p)));
  $("profile").addEventListener("change", () => {
    profile = Number($("profile").value);
    populate();
  });

  for (let i = 0; i < SIDE_BUTTON_COUNT; i++) $("side-grid").append(buttonSelect(`Side ${i + 1}`, FIRST_SIDE_BUTTON + i));
  for (const [button, name] of MAIN_BUTTONS) $("main-grid").append(buttonSelect(name, button));

  // No preset is picked up front: presets change only the profile being edited, and only when asked.
  $("preset").append(new Option("Choose a preset…", ""));
  SIDE_PRESETS.forEach(([name], i) => $("preset").append(new Option(name, String(i))));
  $("preset").addEventListener("change", () => ($("apply-preset").disabled = $("preset").value === ""));
  $("apply-preset").addEventListener("click", () => {
    if ($("preset").value === "") return;
    const usages = SIDE_PRESETS[Number($("preset").value)][1];
    usages.forEach((usage, i) => memory.setButton(profile, FIRST_SIDE_BUTTON + i, keyAction(usage)));
    populate();
    changed();
  });

  $("profile-preset").append(new Option("Choose a ready-made profile…", ""));
  PROFILE_PRESETS.forEach(({ name }, i) => $("profile-preset").append(new Option(name, String(i))));
  $("profile-preset").addEventListener("change", () => {
    const preset = PROFILE_PRESETS[Number($("profile-preset").value)];
    $("apply-profile-preset").disabled = $("profile-preset").value === "";
    $("profile-preset-text").textContent = $("profile-preset").value === "" ? "" : preset.description;
  });
  $("apply-profile-preset").addEventListener("click", () => {
    if ($("profile-preset").value === "") return;
    loadPreset(PROFILE_PRESETS[Number($("profile-preset").value)]);
  });
  // Profile 1 resets to factory profile 1, and so on, as the mouse ships.
  $("factory-reset").addEventListener("click", () => loadPreset(PROFILE_PRESETS[profile]));

  for (let i = 0; i < DPI_STAGE_COUNT; i++) {
    const row = document.createElement("div");
    row.className = "dpi-row";
    row.innerHTML = `
      <label class="switch"><input type="checkbox"><span>Stage ${i + 1}</span></label>
      <input type="range" min="${DPI_MIN}" max="${DPI_MAX}" step="${DPI_STEP}" aria-label="Stage ${i + 1} DPI">
      <input type="number" min="${DPI_MIN}" max="${DPI_MAX}" step="${DPI_STEP}" aria-label="Stage ${i + 1} DPI value">`;
    const [toggle, range, number] = row.querySelectorAll("input");
    const update = (source) => {
      let dpi = Math.round(Number(source.value) / DPI_STEP) * DPI_STEP;
      if (!Number.isFinite(dpi)) return;
      dpi = Math.min(DPI_MAX, Math.max(DPI_MIN, dpi));
      range.value = number.value = dpi;
      if (!toggle.checked && memory.getDpiStage(profile, i).enabled && memory.enabledDpiStageCount(profile) === 1) {
        toggle.checked = true;
        show("warn", "At least one DPI stage has to stay on.");
      }
      memory.setDpiStage(profile, i, { enabled: toggle.checked, x: dpi });
      row.classList.toggle("off", !toggle.checked);
      changed();
    };
    toggle.addEventListener("change", () => update(number));
    range.addEventListener("input", () => update(range));
    number.addEventListener("change", () => update(number));
    $("dpi-rows").append(row);
    dpiRows.push({ row, toggle, range, number });
  }

  $("color").addEventListener("input", () => {
    memory.setColor(profile, Array.from(fromHex($("color").value.slice(1))));
    changed();
  });
  for (const swatch of SWATCHES) {
    const b = document.createElement("button");
    b.className = "swatch";
    b.style.background = swatch;
    b.setAttribute("aria-label", swatch);
    b.addEventListener("click", () => {
      $("color").value = swatch;
      $("color").dispatchEvent(new Event("input"));
    });
    $("swatches").append(b);
  }

  for (const [code, hz] of POLL_RATES) {
    const b = document.createElement("button");
    b.textContent = `${hz} Hz`;
    b.setAttribute("role", "radio");
    b.dataset.code = code;
    b.addEventListener("click", () => {
      memory.setPollRate(profile, code);
      populate();
      changed();
    });
    $("poll").append(b);
  }

  $("make-active").addEventListener("click", () => {
    memory.activeProfile = profile;
    populate();
    changed();
  });

  for (const tab of document.querySelectorAll("[data-tab]")) {
    tab.addEventListener("click", () => {
      for (const t of document.querySelectorAll("[data-tab]")) t.setAttribute("aria-selected", String(t === tab));
      for (const panel of document.querySelectorAll("[data-panel]")) panel.hidden = panel.dataset.panel !== tab.dataset.tab;
    });
  }

  $("connect").addEventListener("click", () => connect(false));
  $("preview").addEventListener("click", startPreview);
  $("save").addEventListener("click", save);
  $("discard").addEventListener("click", discard);
  $("restore-original").addEventListener("click", restoreOriginal);
  $("download-backup").addEventListener("click", downloadBackup);
  $("restore-file").addEventListener("change", restoreFromFile);
}

function loadPreset(preset) {
  applyPreset(memory, profile, preset);
  populate();
  show("info", `${preset.name} is loaded into Profile ${profile + 1}. Click Save to mouse to keep it, or Discard changes to undo.`);
}

// ---- Showing the current profile ----

function populate() {
  if (!memory) return;
  $("profile").value = String(profile);

  for (const select of buttonSelects) {
    const action = memory.getButton(profile, Number(select.dataset.button));
    const index = choiceIndex(action);
    const current = select.options[0];
    current.textContent = describe(action);
    current.hidden = index >= 0;
    select.value = index >= 0 ? String(index) : "current";
  }

  dpiRows.forEach(({ row, toggle, range, number }, i) => {
    const stage = memory.getDpiStage(profile, i);
    const dpi = Math.min(DPI_MAX, Math.max(DPI_MIN, stage.x));
    toggle.checked = stage.enabled;
    range.value = number.value = dpi;
    row.classList.toggle("off", !stage.enabled);
  });

  $("color").value = colorHex(memory.getColor(profile));

  const rate = memory.getPollRate(profile);
  for (const b of $("poll").children) b.setAttribute("aria-checked", String(Number(b.dataset.code) === rate));

  const active = memory.activeProfile;
  $("active-text").textContent =
    active === profile ? `The mouse is using this profile (Profile ${active + 1}).` : `The mouse is using Profile ${active + 1}.`;
  $("make-active").disabled = active === profile;

  const original = storage((s) => s.getItem(ORIGINAL_KEY));
  $("backup-text").textContent = original
    ? "This browser kept a copy of your mouse's settings from the first time you connected."
    : "No copy kept in this browser yet. Download a backup file to be safe.";
  $("restore-original").disabled = !original;
  changed();
}

function changed() {
  const dirty = memory?.hasChanges === true;
  $("save").disabled = $("discard").disabled = busy || !dirty;
  if (!busy) setStatus(dirty ? "Unsaved changes. Click Save to mouse to keep them." : preview ? "Preview mode: no mouse connected." : mouse ? "Connected. Make a change, then click Save to mouse." : "Mouse not connected.");
}

// ---- Connecting ----

async function connect(silent) {
  if (!("hid" in navigator)) {
    $("unsupported").hidden = false;
    return;
  }
  try {
    const transport = silent ? await WebHidTransport.remembered() : await WebHidTransport.request();
    if (!transport) {
      if (!silent) show("warn", "No mouse was picked. Click Connect mouse, click your mouse in the list, then click Connect. If it isn't in the list, see Help at the bottom of the page.");
      return;
    }
    setStatus("Reading the mouse's settings…");
    $("savebar").hidden = false;
    const next = new ImpactMouse(transport);
    const loaded = await next.load();
    mouse = next;
    preview = false;
    memory = loaded;
    storage((s) => s.getItem(ORIGINAL_KEY) ?? s.setItem(ORIGINAL_KEY, toHex(loaded.bytes)));
    profile = memory.activeProfile;
    showEditor();
    $("connect").textContent = "Reconnect";
    $("banner").hidden = true;
  } catch (error) {
    show("error", `Couldn't connect. ${friendlyError(error)} Still stuck? See Help at the bottom of the page.`);
  }
}

function startPreview() {
  mouse = null;
  preview = true;
  memory = new MouseMemory(fromHex(DEMO_MEMORY_HEX));
  profile = memory.activeProfile;
  showEditor();
  show("info", "Preview mode: these are sample settings. Nothing is sent anywhere. Click Connect mouse when your Impact SE is plugged in.");
}

function showEditor() {
  $("welcome").hidden = true;
  $("editor").hidden = false;
  $("savebar").hidden = false;
  $("profile-wrap").hidden = false;
  populate();
}

// ---- Saving ----

async function save() {
  if (!memory || busy) return;
  for (let p = 0; p < PROFILE_COUNT; p++) {
    if (memory.hasLeftClick(p)) continue;
    show("warn", `No button in Profile ${p + 1} does a left click. Set one button to Left click, then save again.`);
    return;
  }
  if (preview) {
    memory = new MouseMemory(memory.toArray());
    populate();
    show("info", "Preview mode: that would have saved to the mouse.");
    return;
  }
  if (!mouse) {
    show("warn", "The mouse isn't connected. Click Connect mouse, then save again.");
    return;
  }
  busy = true;
  changed();
  setStatus("Saving to the mouse…");
  try {
    const saved = await mouse.save(memory);
    const notKept = memory.notKept(saved);
    memory = saved;
    busy = false;
    populate();
    if (notKept.length === 0) show("ok", "Saved. Your mouse has the new settings.");
    else show("warn", "The mouse didn't keep all of the changes. The page now shows what the mouse has. Make the change again and click Save to mouse once more.");
  } catch (error) {
    busy = false;
    changed();
    show("error", `Couldn't save. ${friendlyError(error)}`);
  }
}

async function discard() {
  if (!memory) return;
  if (preview) {
    startPreview();
    return;
  }
  if (!mouse) return;
  try {
    memory = await mouse.load();
    populate();
    setStatus("Changes discarded.");
  } catch (error) {
    show("error", `Couldn't read the mouse. ${friendlyError(error)}`);
  }
}

// ---- Backups ----

function restoreOriginal() {
  const original = storage((s) => s.getItem(ORIGINAL_KEY));
  if (!memory || !original) return;
  memory.loadFrom(fromHex(original));
  populate();
  show("info", "Your original settings are loaded. Click Save to mouse to put them back on the mouse.");
}

function downloadBackup() {
  if (!memory) return;
  const stamp = new Date().toISOString().replace(/[-:]/g, "").replace("T", "-").slice(0, 15);
  const link = document.createElement("a");
  link.href = URL.createObjectURL(new Blob([memory.toArray()], { type: "application/octet-stream" }));
  link.download = `impactse-${stamp}.bin`;
  link.click();
  setTimeout(() => URL.revokeObjectURL(link.href), 1000);
}

async function restoreFromFile(event) {
  const file = event.target.files?.[0];
  event.target.value = "";
  if (!file || !memory) return;
  try {
    memory.loadFrom(new Uint8Array(await file.arrayBuffer()));
    populate();
    show("info", `Loaded ${file.name}. Click Save to mouse to put these settings on the mouse.`);
  } catch (error) {
    show("error", friendlyError(error));
  }
}

// ---- Start ----

build();
if (!("hid" in navigator)) {
  $("unsupported").hidden = false;
  $("connect").disabled = true;
} else {
  navigator.hid.addEventListener("disconnect", (event) => {
    if (mouse && event.device === mouse.transport.device) {
      mouse = null;
      show("warn", "The mouse was unplugged. Plug it back in and click Connect mouse. Unsaved changes are still here.");
      changed();
    }
  });
  connect(true);
}
window.addEventListener("beforeunload", (event) => {
  if (mouse && memory?.hasChanges) event.preventDefault();
});
