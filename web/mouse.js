// Impact SE (USB 36B7:FD16) protocol and settings memory, for the browser.
// A port of src/ImpactSE.Core; see docs/protocol.md for the memory map.

export const VENDOR_ID = 0x36b7;
export const PRODUCT_ID = 0xfd16;
export const CONFIG_USAGE_PAGE = 0xffa0;

const SMALL_REPORT = 2;
const LARGE_REPORT = 3;
const SMALL_LENGTH = 16;
const LARGE_LENGTH = 64;
export const HEADER_LENGTH = 8;
export const CHUNK_LENGTH = 0x20;

const CMD_APPLY = 0xf1;
const CMD_READ = 0xf2;
const CMD_WRITE = 0xf3;
const CMD_SESSION = 0xf5;
const REPLY_OK = 0x08;
const APPLY_FLAGS = [0x04, 0x01, 0x02, 0x08, 0x10];

export class MouseError extends Error {}

const hex = (n, width = 2) => n.toString(16).toUpperCase().padStart(width, "0");
const defaultSleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/**
 * Feature-report access through WebHID. Reports are full buffers with the report ID at index 0,
 * the same shape the Windows app uses.
 */
export class WebHidTransport {
  constructor(device) {
    this.device = device;
  }

  /** Asks the browser for the mouse's settings channel. Returns null if the person cancels. */
  static async request() {
    const devices = await navigator.hid.requestDevice({
      filters: [{ vendorId: VENDOR_ID, productId: PRODUCT_ID, usagePage: CONFIG_USAGE_PAGE }],
    });
    return WebHidTransport.pick(devices);
  }

  /** A mouse this page was allowed to use before, so returning visitors skip the picker. */
  static async remembered() {
    return WebHidTransport.pick(await navigator.hid.getDevices());
  }

  static async pick(devices) {
    // Windows lists each collection separately; the settings one has feature reports 2 and 3.
    const device = devices.find(
      (d) =>
        d.vendorId === VENDOR_ID &&
        d.productId === PRODUCT_ID &&
        d.collections.some((c) => c.featureReports?.some((r) => r.reportId === LARGE_REPORT)),
    );
    if (!device) return null;
    if (!device.opened) await device.open();
    return new WebHidTransport(device);
  }

  async setFeature(report) {
    await this.device.sendFeatureReport(report[0], report.subarray(1));
  }

  async getFeature(reportId) {
    const view = await this.device.receiveFeatureReport(reportId);
    const bytes = new Uint8Array(view.buffer, view.byteOffset, view.byteLength);
    if (bytes[0] === reportId) return bytes;
    // Some platforms leave the report ID out of the reply.
    const withId = new Uint8Array(bytes.length + 1);
    withId[0] = reportId;
    withId.set(bytes, 1);
    return withId;
  }

  async close() {
    if (this.device.opened) await this.device.close();
  }
}

/** The raw commands: session, read, write and apply. */
export class MouseProtocol {
  constructor(transport, sleep = defaultSleep) {
    this.transport = transport;
    this.sleep = sleep;
  }

  openSession() {
    return this.#session(true);
  }

  closeSession() {
    return this.#session(false);
  }

