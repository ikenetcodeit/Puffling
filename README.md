<div align="center">

<img src="docs/previews/happy-ghast/hero.png" alt="Three tiny Happy Ghasts floating among blocky clouds" width="960">

# Puffling ☁️

### A tiny Happy Ghast for your desktop.

This `happy-ghast` branch gives Puffling a fan-made Minecraft Happy Ghast skin: a pale cube, smiling face, short dangling tentacles, leather harness, and raised blue goggles. It floats around your Windows desktop, notices you, gets playful, and eventually falls asleep.

**Windows 10/11 · C# · .NET 8 · 48 px of personality by default**

<em>Illustration and GIFs are made from this branch's in-app renders.</em>

</div>

---

## A day with Puffling

It starts with a quiet float. Move your cursor close and it gets curious. Give it a click and it lights up; give it two and suddenly there's a dance. If you leave it alone for a while, it gets drowsy and drifts off to sleep.

| A little attention | A lot of excitement | Time for a nap |
| :---: | :---: | :---: |
| <img src="docs/previews/happy-ghast/curious.gif" alt="Puffling looking toward the cursor" width="144"> | <img src="docs/previews/happy-ghast/dance.gif" alt="Puffling dancing" width="144"> | <img src="docs/previews/happy-ghast/sleeping.gif" alt="Puffling sleeping" width="144"> |
| **Hover nearby** | **Double-click** | **Let it rest** |

Puffling has more to do than float in one spot. It can wander, travel between monitors, follow your cursor, or come when you call it from the tray menu. Pet it with a few back-and-forth strokes for hearts. Pick it up and let go with a flick to watch it wobble back into place.

### Bring a couple of friends

Right-click a ghast or the tray icon and choose **Spawn another**. You can have **up to three** on screen at once. Each one wanders and reacts on its own. When they share a monitor, they occasionally meet for a little performance or game:

| Two ghasts: a mirrored, heart-filled flyby | Three ghasts: a tiny musical carousel |
| :---: | :---: |
| <img src="docs/previews/happy-ghast/pair-play.gif" alt="Two Happy Ghasts float around each other, hopping in turns with hearts" width="300"> | <img src="docs/previews/happy-ghast/trio-carousel.gif" alt="Three Happy Ghasts circle together with staggered hops and music notes" width="300"> |

| Chase each other | Bounce sparring | Three-Puffling pong |
| :---: | :---: | :---: |
| <img src="docs/previews/happy-ghast/chase.gif" alt="Three Pufflings chasing in a loop" width="220"> | <img src="docs/previews/happy-ghast/spar.gif" alt="Two Pufflings bouncing toward and away from each other" width="220"> | <img src="docs/previews/happy-ghast/pong.gif" alt="A green Puffling bouncing between red and blue Pufflings" width="220"> |

The **red** Puffling is composed and curious, the **blue** one is bubbly and playful, and the **green** one is bold and quick. Their harness colors, faces, idle pace, wandering, and reactions reflect those temperaments. Each has its own move in the right-click menu: *Lead a rally*, *Do a twirl*, or *Show a dash*. Their looks stay with their roster positions after restart. Click or drag one and the whole group gives way to you. Right-click a specific ghast for **Remove this Puffling**, or use **Remove newest** in the tray menu. Puffling remembers how many you had and where they were after restart.

| Pet me | Pick me up | Off I go |
| :---: | :---: | :---: |
| <img src="docs/previews/happy-ghast/affection.gif" alt="Puffling showing hearts when petted" width="144"> | <img src="docs/previews/happy-ghast/dragged.gif" alt="Puffling being dragged" width="144"> | <img src="docs/previews/happy-ghast/travel.gif" alt="Puffling traveling" width="144"> |

## Invite Puffling onto your desktop

Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0), then run this on **Windows 10 or 11** from the repository folder:

```powershell
dotnet run --project src/Puffling.App -c Release
```

Puffling appears near the lower-right corner of your primary monitor. **Left-click its tray icon** to open Settings. **Right-click the tray icon or Puffling** for actions like *Dance!*, *Come here*, *Go to sleep*, and *Freeze position*.

