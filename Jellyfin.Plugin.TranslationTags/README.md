# Translation Tags — Jellyfin Plugin

Bulk-tag movies and series by their **translation type** and turn any tag into a
collection. Tags are stored as **native Jellyfin tags**, so you can filter the library
by them straight away (*Movies → Filters → Tags*).

Built for a Polish library, the default predefined tags are:

| Tag | Meaning |
| --- | --- |
| **Napisy** | Subtitles |
| **Lektor** | Voice-over (single narrator over the original audio) |
| **Dubbing** | Full dubbing |
| **Brak Tłumaczenia** | No translation (original audio only) |

You can also apply any **custom tag** you type in.

## Features

- 🖼️ **Poster-grid picker** — pick a library, see cover art in a responsive grid.
- ✅ **Multi-select** by clicking posters (with "select all visible" / "clear").
- ▶️ **Play button** on each poster that opens the item in a new tab so you can verify
  the translation before tagging.
- 🏷️ **Quick-assign** predefined tags, or **add / remove a custom tag**.
- 🧹 **Clear ALL tags** in bulk — handy for stripping the noisy auto-imported tags
  Jellyfin adds (e.g. "11th century", "sexual humor", …).
- 🐚 **Create a collection from a tag** with one click — it shows up under
  *Movies → Collections*.
- 📌 **Sticky toolbar** so the action bar stays at the top while you scroll.
- 🔎 **Filter** the grid by search term, by a specific tag, or show only untagged items.

## Why this plugin?

Jellyfin can already *filter* by tags, but it has no convenient way to **bulk-apply**
them, and there is no reliable way to auto-detect "Lektor vs Dubbing" from metadata.
This plugin gives you a fast manual workflow: watch a snippet, tag it, move on.

## Requirements

- Jellyfin **12.1+** (next-gen, .NET 10)
- .NET **10** SDK (to build from source)

## Building

```bash
dotnet build -c Release
```

The Jellyfin 12.x assemblies are referenced directly from the local server install via
the `JellyfinServerDir` MSBuild property (default `C:\Program Files\Jellyfin\Server`):

```bash
dotnet build -c Release -p:JellyfinServerDir="D:\Apps\Jellyfin\Server"
```

`build-and-install.ps1` builds the plugin and copies it into the local Jellyfin
plugins folder.

## Installation

1. Build (or download the release DLL).
2. Copy `Jellyfin.Plugin.TranslationTags.dll` and `meta.json` into:
   `…/plugins/Translation Tags_1.0.0.0/`
   (on Windows: `C:\ProgramData\Jellyfin\Server\plugins\`).
3. If the service runs as `NT Authority\NetworkService`, grant write access once:
   ```powershell
   icacls "C:\ProgramData\Jellyfin\Server\plugins\Translation Tags_1.0.0.0" /grant "NETWORK SERVICE:(OI)(CI)F" /T
   ```
4. Restart Jellyfin, then open **Dashboard → Plugins → Translation Tags → Settings**.

## Usage

1. Choose a **library** from the dropdown.
2. (Optional) click ▶ on a poster to watch a snippet in a new tab and confirm the
   translation type.
3. Click posters to select them (or **Zaznacz widoczne** to select all visible).
4. Click a tag chip under **"Przypisz zaznaczonym"** to apply it, or type a custom tag
   and press **Przypisz** / Enter.
5. To browse everything of one kind later: **Movies → Filters → Tags → (your tag)**,
   or use **"Utwórz kolekcję z tagu"** to make a permanent collection.

## Project layout

```
Plugin.cs                        Plugin entry point + dashboard page registration
PluginServiceRegistrator.cs      DI registration
TagService.cs                    Tag read/write, collection creation
Api/TranslationTagsController.cs REST API backing the dashboard
Configuration/
  PluginConfiguration.cs         Predefined tag list
  configPage.html                Dashboard UI (poster grid)
meta.json                        Plugin manifest
```

## License

MIT — see the [root LICENSE](../LICENSE).