  async #session(open) {
    const report = new Uint8Array(SMALL_LENGTH);
    report[0] = SMALL_REPORT;
    report[1] = CMD_SESSION;
    report[2] = open ? 0 : 1;
    await this.transport.setFeature(report);
    await this.sleep(50);
  }

  async read(address, length) {
    const result = new Uint8Array(length);
    for (let done = 0; done < length; done += CHUNK_LENGTH) {
      const n = Math.min(CHUNK_LENGTH, length - done);
      result.set(await this.#readChunk(address + done, n), done);
    }
    return result;
  }

  async #readChunk(address, length) {
    const request = new Uint8Array(LARGE_LENGTH);
    writeHeader(request, LARGE_REPORT, CMD_READ, address, length);
    await this.transport.setFeature(request);
    await this.sleep(10);

    const reply = await this.transport.getFeature(LARGE_REPORT);
    if (
      !reply ||
      reply.length < HEADER_LENGTH + length ||
      reply[1] !== REPLY_OK ||
      reply[2] !== (address & 0xff) ||
      reply[3] !== address >> 8
    ) {
      const got = reply ? [...reply.slice(0, 8)].map((b) => hex(b)).join(" ") : "nothing";
      throw new MouseError(`Unexpected reply reading 0x${hex(address, 4)}: ${got}`);
    }
    return reply.slice(HEADER_LENGTH, HEADER_LENGTH + length);
  }

  async write(address, data) {
    for (let done = 0; done < data.length; done += CHUNK_LENGTH) {
      const n = Math.min(CHUNK_LENGTH, data.length - done);
      const report = new Uint8Array(LARGE_LENGTH);
      writeHeader(report, LARGE_REPORT, CMD_WRITE, address + done, n);
      report.set(data.subarray(done, done + n), HEADER_LENGTH);
      await this.transport.setFeature(report);
      await this.sleep(10);
    }
  }

  /** Tells the mouse to reload its settings, the same way the official app does. */
  async apply() {
    for (const flag of APPLY_FLAGS) {
      const report = new Uint8Array(SMALL_LENGTH);
      report[0] = SMALL_REPORT;
      report[1] = CMD_APPLY;
      report[2] = 0x02;
      report[3] = flag;
      await this.transport.setFeature(report);
      await this.sleep(50);
    }
  }
}

function writeHeader(report, reportId, command, address, length) {
  if (address < 0 || address > 0xffff) throw new RangeError("address");
  if (length <= 0 || length > CHUNK_LENGTH) throw new RangeError("length");
  report[0] = reportId;
  report[1] = command;
  report[2] = address & 0xff;
  report[3] = address >> 8;
  report[4] = length;
}

/** Load the whole settings memory, then save only the bytes that changed. */
export class ImpactMouse {
  constructor(transport, sleep) {
    this.transport = transport;
    this.protocol = new MouseProtocol(transport, sleep);
  }

  load() {
    return this.#inSession(async () => new MouseMemory(await this.protocol.read(0, MEMORY_SIZE)));
  }

  /** Writes the changed bytes, asks the mouse to reload, and returns a fresh copy read back from it. */
  async save(memory) {
    const changes = memory.changes();
    if (changes.length === 0) return memory;
    return this.#inSession(async () => {
      for (const { address, data } of changes) await this.protocol.write(address, data);
      await this.protocol.apply();
      return new MouseMemory(await this.protocol.read(0, MEMORY_SIZE));
    });
  }

  async #inSession(action) {
    await this.protocol.openSession();
    try {
      return await action();
    } finally {
      await this.protocol.closeSession();
    }
  }
}

// ---- Settings memory ----

export const MEMORY_SIZE = 0x500;
export const PROFILE_COUNT = 5;
export const DPI_STAGE_COUNT = 5;
export const BUTTON_COUNT = 24;
export const FIRST_SIDE_BUTTON = 6;
export const SIDE_BUTTON_COUNT = 12;
export const WHEEL_UP_BUTTON = 22;
export const WHEEL_DOWN_BUTTON = 23;

/** The top buttons, numbered as in Redragon's picture, then the scroll wheel. */
export const MAIN_BUTTONS = [
  [0, "Button 1 (left)"],
  [1, "Button 2 (right)"],
  [2, "Button 3 (wheel press)"],
  [3, "Button 4"],
  [4, "Button 5"],
  [5, "Button 6"],
  [WHEEL_UP_BUTTON, "Scroll wheel up"],
  [WHEEL_DOWN_BUTTON, "Scroll wheel down"],
];

const ACTIVE_PROFILE_ADDRESS = 0x2c;
const POLL_RATE_ADDRESS = 0x32; // 2 bytes per profile
const LIGHTING_ADDRESS = 0x448; // 8 bytes per profile: ?? R G B mode ...
const PROFILE_BASE = [0x40, 0x100, 0x1b0, 0x260, 0x310];
const DPI_STAGES_OFFSET = 4; // then 6 bytes per stage: en xLo xHi yLo yHi 00
const DPI_STAGE_LENGTH = 6;
const BUTTONS_OFFSET = 0x42; // 4 bytes per button: type mod key 00

