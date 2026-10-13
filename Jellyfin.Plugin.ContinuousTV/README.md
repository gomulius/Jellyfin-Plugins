# Continuous TV — Jellyfin Plugin

Build a smart, **never-ending playlist** that mixes the next unwatched episode from
several series (and optional movies) the way an old TV channel would — always resuming
at the right episode and sampling **longer series proportionally more often** than short ones.

> Example: pick *Friends* (230+ episodes) and *Band of Brothers* (10 episodes). The
> generated playlist plays several *Friends* episodes for every single *Band of Brothers*
> episode, nicely spread out instead of in long blocks — just like channel-surfing a
> curated TV station.

## Features

- 🎛️ **Dashboard page** to create, edit, preview, and generate playlists.
- ⏭️ **Resume-aware** — for each series it finds the first *unwatched* episode (in aired
  order) and continues from there; already-watched episodes are skipped.
- ⚖️ **Proportional interleaving** using smooth weighted round-robin (the algorithm Nginx
  uses) so high-episode-count shows appear more often *and* are evenly distributed.
- 🎚️ **Weighting modes:** by total episodes, by remaining unwatched episodes, or equal.
- 🎬 **Movies** can be mixed in with a configurable weight.
- 🔁 **Daily scheduled task** rebuilds playlists so they always resume where you left off.

## Requirements

> ⚠️ This plugin was developed and tested against **Jellyfin 12.1** (the next-generation
> build that runs on **.NET 10**, plugin ABI `12.0.0.0`). It is **not** compatible with the
> Jellyfin 10.x stable line. If you are on 10.x you will need to retarget the project to
> `net8.0` and adjust a few namespaces.

- Jellyfin **12.1+** server
- .NET **10** SDK (only needed to build from source)

## Installation (pre-built)

1. Build the plugin (see below) or download a release `.dll`.
2. Copy `Jellyfin.Plugin.ContinuousTV.dll` and `meta.json` into a new folder:
   ```
   <Jellyfin data>/plugins/Continuous TV_1.0.0.0/
   ```
   On Windows that data folder is typically `C:\ProgramData\Jellyfin\Server\plugins\`.
3. **Windows service note:** the Jellyfin service often runs as `NT Authority\NetworkService`,
   which must be able to write `meta.json`. Grant it access once:
   ```powershell
   icacls "C:\ProgramData\Jellyfin\Server\plugins\Continuous TV_1.0.0.0" /grant "NETWORK SERVICE:(OI)(CI)F" /T
   ```
4. Restart Jellyfin. You should see `Loaded plugin: "Continuous TV"` in the logs.
5. Open **Dashboard → Plugins → Continuous TV** (also pinned to the main menu).

## Building from source

```bash
dotnet build -c Release
```

The output DLL is placed in `bin/Release/net10.0/Jellyfin.Plugin.ContinuousTV.dll`.

Because the Jellyfin 12.x assemblies are **not published to NuGet**, the project references
them directly from the local server install via the `JellyfinServerDir` MSBuild property
(default `C:\Program Files\Jellyfin\Server`). Override it if your install lives elsewhere:

```bash
dotnet build -c Release -p:JellyfinServerDir="D:\Apps\Jellyfin\Server"
```

A helper script `build-and-install.ps1` builds the plugin and copies it into your local
Jellyfin plugins folder.

## Usage

1. Open the **Continuous TV** page in the dashboard.
2. Click **Create new playlist**.
3. Choose the owning user, give it a name, pick a weighting mode and item count.
4. Search and select the **series** (and optionally **movies**) to include.
5. Click **Preview order** to sanity-check the mix, then **Save & generate now**.
6. The generated playlist appears in your normal Jellyfin **Playlists** — press play and
   it runs like a TV channel.

The daily scheduled task (**Regenerate Continuous TV playlists**) refreshes every saved
playlist so it keeps resuming at the next unwatched episode.

## How the scheduling works

Each series becomes a queue of its unwatched episodes in aired order. Movies become one
pooled queue. Each queue gets a **weight**:

| Mode | Weight |
| --- | --- |
| Total episodes | number of episodes in the series |
| Remaining | number of unwatched episodes |
| Equal | `1` |

Items are then emitted using **smooth weighted round-robin**: every step each source's
`current += weight`, the highest-`current` source is chosen and has `totalWeight`
subtracted. This yields an even, proportional spread rather than long consecutive runs.

## Project layout

```
Plugin.cs                         Plugin entry point + dashboard page registration
PluginServiceRegistrator.cs       DI registration
ContinuousPlaylistService.cs      Core scheduling + playlist generation logic
Api/ContinuousTvController.cs     REST API backing the dashboard
Configuration/
  PluginConfiguration.cs          Saved playlist definitions
  configPage.html                 Dashboard UI
ScheduledTasks/
  RegeneratePlaylistsTask.cs      Daily auto-refresh
meta.json                         Plugin manifest
```

## License

MIT — see the [root LICENSE](../LICENSE).
