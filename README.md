# PlaySounds (CounterStrikeSharp)

A CS2 plugin for [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) that lets admins play sounds to players during a match, plus a personal radio any player can use.

## Features

- Play a sound to everyone, to specific players only, or at a player's position (3D, heard by anyone nearby).
- Step-by-step menu (sound → who → player), including a random alive player option.
- Personal radio: each player picks songs that only they can hear, with their own volume.
- Sound aliases, command names and permissions are configured in a JSON file, and it can be reloaded without restarting the server.
- Messages and menus in Spanish or English (`"Language"` option), easy to extend with more languages.

## Requirements

- [CounterStrikeSharp](https://github.com/roflmuffin/CounterStrikeSharp) **1.0.375** or newer (.NET 10).
- [MultiAddonManager](https://github.com/Source2ZE/MultiAddonManager), so players download your Workshop addon with the sounds.

## Installation

1. Download `PlaySounds.zip` from the [latest release](../../releases/latest) and extract it into `game/csgo/`. This places the plugin at `addons/counterstrikesharp/plugins/PlaySounds/PlaySounds.dll`.
2. Start the server (or change map) once. The config file is created at `game/csgo/addons/counterstrikesharp/configs/plugins/PlaySounds/PlaySounds.json`.

To build it yourself instead: `dotnet build -c Release` (requires the .NET 10 SDK) and copy `bin/Release/net10.0/PlaySounds.dll` to the folder above.

## Commands

These are the default names. They can be changed in the config (see [Renaming commands](#renaming-commands)).

| Command | What it does |
|---|---|
| `!psmenu` | Opens the menu: sound → who → player. |
| `!psall <sound> [volume]` | Everyone hears it, "inside their head". |
| `!psto <target> <sound> [volume]` | Only the target hears it. |
| `!psat <target> <sound> [volume]` | Plays at the target's position; anyone nearby hears it (3D sound). |
| `!pslist` | Lists the configured sound aliases. |
| `!psreload` | Reloads `PlaySounds.json` without restarting the server. |
| `!radio` / `!radiostop` | Personal radio for any player (see [Radio](#radio)). |

In chat, use `!` (or `/` to hide the message). In the console, use the `css_` prefix: `css_psmenu`, `css_psto`...

- `<sound>`: an alias from the config (`alert`) or a sound event name (`sounds.alert`).
- `<target>`: player name, `#userid`, `@all`, `@ct`, `@t`, `@alive`, `@dead`, `@me`...
- `[volume]`: optional, from `0` to `1`.

Examples:
```
!psto "John" alert           // only John hears it
!psat @ct steps 0.8          // footsteps next to every CT
!psall alarm                 // alarm for the whole server
```

Admin commands require the `@css/generic` permission by default (configurable). The server console can always use them.

### Menu (`!psmenu`)

1. Pick a sound (listed in the same order as in the config).
2. Pick who hears it: **everyone**, **only one player**, or **next to one player (3D)**.
3. For a player, pick them from the list (alive players first, with their team) or choose a **random alive player**.

After playing a sound, the menu goes back to the sound list so you can play several in a row (disable with `"ReopenMenuAfterPlay": false`). With the chat menu you choose options by typing `!1`, `!2`...; set `"MenuType": "center"` to show the menu in the center of the screen instead.

## Custom sounds

CS2 servers can't play loose `.mp3`/`.wav` files. Sounds must be **sound events** that clients have already downloaded, so they have to be packed into a **Workshop addon**:

1. Install the CS2 Workshop Tools and create an addon.
2. Put your audio files (`.wav` or `.mp3`) in `content/csgo_addons/<your_addon>/sounds/...`.
3. Copy `addon-ejemplo/soundevents/soundevents_addon.vsndevts` to `content/csgo_addons/<your_addon>/soundevents/` and edit the names and paths.
4. Compile the addon and upload it to the Workshop.
5. On the server, add the Workshop ID to `mm_extra_addons` in `cfg/multiaddonmanager/multiaddonmanager.cfg`, so players download it when they join.
6. Add the aliases to the config (see below) and change map.

Paths in the `.vsndevts` file:

- start at `sounds/` (no absolute paths),
- use forward slashes `/`,
- use the `.vsnd` extension, even if the source file is `.wav` or `.mp3`. For example, `sounds/alerts/alarm.wav` is written as `"sounds/alerts/alarm.vsnd"`.

Sound event types:

- `csgo_mega`: same volume wherever the player is. Best for `!psall`, `!psto` and radio songs.
- `csgo_3d`: fades with distance (`distance_max`). Best for `!psat`, where it sounds like something is right next to the player.

## Configuration

`game/csgo/addons/counterstrikesharp/configs/plugins/PlaySounds/PlaySounds.json`:

```json
{
  "Language": "es",
  "AdminFlag": "@css/generic",
  "ChatPrefix": " {green}[PlaySounds]{default}",
  "SoundEventFiles": [ "soundevents/soundevents_addon.vsndevts" ],
  "DefaultVolume": 1.0,
  "NotifyAdmins": true,
  "MenuType": "chat",
  "ReopenMenuAfterPlay": true,
  "Commands": {
    "Menu": [ "psmenu" ],
    "PlayAll": [ "psall" ],
    "PlayTo": [ "psto" ],
    "PlayAt": [ "psat" ],
    "List": [ "pslist" ],
    "Reload": [ "psreload" ]
  },
  "Radio": {
    "Enabled": true,
    "Permission": "",
    "DefaultVolume": 0.5,
    "Commands": [ "radio" ],
    "StopCommands": [ "radiostop" ],
    "Songs": {
      "Song 1": "radio.song1",
      "Song 2": "radio.song2"
    }
  },
  "Sounds": {
    "alert": "sounds.alert",
    "alarm": "sounds.alarm",
    "steps": "sounds.steps"
  }
}
```

| Option | Description |
|---|---|
| `Language` | Language for messages and menus: `es` (Spanish) or `en` (English). New languages can be added in `Messages.cs`. |
| `AdminFlag` | Permission required for the admin commands. |
| `ChatPrefix` | Prefix for chat messages. Supports color tags such as `{green}`, `{red}`, `{default}`. |
| `SoundEventFiles` | Sound event files precached on every map load. They must exist in your Workshop addon. Changes apply after a map change. |
| `DefaultVolume` | Volume (0–1) used when a command doesn't specify one. |
| `NotifyAdmins` | Tells other admins in chat who played what. |
| `MenuType` | `chat` or `center`. |
| `ReopenMenuAfterPlay` | Reopen the sound list after playing a sound from the menu. |
| `Commands` | Command names (see [Renaming commands](#renaming-commands)). |
| `Radio` | Radio settings (see [Radio](#radio)). |
| `Sounds` | Alias shown in the menu → sound event name, in menu order. |

To add a sound, add a line to `"Sounds"` (`"menu name": "sound.event.name"`) and run `!psreload`. The sound event name must match the `.vsndevts` file exactly.

### Renaming commands

Use the `"Commands"` section. Write names without `!` or `css_`. Each command can have several names:

```json
"Commands": {
  "Menu": [ "sounds", "smenu" ],
  "PlayAll": [ "sall" ],
  "PlayTo": [ "sto" ],
  "PlayAt": [ "sat" ],
  "List": [ "slist" ],
  "Reload": [ "sreload" ]
}
```

With this, the menu opens with `!sounds` or `!smenu`. If the section is missing, the default names are used. Changes apply after running the reload command (using its current name).

## Radio

Any player can type `!radio` and pick a song that **only they hear**. The menu has:

- **Stop**: stops the song (also `!radiostop`).
- **Volume**: 25% → 50% → 75% → 100%. Each player has their own volume, which is kept across maps and applies to the next song.
- **Random** and the song list (the current song is marked with ▶).

Options in the `"Radio"` section:

- `Enabled`: turns the radio on or off.
- `Permission`: leave empty so everyone can use it, or set a permission (e.g. `@css/vip`) to restrict it.
- `DefaultVolume`: starting volume for each player.
- `Commands` / `StopCommands`: command names, same rules as above.
- `Songs`: same as `Sounds`, menu name → sound event name. Songs go in the same addon and `.vsndevts` file, with `type = "csgo_mega"`.

Songs make the addon players download much bigger, so prefer `.mp3` over `.wav` for them.

## Tips

- Use `!psto @me <sound>` to test a sound without anyone else hearing it.
- Test every sound on a local server first: if a sound event doesn't exist or the addon wasn't downloaded, nothing plays and no error is shown.

## License

[MIT](LICENSE)