Want an executable? The build script runs the tests and publishes to `dist/win-x64/Puffling.exe`:

```powershell
./build.ps1                 # requires the .NET 8 runtime on the target PC
./build.ps1 -SelfContained  # bundles the runtime into a larger executable
```

Use `-Runtime win-arm64` when building for Windows on ARM.

## Let Puffling remind you

Puffling can read your **Outlook calendar** and **Microsoft To Do** lists through Microsoft Graph. At your chosen local time, it shows a short bubble with your open to-dos. It also gives you a heads-up before timed events in your default calendar. Reminders appear beside Puffling while the app is running; the bubble closes on its own after a few seconds.

To connect your account:

1. [Register an application in Microsoft Entra](https://learn.microsoft.com/en-us/graph/auth-register-app-v2). Choose **Accounts in any organizational directory and personal Microsoft accounts** if you want to use either a work account or Outlook.com.
2. Add a **Mobile and desktop applications** platform with the redirect URI `http://localhost`. Copy the **Application (client) ID**. You do not need a client secret.
3. Under **API permissions**, add delegated Microsoft Graph permissions **Calendars.ReadBasic** and **Tasks.Read**. Your organization may require an administrator to approve them.
4. Open **Puffling Settings → Microsoft reminders**, paste the client ID, choose a daily time (`HH:mm`) and an event lead time (1–120 minutes), then click **Connect Microsoft**. Finish sign-in in your browser.

Puffling checks for updates every five minutes. The daily bubble lists up to three open tasks and counts any others; event reminders default to 15 minutes before the start. You can turn reminders off or disconnect in the same window. The app requests read-only access, stores its sign-in cache encrypted for your Windows user in `%LOCALAPPDATA%\Puffling`, and never puts account tokens in `settings.json`.

## How to say hello

| You | Puffling |
| --- | --- |
| Hover nearby | Looks toward the cursor |
| Click | Red studies you, blue gives a happy bounce, and green gets excited; three quick clicks excite any Puffling |
| Double-click | Dances |
| Stroke back and forth without clicking | Shows hearts |
| Drag and release | Dangles, follows, and settles after a gentle toss |
| Click while it sleeps | Wakes up |

Settings let you choose its size, movement speed, animation intensity, activity level, and frame rate. You can also switch wandering and following on or off, keep it on top, start it with Windows, or let it hide during fullscreen apps. Preferences live in `%APPDATA%\Puffling\settings.json`.

## Small on purpose

Puffling's window defaults to **48 × 48 logical pixels** and can be set from **32 to 96**. It scales with your monitor's DPI, stays within the usable screen area, and keeps its animations inside that little square. Transparent pixels pass clicks through, and clicking Puffling leaves keyboard focus in your current app.

<p align="center">
  <img src="docs/previews/happy-ghast/actual_size.png" alt="Puffling rendered at actual 32, 48, and 96 pixel sizes"><br>
  <em>Actual pixel sizes: 32, 48, and 96. The animated previews above are enlarged.</em>
</p>

## What makes it move?

`Puffling.Core` contains the voxel character, software renderer, animations, behavior, movement, gesture detection, and settings. `Puffling.App` puts those pixels into a transparent Win32 overlay and adds the tray menu. The core has no Windows dependency, so its tests and preview generator can run on other systems.

```powershell
dotnet run --project tests/Puffling.Tests -c Release
dotnet run --project tools/Puffling.Preview -- preview expressions
```

The automated tests cover gestures, rendering bounds, movement, settings, and a simulated multi-monitor session. The Windows shell still needs hands-on testing for tray, fullscreen, and mixed-DPI behavior.

<p align="center">
  <img src="docs/previews/happy-ghast/expressions.png" alt="A lineup of Puffling's different expressions"><br>
  <em>One tiny face, a surprising number of moods.</em>
</p>

This is an unofficial fan project and is not affiliated with Mojang or Microsoft. The Happy Ghast design belongs to Minecraft; this branch recreates its look procedurally without bundling game textures or models.
