// Run with: node --test web/tests
// Mirrors tests/ImpactSE.Core.Tests so the web page and the Windows app agree on the protocol.
import { test } from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import {
  ImpactMouse, MouseProtocol, MouseError, MouseMemory, MEMORY_SIZE, HEADER_LENGTH, CHUNK_LENGTH,
  FIRST_SIDE_BUTTON, MAIN_BUTTONS, SIDE_PRESETS, ALL_CHOICES, keyAction, comboAction, mediaAction,
  describe, choiceIndex, sameAction,
} from "../mouse.js";
import { DEMO_MEMORY_HEX } from "../demo.js";

const fixtures = new URL("../../tests/ImpactSE.Core.Tests/Fixtures/", import.meta.url);
const noSleep = async () => {};

function loadDump(name) {
  const memory = new Uint8Array(MEMORY_SIZE);
  for (const line of readFileSync(fileURLToPath(new URL(name, fixtures)), "utf8").split(/\r?\n/)) {
    if (!line.trim()) continue;
    const address = parseInt(line.slice(0, 4), 16);
    const bytes = line.slice(6).trim().split(/\s+/).map((b) => parseInt(b, 16));
    memory.set(bytes.slice(HEADER_LENGTH, HEADER_LENGTH + CHUNK_LENGTH), address);
  }
  return memory;
}

/** Simulates the mouse as observed on real hardware, including zeros when no session is open. */
class FakeMouse {
  sent = [];
  #session = false;
  #pendingRead = null;

  constructor(memory) {
    this.memory = memory;
  }

  static fromDump(name) {
    return new FakeMouse(loadDump(name));
  }

