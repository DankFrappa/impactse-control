# Changelog

Every change to Impact SE Control, newest first. The web page and the Windows download update by
themselves when a change lands.

## October 24, 2026

- **Planned:** save one profile to a file and load it back later, so you can try a new button layout and
  return to exactly what you had. A user asked for this. For now, the Backups tab saves and restores all
  five profiles at once.
- Added this changelog.

## October 23, 2026

- **More reliable saving.** Some DPI changes needed saving twice. Now, after every save, the app reads the
  mouse back, and if a change didn't stick it waits a moment and sends it once more. The Windows app writes
  a short `save-log.txt` in its backups folder to help track down any save that still fails.
- **DPI stages are safer to switch off.** The stage a profile starts on always stays switched on, and you
  can't switch every stage off.
- **Same color tools in both versions.** The web page now has the Windows app's full color picker (color
  area, brightness slider, hex and Red/Green/Blue boxes), and the Windows app has the web page's one-click
  quick colors.
- **Ready-made profiles.** Load one of Redragon's five factory profiles into any profile, or start from a
  ready-made setup: MMO (side buttons on F13–F24), Shooter (low DPI) or Everyday and media. Find them under
  **Profiles & polling**. Only the profile you're editing changes, and only after you click **Save to mouse**.
- **Reset to factory.** Each profile has a **Reset this profile to factory settings** button.
- **DPI sliders in the Windows app**, like the web page has.
- **DPI numbers now match Redragon's app.** The mouse counts DPI in steps of 80, and the numbers are now
  shown the same way Redragon's software shows them. For example, the factory stages read 400, 800, 1600,
  2400 and 3200. DPI now goes from 200 to 10,000.
- The web page's side-button preset menu starts empty, so nothing is picked by accident.

## October 22, 2026

- **First release of the Windows app.** Change what every button does (the 12 side buttons, the 6 top
  buttons and the scroll wheel), set the 5 DPI stages, the light color and the polling rate, and choose
  which profile the mouse uses. One-click side-button presets include F13–F24, keys no keyboard has, so
  games can bind them without clashing with your keyboard.
- **Safe by design.** Nothing reaches the mouse until you click **Save to mouse**, the app backs up your
  settings every time it connects, and it won't save a profile without a left click.
- **The Windows app is a single ImpactSE.exe** with a permanent download link.
- **Web version.** The same settings in Chrome or Edge, with nothing to install:
  https://dankfrappa.github.io/impactse-control/
- **Step-by-step instructions** in the README and on the web page, including a Help section.
- Released under the MIT License, with a note that this is an independent project, not affiliated with
  Redragon.
