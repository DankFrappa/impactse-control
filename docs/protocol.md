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
| base + 0 | DPI header: `count 00 currentStage 00` | read only |
| base + 4 + 6s | DPI stage s: `enabled xLo xHi yLo yHi 00`, value = DPI / 100 + 2 | confirmed |
| base + 0x42 + 4b | Button b: `type modifier code 00`. Type `90` = keyboard key (code = HID usage). `81` left, `82` right, `83` middle | confirmed for keys |
| buttons 6–17 | The 12 side buttons, in order | confirmed |
| `0x448 + 8p` | Lighting for profile p: `?? R G B mode …` | confirmed for color |
| `0x480`– | Macro area (all `FF` when empty) | not decoded |