  async setFeature(report) {
    this.sent.push(Uint8Array.from(report));
    const address = report[2] | (report[3] << 8);
    const length = report[4];
    if (report[1] === 0xf5) this.#session = report[2] === 0;
    else if (report[1] === 0xf2) this.#pendingRead = report.slice(0, HEADER_LENGTH);
    else if (report[1] === 0xf3 && this.#session)
      this.memory.set(report.subarray(HEADER_LENGTH, HEADER_LENGTH + length), address);
  }

  async getFeature(reportId) {
    const reply = new Uint8Array(64);
    reply[0] = reportId;
    if (!this.#session || !this.#pendingRead) return reply;
    const p = this.#pendingRead;
    const address = p[2] | (p[3] << 8);
    reply.set([0x08, p[2], p[3], p[4], 0, 0xfa, 0xfa], 1);
    reply.set(this.memory.subarray(address, address + p[4]), HEADER_LENGTH);
    this.#pendingRead = null;
    return reply;
  }
}

const loadFixture = (name) => new ImpactMouse(FakeMouse.fromDump(name), noSleep).load();

test("load reads the whole memory inside a session", async () => {
  const fake = FakeMouse.fromDump("baseline.txt");
  const memory = await new ImpactMouse(fake, noSleep).load();
  assert.deepEqual(memory.toArray(), fake.memory);
  assert.deepEqual([...fake.sent[0].slice(0, 3)], [0x02, 0xf5, 0x00]);
  assert.deepEqual([...fake.sent.at(-1).slice(0, 3)], [0x02, 0xf5, 0x01]);
});

test("a read without a session fails like the real mouse", async () => {
  const protocol = new MouseProtocol(FakeMouse.fromDump("baseline.txt"), noSleep);
  await assert.rejects(protocol.read(0x2c, 2), MouseError);
});

test("DPI, polling rate and color match the official app", async () => {
  const before = await loadFixture("baseline.txt");
  const after = await loadFixture("dpi800-red-500hz.txt");
  assert.deepEqual(after.getDpiStage(0, 0), { enabled: true, x: 800, y: 800 });
  assert.equal(after.getDpiStage(0, 1).x, 900);
  assert.equal(before.getPollRate(0), 1);
  assert.equal(after.getPollRate(0), 2);
  assert.deepEqual(before.getColor(0), [0xfc, 0x6f, 0x03]);
  assert.deepEqual(after.getColor(0), [0xff, 0x00, 0x00]);
});

test("buttons decode by name", async () => {
  const before = await loadFixture("baseline.txt");
  const after = await loadFixture("dpi800-red-500hz.txt");
  assert.equal(describe(before.getButton(0, FIRST_SIDE_BUTTON)), "1");
  assert.equal(describe(before.getButton(0, FIRST_SIDE_BUTTON + 9)), "0");
  assert.equal(describe(after.getButton(0, FIRST_SIDE_BUTTON)), "Num 1");
  assert.deepEqual(
    MAIN_BUTTONS.map(([b]) => describe(before.getButton(0, b))),
    ["Left click", "Right click", "Middle click", "Back", "DPI up", "DPI down", "Scroll up", "Scroll down"],
  );
});

test("editing the baseline reproduces the official app's changes in those fields", async () => {
  const memory = await loadFixture("baseline.txt");
  memory.setPollRate(0, 2);
  memory.setDpiStage(0, 0, { enabled: true, x: 800 });
  memory.setDpiStage(0, 1, { enabled: true, x: 900 });
  memory.setColor(0, [0xff, 0, 0]);
  const numpad = SIDE_PRESETS[1][1];
  for (let i = 0; i < 10; i++) memory.setButton(0, FIRST_SIDE_BUTTON + i, keyAction(numpad[i]));
  assert.deepEqual(memory.toArray(), loadDump("dpi800-red-500hz.txt"));
});

test("save writes only the changed bytes, then applies", async () => {
  const fake = FakeMouse.fromDump("baseline.txt");
  const mouse = new ImpactMouse(fake, noSleep);
  const memory = await mouse.load();
  fake.sent.length = 0;

  memory.setColor(0, [0xff, 0, 0]);
  const reloaded = await mouse.save(memory);

  const writes = fake.sent.filter((r) => r[1] === 0xf3);
  assert.equal(writes.length, 1);
  assert.deepEqual([...writes[0].slice(0, 11)], [0x03, 0xf3, 0x49, 0x04, 0x03, 0, 0, 0, 0xff, 0, 0]);
  assert.equal(fake.sent.filter((r) => r[1] === 0xf1).length, 5);
  assert.deepEqual(reloaded.getColor(0), [0xff, 0, 0]);
  assert.equal(reloaded.hasChanges, false);
  assert.deepEqual([...fake.sent.at(-1).slice(0, 3)], [0x02, 0xf5, 0x01]);
});

test("save with no changes sends nothing", async () => {
  const fake = FakeMouse.fromDump("baseline.txt");
  const mouse = new ImpactMouse(fake, noSleep);
  const memory = await mouse.load();
  fake.sent.length = 0;
  await mouse.save(memory);
  assert.equal(fake.sent.length, 0);
});

test("DPI outside the supported range is rejected", async () => {
  const memory = await loadFixture("baseline.txt");
  for (const dpi of [50, 150, 10100])
    assert.throws(() => memory.setDpiStage(0, 0, { enabled: true, x: dpi }), RangeError);
});

test("every listed choice has a unique encoding and reads back by name", () => {
  const encodings = new Set(ALL_CHOICES.map(([a]) => a.join(",")));
  assert.equal(encodings.size, ALL_CHOICES.length);
  assert.equal(describe(ALL_CHOICES[choiceIndex(comboAction(1, 0x06))][0]), "Ctrl+C");
  assert.equal(describe(mediaAction(0xe9)), "Volume up");
  assert.ok(sameAction(ALL_CHOICES[choiceIndex(keyAction(0x68))][0], keyAction(0x68)));
});

test("a profile without a left click is detected", async () => {
  const memory = await loadFixture("baseline.txt");
  assert.equal(memory.hasLeftClick(0), true);
  memory.setButton(0, 0, keyAction(0x68));
  assert.equal(memory.hasLeftClick(0), false);
});

test("the preview data is the baseline dump", () => {
  const bytes = Uint8Array.from(DEMO_MEMORY_HEX.match(/../g), (h) => parseInt(h, 16));
  assert.deepEqual(new MouseMemory(bytes).toArray(), loadDump("baseline.txt"));
});
