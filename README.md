# Jellyfin Plugins

A collection of custom plugins for **Jellyfin 12.1** (the next-generation build that
runs on **.NET 10**, plugin ABI `12.0.0.0`).

> ⚠️ These plugins target the Jellyfin **12.x** line specifically. They are **not**
> compatible with the Jellyfin 10.x stable series without retargeting to `net8.0`
> and adjusting some namespaces.

Each plugin lives in its own folder with its own README and build script.

## Plugins

### 📺 [Continuous TV](./Jellyfin.Plugin.ContinuousTV)
Build a smart, never-ending playlist that mixes the next unwatched episode from several
series (and optional movies) the way an old TV channel would — always resuming at the
right episode and sampling longer series proportionally more often than short ones.

- Resume-aware: continues each series from its first unwatched episode.
- Proportional interleaving via smooth weighted round-robin.
- Dashboard to create, preview, and generate playlists.
- Daily scheduled task keeps playlists fresh.

→ See [Jellyfin.Plugin.ContinuousTV/README.md](./Jellyfin.Plugin.ContinuousTV/README.md)

### 🏷️ [Translation Tags](./Jellyfin.Plugin.TranslationTags)
Bulk-tag movies and series by their translation type (e.g. *Napisy, Lektor, Dubbing,
Brak Tłumaczenia* — Polish for subtitles / voice-over / dubbing / no translation) and
turn any tag into a collection. Tags are stored as native Jellyfin tags, so you can
filter the library by them immediately.

- Poster-grid picker with multi-select and a per-poster ▶ play button.
- Quick-assign predefined tags, add custom tags, or clear all tags in bulk.
- One-click "create collection from tag".
- Sticky toolbar so actions stay reachable while scrolling.

→ See [Jellyfin.Plugin.TranslationTags/README.md](./Jellyfin.Plugin.TranslationTags/README.md)

## Building

Each plugin targets **.NET 10** and references the Jellyfin 12.x server assemblies
directly from the local install (they are not published to NuGet). From a plugin folder:

```bash
dotnet build -c Release
```

Override the server location if it is not the Windows default
(`C:\Program Files\Jellyfin\Server`):

```bash
dotnet build -c Release -p:JellyfinServerDir="D:\Apps\Jellyfin\Server"
```

Each plugin also ships a `build-and-install.ps1` helper that builds and copies the
plugin into the local Jellyfin plugins folder.

## Installation (general)

1. Build the plugin (or grab a release DLL).
2. Copy `<plugin>.dll` and `meta.json` into a new folder under your Jellyfin data dir:
   `…/plugins/<Plugin Name>_<version>/`.
3. On Windows the service often runs as `NT Authority\NetworkService`; grant it write
   access to that folder so it can update `meta.json`:
   ```powershell
   icacls "C:\ProgramData\Jellyfin\Server\plugins\<Plugin Name>_<version>" /grant "NETWORK SERVICE:(OI)(CI)F" /T
   ```
4. Restart Jellyfin and open the plugin from **Dashboard → Plugins**.

## License

MIT — see [LICENSE](./LICENSE).
