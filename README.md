# Impact SE Control

Change the settings of your **Redragon M908 Impact SE** (wired) MMO mouse: what every button does, DPI, light color and polling rate. It replaces Redragon's own settings software.

Your settings are saved inside the mouse itself. Once you've saved them, they keep working on any computer, and you don't need to keep anything open or installed.

There are two ways to use it. **Most people should use the web page**: there's nothing to download.

## Option 1: Use it in your browser (easiest)

You need **Google Chrome** or **Microsoft Edge** on a Windows computer. Other browsers (Firefox, Safari) and phones can't talk to USB devices, so they won't work.

1. Plug in your Impact SE.
2. If Redragon's own software is open, close it. Check the little arrow by the clock in the bottom-right corner of your screen too: if Redragon's icon is there, right-click it and choose **Exit** or **Quit**.
3. Open **https://dankfrappa.github.io/impactse-control/** in Chrome or Edge.
4. Click the red **Connect mouse** button at the top right.
5. A small box pops up asking which device to connect. Click the mouse in the list (there should be only one), then click **Connect**.
6. Your current settings appear. Use the tabs (**Buttons**, **DPI**, **Lighting**, **Polling & profiles**, **Backups**) to change what you want.
7. Click **Save to mouse** at the bottom of the page. Nothing changes on the mouse until you do this.

You can click **Discard changes** at any time to throw away anything you haven't saved yet. Next time you visit the page, it reconnects to your mouse by itself.

**Just curious?** On the page, click **Just looking? Try it without a mouse** to look around with sample settings. Nothing is sent anywhere.

## Option 2: Download the Windows app

Use this if you'd rather have a regular program on your PC. It needs Windows 10 or 11.

1. Click this link to download the app: **[ImpactSE.exe](https://github.com/DankFrappa/impactse-control/releases/latest/download/ImpactSE.exe)**
2. If your browser warns that the file "isn't commonly downloaded", click the **…** next to it and choose **Keep** (in Edge you may also need **Show more**, then **Keep anyway**).
3. Open your Downloads folder. You can move **ImpactSE.exe** anywhere you like, such as your Desktop.
4. Close Redragon's software if it's running (see step 2 of Option 1).
5. Double-click **ImpactSE.exe**. The first start takes a few seconds.
6. Change your settings, then click **Save to mouse** at the top right.

**If Windows says "Windows protected your PC":** this blue box appears because the app is new and isn't signed by a big company. Click **More info**, then click **Run anyway**. You only need to do this once.

## Something not working?

- **The page says this browser can't talk to USB devices.** Open the link in Google Chrome or Microsoft Edge on a computer instead.
- **The mouse isn't in the list when you click Connect mouse.**
  1. Make sure the mouse is plugged in and works as a normal mouse.
  2. Close Redragon's software (see step 2 of Option 1).
  3. Close any other tab or window that has this page open.
  4. Unplug the mouse, wait five seconds, plug it back in, then refresh the page (press **F5**) and click **Connect mouse** again.
  5. If it's still missing, try a different USB port, preferably one directly on the computer rather than a hub.
- **"The mouse is busy" or "Couldn't connect".** Close Redragon's software and the Windows app (only one program can change the mouse's settings at a time), unplug and replug the mouse, then try again.
- **"Impact SE not found" in the Windows app.** Plug in the mouse, close Redragon's software, then close and reopen the app.
- **"No button in Profile … does a left click".** This protects you from ending up with a mouse that can't click. Set at least one button in that profile to **Left click**, then save again.
- **Something else.** [Open an issue](../../issues/new) and describe what you see. A screenshot helps.

## Putting your original settings back

Both versions keep a copy of your mouse's settings from before you first changed anything.

**On the web page:**

1. Connect the mouse, then open the **Backups** tab.
2. Click **Put back my original settings**, then click **Save to mouse**.

The web page keeps that copy in your browser, so use the same browser on the same computer. To keep a copy somewhere safe, click **Download a backup file** on the **Backups** tab. Later, you can use **Restore from a file** to load it, then click **Save to mouse**.

**In the Windows app:**

1. Open the **Backups** page on the left.
2. Click **Restore original settings**, then click **Save to mouse**.

Backup files work in both versions.

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

## Not done yet

- Lighting effects (breathing, rainbow, and others) and brightness
- Macros
- Custom key combos beyond the built-in shortcuts

## For developers

The project has three parts:

- **`src/ImpactSE.Core`** is the mouse protocol (HID feature reports) and a typed view of the mouse's settings memory. It targets net8.0.
- **`src/ImpactSE.App`** is the WinUI 3 app. It's unpackaged and self-contained, and publishes as a single exe.
- **`tests/ImpactSE.Core.Tests`** contains xunit tests that run against real memory dumps from the mouse.
- **`web`** is the browser version: plain HTML and JavaScript with no build step. It talks to the mouse through WebHID (Chrome and Edge on desktop). `web/mouse.js` is a port of ImpactSE.Core.

Build commands:

- Tests (any OS): `dotnet test`
- Web tests (Node 22): `node --test web/tests/*.test.mjs`
- Web page locally: serve `web/` (for example `python -m http.server -d web`) and open http://localhost:8000 in Chrome. WebHID needs `localhost` or HTTPS.
- App (Windows, Visual Studio MSBuild): `msbuild src/ImpactSE.App/ImpactSE.App.csproj /restore /t:Publish /p:Configuration=Release /p:Platform=x64 /p:RuntimeIdentifier=win-x64`

On every push, CI builds the app and starts it once to check it opens. A second workflow tests the web page and publishes `web/` to GitHub Pages from main. The protocol and memory map are documented in [docs/protocol.md](docs/protocol.md).

## License and disclaimer

Released under the [MIT License](LICENSE).

This is an independent project. It isn't affiliated with, endorsed by or supported by Redragon. Redragon is a trademark of its owner. It changes settings stored on your mouse, and you use it at your own risk. Both the app and the web page keep a backup of your settings so you can put them back.