/** Polling-rate codes. 1000 Hz and 500 Hz are confirmed on the mouse; 250 and 125 follow the same pattern. */
export const POLL_RATES = [
  [1, 1000],
  [2, 500],
  [4, 250],
  [8, 125],
];

export const DPI_MIN = 100;
export const DPI_MAX = 10000;
export const DPI_STEP = 100;

// Stored value = DPI / 100 + 2 (confirmed: 0x0A = 800, 0x0B = 900).
export const dpiFromRaw = (raw) => (raw - 2) * DPI_STEP;

export function dpiToRaw(dpi) {
  if (!Number.isInteger(dpi) || dpi < DPI_MIN || dpi > DPI_MAX || dpi % DPI_STEP !== 0)
    throw new RangeError(`DPI must be ${DPI_MIN}–${DPI_MAX} in steps of ${DPI_STEP}.`);
  return dpi / DPI_STEP + 2;
}

/**
 * A copy of the mouse's settings memory with typed accessors for the confirmed parts.
 * Edits change only this copy; changes() lists what differs from the original.
 */
export class MouseMemory {
  #original;
  #data;

  constructor(image) {
    if (image.length !== MEMORY_SIZE) throw new MouseError(`Expected ${MEMORY_SIZE} bytes.`);
    this.#original = Uint8Array.from(image);
    this.#data = Uint8Array.from(image);
  }

  get bytes() {
    return this.#data;
  }

