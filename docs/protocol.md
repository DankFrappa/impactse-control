# Impact SE configuration protocol

Decoded from Redragon's `RDCfg.exe` (version 1.0.0) and confirmed by reading a real mouse.
Real dumps are in `tests/ImpactSE.Core.Tests/Fixtures`.

## Device

| | |
|---|---|
| USB ID | `36B7:FD16` |
| Config collection | interface 2, collection 2 (`mi_02&col02`), usage page `FFA0` |
| Reports | ID 2 = 16 bytes, ID 3 = 64 bytes (feature reports) |

## Commands

Every access must be wrapped in a session, or reads return zeros.

| Purpose | Bytes |
|---|---|
| Open session | `02 F5 00` |
| Read | `03 F2 addrLo addrHi len`, then GET_FEATURE report 3 |
| Read reply | `03 08 addrLo addrHi len 00 FA FA data…` |
| Write | `03 F3 addrLo addrHi len 00 00 00 data…` |
| Apply | `02 F1 02 flag` for flags `04 01 02 08 10` |
| Close session | `02 F5 01` |

Reads and writes use at most 0x20 data bytes each, as the official app does.

## Memory map

| Address | Contents | Status |
|---|---|---|
| `0x2C` | Active profile (0–4) | confirmed |
| `0x32 + 2p` | Polling rate: 1 = 1000 Hz, 2 = 500 Hz (4 = 250, 8 = 125 assumed) | confirmed for 1000/500 |
| `0x3C` | Key-map mode (`1F` on this firmware) | read only |
| `0x40`, `0x100`, `0x1B0`, `0x260`, `0x310` | Profile 1–5 base | confirmed |
| base + 0 | DPI header: `count 00 startStage 00` (count is 5) | confirmed |
| base + 4 + 6s | DPI stage s: `enabled xLo xHi yLo yHi 00`, value = DPI × 1.25 / 100 (steps of 80 DPI, shown rounded to 100). See "Factory profiles" | confirmed |
| base + 0x42 + 4b | Button b: `type modifier code extra` (24 buttons). See "Button actions" below | confirmed |
| buttons 0–5 | Top buttons 1–6 as numbered in Redragon's picture (factory: left, right, middle, back, DPI up, DPI down) | confirmed |
| buttons 6–17 | The 12 side buttons, in order | confirmed |
| button 18 | Factory `8D` (profile cycle); not shown in Redragon's app | read only |
| buttons 22, 23 | Scroll wheel up / down (factory `8B`, `8C`) | confirmed |
| `0x448 + 8p` | Lighting for profile p: `?? R G B mode …` | confirmed for color |
| `0x480`– | Macro area (all `FF` when empty) | not decoded |

## Button actions

From the lookup table in RDCfg.exe at 0x4a8980 (app function id → 4 bytes sent to the mouse).

| Bytes | Action |
|---|---|
| `90 00 kk 00` | Keyboard key, HID usage `kk` |
| `8F mm kk 00` | Key combo: modifier bits `mm` (1 Ctrl, 2 Shift, 4 Alt, 8 Win) plus key `kk` |
| `8E 01 lo hi` | Media key, consumer usage `hi lo` (CD play/pause, B7 stop, B6 previous, B5 next, E9 volume up, EA volume down, E2 mute) |
| `81` / `82` / `83` | Left / right / middle click |
| `84` / `85` | Back / forward |
| `86` / `87` | Tilt (scroll) left / right |
| `88` / `8A` / `89` | DPI cycle / up / down |
| `8B` / `8C` | Scroll up / down |
| `8D` / `94` / `95` | Profile cycle / up / down |
| `97` / `98` | Polling rate up / down |
| `9B 04 00 00` | LED switch |
| `00 00 00 00` | Disabled |
| `91`, `99`, `9A`, `93`, `9D` | Macro, fire key, sniper, launch app, other: not supported yet |

## Factory profiles

Redragon's app ships five default profiles (`REDRAGON M908WB\Profile\Profile 1–5.prof`, Qt INI files). They are
identical except for the light color and the DPI stage each one starts on:

| Profile | Light | Starts at |
|---|---|---|
| 1 | red `FF 00 00` | stage 1 (400 DPI) |
| 2 | blue `00 00 FF` | stage 2 (800 DPI) |
| 3 | green `00 FF 00` | stage 3 (1600 DPI) |
| 4 | pink `FF 00 FF` | stage 4 (2400 DPI) |
| 5 | yellow `FF FF 00` | stage 5 (3200 DPI) |

All of them: 1000 Hz; DPI stages 400, 800, 1600, 2400, 3200 (all on); top buttons left, right, middle, back,
DPI up, DPI down; side buttons 1–0, Num +, Num −; wheel scroll up and down.

The `.prof` files store DPI in hundreds (4, 8, 16, 24, 32) and the mouse stores 5, 10, 20, 30, 40 for the same
stages, so the mouse's unit is 80 DPI. This also matches what Redragon's app wrote for 800 (`0A`) and 900 (`0B`).
