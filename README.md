# Impact SE Control

A clean, modern Windows 11 app for setting up the **Redragon M908 Impact SE** (wired) MMO mouse. It replaces Redragon's own settings software.

It works with the mouse's built-in memory, so your settings stay on the mouse. You don't need to keep this app running, and it installs no drivers.

## Features

- **Every button, your choice.** You can set the 12 side buttons, the 6 top buttons and the scroll wheel to:
  - any keyboard key
  - a mouse action: left, right or middle click, back, forward, scroll, DPI up or down, profile switch
  - a media key: play/pause, volume, mute
  - a shortcut like Copy, Paste or Alt+Tab
- **One-click side-button presets.** Choose number row 1–0, numpad, or **F13–F24**. F13–F24 are real keys that no keyboard has, so games can bind them without clashing with your keyboard.
- **DPI:** switch each of the 5 stages on or off and set it anywhere from 100 to 10,000.
- **Lighting:** a color picker for each profile.
- **Polling rate**, and which of the 5 profiles the mouse uses.
- **Safe by design:**
  - Nothing reaches the mouse until you click **Save to mouse**.
  - Every time the app connects, it saves a backup of the mouse's settings. You can restore the original settings in one click.
  - The app won't let you save a profile that has no left-click button.

## Getting it

1. Open the [Actions](../../actions) tab and click the newest green **Build** run.
2. Scroll to the bottom. Under **Artifacts**, download **ImpactSE-win-x64**.
3. Extract the zip. Inside is a single file, **ImpactSE.exe**. Put it anywhere you like.
4. Close Redragon's software if it's running, then double-click **ImpactSE.exe**.

The first start takes a few seconds while the app unpacks itself. Windows may show a SmartScreen warning because the app isn't signed. If it does, click **More info**, then **Run anyway**.

## Not done yet

- Lighting effects (breathing, rainbow, and others) and brightness
- Macros
- Custom key combos beyond the built-in shortcuts

## For developers

The project has three parts:

- **`src/ImpactSE.Core`** is the mouse protocol (HID feature reports) and a typed view of the mouse's settings memory. It targets net8.0.
- **`src/ImpactSE.App`** is the WinUI 3 app. It's unpackaged and self-contained, and publishes as a single exe.
- **`tests/ImpactSE.Core.Tests`** contains xunit tests that run against real memory dumps from the mouse.

Build commands:

- Tests (any OS): `dotnet test`
- App (Windows, Visual Studio MSBuild): `msbuild src/ImpactSE.App/ImpactSE.App.csproj /restore /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64`

On every push, CI builds the app and starts it once to check it opens. The protocol and memory map are documented in [docs/protocol.md](docs/protocol.md).

Redragon is a trademark of its owner. This is an independent project and isn't affiliated with Redragon.