  toArray() {
    return Uint8Array.from(this.#data);
  }

  static profileAddress(profile) {
    return PROFILE_BASE[checkProfile(profile)];
  }

  get activeProfile() {
    return this.#data[ACTIVE_PROFILE_ADDRESS];
  }

  set activeProfile(value) {
    this.#data[ACTIVE_PROFILE_ADDRESS] = checkProfile(value);
  }

  getPollRate(profile) {
    return this.#data[POLL_RATE_ADDRESS + 2 * checkProfile(profile)];
  }

  setPollRate(profile, code) {
    if (!POLL_RATES.some(([c]) => c === code)) throw new RangeError("poll rate");
    this.#data[POLL_RATE_ADDRESS + 2 * checkProfile(profile)] = code;
  }

  getDpiStage(profile, stage) {
    const a = dpiStageAddress(profile, stage);
    const d = this.#data;
    return {
      enabled: d[a] !== 0,
      x: dpiFromRaw(d[a + 1] | (d[a + 2] << 8)),
      y: dpiFromRaw(d[a + 3] | (d[a + 4] << 8)),
    };
  }

  setDpiStage(profile, stage, { enabled, x, y = x }) {
    const a = dpiStageAddress(profile, stage);
    const rx = dpiToRaw(x);
    const ry = dpiToRaw(y);
    const d = this.#data;
    d[a] = enabled ? 1 : 0;
    d[a + 1] = rx & 0xff;
    d[a + 2] = rx >> 8;
    d[a + 3] = ry & 0xff;
    d[a + 4] = ry >> 8;
  }

  getButton(profile, button) {
    const a = buttonAddress(profile, button);
    return Array.from(this.#data.subarray(a, a + 4));
  }

  setButton(profile, button, action) {
    this.#data.set(action, buttonAddress(profile, button));
  }

  getColor(profile) {
    const a = LIGHTING_ADDRESS + 8 * checkProfile(profile) + 1;
    return Array.from(this.#data.subarray(a, a + 3));
  }

  setColor(profile, [r, g, b]) {
    this.#data.set([r, g, b], LIGHTING_ADDRESS + 8 * checkProfile(profile) + 1);
  }

  /** Copies every byte of another image into this copy (used to restore a backup). */
  loadFrom(image) {
    if (image.length !== MEMORY_SIZE) throw new MouseError("That file isn't an Impact SE backup.");
    this.#data.set(image);
  }

  get hasChanges() {
    return this.changes().length > 0;
  }

  /** Contiguous runs of changed bytes, as { address, data }. */
  changes() {
    const runs = [];
    let i = 0;
    while (i < MEMORY_SIZE) {
      if (this.#data[i] === this.#original[i]) {
        i++;
        continue;
      }
      const start = i;
      while (i < MEMORY_SIZE && this.#data[i] !== this.#original[i]) i++;
      runs.push({ address: start, data: this.#data.slice(start, i) });
    }
    return runs;
  }

  /** True if some button in the profile still does a left click, so the mouse stays usable. */
  hasLeftClick(profile) {
    for (let b = 0; b < BUTTON_COUNT; b++) if (this.getButton(profile, b)[0] === LEFT_CLICK_TYPE) return true;
    return false;
  }
}

function dpiStageAddress(profile, stage) {
  if (!(stage >= 0 && stage < DPI_STAGE_COUNT)) throw new RangeError("stage");
  return MouseMemory.profileAddress(profile) + DPI_STAGES_OFFSET + DPI_STAGE_LENGTH * stage;
}

function buttonAddress(profile, button) {
  if (!(button >= 0 && button < BUTTON_COUNT)) throw new RangeError("button");
  return MouseMemory.profileAddress(profile) + BUTTONS_OFFSET + 4 * button;
}

function checkProfile(profile) {
  if (!(Number.isInteger(profile) && profile >= 0 && profile < PROFILE_COUNT)) throw new RangeError("profile");
  return profile;
}

// ---- Button actions: [type, modifier, code, extra] ----

export const KEY_TYPE = 0x90;
export const COMBO_TYPE = 0x8f; // modifier bits: 1 Ctrl, 2 Shift, 4 Alt, 8 Win
export const MEDIA_TYPE = 0x8e; // modifier 01, then a 16-bit consumer usage in code/extra
export const LEFT_CLICK_TYPE = 0x81;

export const keyAction = (usage) => [KEY_TYPE, 0, usage, 0];
export const comboAction = (modifiers, usage) => [COMBO_TYPE, modifiers, usage, 0];
export const functionAction = (type) => [type, 0, 0, 0];
export const mediaAction = (usage) => [MEDIA_TYPE, 1, usage & 0xff, usage >> 8];
export const sameAction = (a, b) => a.length === b.length && a.every((v, i) => v === b[i]);

/** Names for HID keyboard usages. */
export const KEYS = (() => {
  const keys = [];
  for (let i = 0; i < 26; i++) keys.push([0x04 + i, String.fromCharCode(65 + i)]);
  for (let i = 0; i < 9; i++) keys.push([0x1e + i, String(i + 1)]);
  keys.push(
    [0x27, "0"], [0x28, "Enter"], [0x29, "Esc"], [0x2a, "Backspace"], [0x2b, "Tab"], [0x2c, "Space"],
    [0x2d, "-"], [0x2e, "="], [0x2f, "["], [0x30, "]"], [0x33, ";"], [0x34, "'"], [0x35, "`"],
    [0x36, ","], [0x37, "."], [0x38, "/"],
  );
  for (let i = 0; i < 12; i++) keys.push([0x3a + i, `F${i + 1}`]);
  for (let i = 0; i < 12; i++) keys.push([0x68 + i, `F${i + 13}`]);
  keys.push(
    [0x49, "Insert"], [0x4a, "Home"], [0x4b, "Page Up"], [0x4c, "Delete"], [0x4d, "End"], [0x4e, "Page Down"],
    [0x4f, "Arrow right"], [0x50, "Arrow left"], [0x51, "Arrow down"], [0x52, "Arrow up"],
    [0x54, "Num /"], [0x55, "Num *"], [0x56, "Num -"], [0x57, "Num +"], [0x58, "Num Enter"],
  );
  for (let i = 0; i < 9; i++) keys.push([0x59 + i, `Num ${i + 1}`]);
  keys.push([0x62, "Num 0"], [0x63, "Num ."]);
  return keys;
})();

export const keyName = (usage) => KEYS.find(([u]) => u === usage)?.[1] ?? `Key 0x${hex(usage)}`;

/**
 * Everything a button can be set to, in menu groups. The mouse-function and media codes come from
 * the lookup table in Redragon's app (RDCfg.exe, 0x4a8980).
 */
export const CHOICE_GROUPS = [
  {
    name: "Mouse",
    choices: [
      [functionAction(0x81), "Left click"],
      [functionAction(0x82), "Right click"],
      [functionAction(0x83), "Middle click"],
      [functionAction(0x84), "Back"],
      [functionAction(0x85), "Forward"],
      [functionAction(0x8b), "Scroll up"],
      [functionAction(0x8c), "Scroll down"],
      [functionAction(0x86), "Scroll left"],
      [functionAction(0x87), "Scroll right"],
      [functionAction(0x88), "DPI cycle"],
      [functionAction(0x8a), "DPI up"],
      [functionAction(0x89), "DPI down"],
      [functionAction(0x8d), "Profile cycle"],
      [functionAction(0x94), "Profile up"],
      [functionAction(0x95), "Profile down"],
      [functionAction(0x97), "Polling rate up"],
      [functionAction(0x98), "Polling rate down"],
      [[0x9b, 4, 0, 0], "Lights on/off"],
      [functionAction(0x00), "Disabled"],
    ],
  },
  { name: "Keyboard keys", choices: KEYS.map(([usage, name]) => [keyAction(usage), name]) },
  {
    name: "Media",
    choices: [
      [mediaAction(0xcd), "Play / pause"],
      [mediaAction(0xb7), "Stop"],
      [mediaAction(0xb6), "Previous track"],
      [mediaAction(0xb5), "Next track"],
      [mediaAction(0xe9), "Volume up"],
      [mediaAction(0xea), "Volume down"],
      [mediaAction(0xe2), "Mute"],
    ],
  },
  {
    name: "Shortcuts",
    choices: [
      [comboAction(1, 0x06), "Copy (Ctrl+C)"],
      [comboAction(1, 0x19), "Paste (Ctrl+V)"],
      [comboAction(1, 0x1b), "Cut (Ctrl+X)"],
      [comboAction(1, 0x1d), "Undo (Ctrl+Z)"],
      [comboAction(4, 0x2b), "Switch window (Alt+Tab)"],
      [comboAction(8, 0x07), "Show desktop (Win+D)"],
      [comboAction(8, 0x0f), "Lock PC (Win+L)"],
    ],
  },
];

export const ALL_CHOICES = CHOICE_GROUPS.flatMap((g) => g.choices);

/** Position of the action in ALL_CHOICES, or -1 if it isn't one of the listed choices. */
export const choiceIndex = (action) => ALL_CHOICES.findIndex(([a]) => sameAction(a, action));

export function describe(action) {
  const [type, mod, code] = action;
  if (type === KEY_TYPE && mod === 0) return keyName(code);
  if (type === KEY_TYPE || type === COMBO_TYPE) {
    const parts = [];
    if (mod & 1) parts.push("Ctrl");
    if (mod & 2) parts.push("Shift");
    if (mod & 4) parts.push("Alt");
    if (mod & 8) parts.push("Win");
    return [...parts, keyName(code)].join("+");
  }
  const found = [...CHOICE_GROUPS[0].choices, ...CHOICE_GROUPS[2].choices].find(([a]) => sameAction(a, action));
  return found ? found[1] : `Mouse function 0x${hex(type)}`;
}

/** Key presets for the 12 side buttons, in button order. */
export const SIDE_PRESETS = [
  ["Number row 1–0, -, =", [0x1e, 0x1f, 0x20, 0x21, 0x22, 0x23, 0x24, 0x25, 0x26, 0x27, 0x2d, 0x2e]],
  ["Numpad 1–0, +, -", [0x59, 0x5a, 0x5b, 0x5c, 0x5d, 0x5e, 0x5f, 0x60, 0x61, 0x62, 0x57, 0x56]],
  ["F13–F24 (no clashes)", [0x68, 0x69, 0x6a, 0x6b, 0x6c, 0x6d, 0x6e, 0x6f, 0x70, 0x71, 0x72, 0x73]],
];
