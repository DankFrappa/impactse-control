# impactse-control

A modern Windows app for configuring the Redragon Impact SE (wired) MMO mouse, replacing Redragon's settings tool.

## What it does

- **Side buttons:** set each of the 12 side buttons to any key, with one-click presets (number row, numpad, or F13–F24 so they never clash with your keyboard)
- **DPI:** turn the 5 stages on or off and set each one from 100 to 10,000
- **Lighting:** pick each profile's color
- **Polling rate** and **active profile**
- **Backups:** a full copy of the mouse's settings is saved every time the app connects, and the original can be restored in one click

Nothing is sent to the mouse until you click **Save to mouse**.

## Running it

Download the latest `ImpactSE-win-x64` build from the Actions tab, unzip it, and run `ImpactSE.exe`.
Close Redragon's app first so the two don't talk to the mouse at the same time.

## Building

- Core library and tests (any OS): `dotnet test`
- Windows app (Visual Studio MSBuild): `msbuild src/ImpactSE.App/ImpactSE.App.csproj /restore /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64 /p:SelfContained=true`

How the mouse is talked to is documented in [docs/protocol.md](docs/protocol.md).
