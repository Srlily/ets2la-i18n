# ETS2LA Localization

`ets2la-i18n` is a runtime localization plugin for [ETS2LA](https://github.com/ETS2LA/ETS2LA).
It translates ETS2LA's hardcoded English interface without modifying the ETS2LA application
source code, and lets users switch languages while the application is running.

The repository currently includes a Simplified Chinese (`zh-CN`) language pack. The localization
engine is language agnostic, so additional language packs can be added without changing the
plugin code.

## Features

- Runtime language switching from the ETS2LA interface.
- Persistent language selection across application restarts.
- English fallback for every string without a translation.
- Translation of Avalonia text, headers, content, tooltips, window titles, and accessibility names.
- Translation of ETS2LA's bottom-right Growl notifications, including dynamic notification text.
- Automatic processing of newly opened windows and dynamically created controls.
- A language selector in the ETS2LA sidebar.
- A dedicated Localization page in the ETS2LA Settings view.
- An embedded plugin icon based on ETS2LA's `ETS2LA/Assets/Installer/favicon.ico`.
- Headless translation, injection, and plugin-loading test tools.

## How It Works

ETS2LA currently stores most user-facing strings directly in C# and AXAML source files. This
project translates those strings at runtime in three layers:

1. `LocalizationLibrary` loads embedded JSON language packs and performs exact-string and
   placeholder-aware lookups.
2. `UiTranslator` walks the Avalonia visual and logical trees and replaces visible UI values while
   retaining the original English value for later language changes.
3. `NotificationTranslator` observes ETS2LA notifications and applies translations to the active
   Growl presentation without mutating the notification source objects.

The plugin revisits open windows periodically because ETS2LA creates some controls through data
templates after a page has already been opened. Switching back to English restores the original
English text automatically.

## Included Language Pack

| Code | Language | Source file |
| --- | --- | --- |
| `en-US` | English fallback | Built into the localization engine |
| `zh-CN` | 简体中文 (Simplified Chinese) | `Libraries/LocalizationLibrary/Translations/zh-CN.json` |

Missing entries intentionally remain in English. This prevents an incomplete language pack from
making controls unusable or hiding important error information.

## Requirements

- ETS2LA compatible with the API referenced by the `ETS2LA` submodule.
- .NET 10 SDK.
- Git with submodule support when building from a fresh clone.
- Python 3 only for the optional manifest helper scripts.

The plugin currently targets the latest official ETS2LA release, `v3.4.37`
(`SupportedETS2LA = "3.4.37"`). The compiled plugin also depends on the ETS2LA interfaces
referenced by the checked-out submodule. When ETS2LA publishes a newer release, update the
submodule, the supported version, and the compatibility notes together.

## Repository Layout

```text
.
├── ETS2LA/                                  # ETS2LA git submodule
│   └── Assets/Installer/favicon.ico         # Source icon used by the plugin
├── Libraries/
│   └── LocalizationLibrary/
│       ├── LocalizationLibrary.csproj       # Library plugin project
│       ├── LocalizationManager.cs           # Language loading and lookup
│       ├── Language.cs                       # Language-pack model
│       └── Translations/                     # Embedded JSON language packs
├── Plugins/
│   └── Localization/
│       ├── Localization.csproj               # Main plugin project
│       ├── Program.cs                        # Plugin lifecycle and settings page
│       ├── UiTranslator.cs                    # Avalonia UI translation
│       ├── NotificationTranslator.cs         # Bottom-right notification translation
│       ├── LanguageSelector.cs                # Sidebar language selector
│       ├── SettingsPageInjector.cs            # Settings-page injection
│       └── Assets/favicon.ico                 # Embedded plugin icon
├── Tools/
│   ├── TranslationTest/                     # Translation and placeholder checks
│   ├── InjectTest/                          # Headless Settings-tab injection test
│   └── LoadTest/                            # Plugin load and enable test
├── dist/                                    # Ignored local package staging directory
├── VERSION                                  # Release version used by GitHub Actions
├── RELEASE_NOTES.md                         # Release body for the current version
├── .github/workflows/build-release.yml      # CI build and release workflow
├── Localization.sln                         # Main solution
├── BuildYourPlugins.sh/.bat                  # Build and install plugin DLLs
├── InstallPlugins.sh/.bat/.ps1               # Install top-level DLLs for manual scanning
└── RegisterPlugins.sh/.bat/.ps1              # Register catalogue-style DLLs in the manifest
```

## Build From Source

Clone the repository together with the ETS2LA submodule:

```bash
git clone --recurse-submodules https://github.com/Srlily/ets2la-i18n.git
cd ets2la-i18n
```

If the repository was cloned without submodules, initialize them before building:

```bash
git submodule update --init --recursive
```

Restore and build the localization plugin in Release configuration:

```bash
dotnet restore Localization.sln
dotnet build Plugins/Localization/Localization.csproj --configuration Release
```

For a clean rebuild, use `Rebuild` explicitly:

```bash
dotnet build Plugins/Localization/Localization.csproj \
  --configuration Release \
  --target Rebuild
```

The important output files are:

```text
Libraries/LocalizationLibrary/bin/Release/net10.0/srlily.i18n.library.dll
Plugins/Localization/bin/Release/net10.0/srlily.i18n.dll
```

The `dist/` directory is generated locally and is intentionally not committed. When a local
catalogue-style package is needed, create it after building Release:

```bash
mkdir -p dist/Libraries/srlily.i18n.library dist/Plugins/srlily.i18n
cp Libraries/LocalizationLibrary/bin/Release/net10.0/srlily.i18n.library.dll \
  dist/Libraries/srlily.i18n.library/srlily.i18n.library.dll
cp Plugins/Localization/bin/Release/net10.0/srlily.i18n.dll \
  dist/Plugins/srlily.i18n/srlily.i18n.dll
```

The icon is compiled into `srlily.i18n.dll` as an Avalonia resource, so it remains available
when ETS2LA loads the plugin from its shadow-copy directory.

### Build and Install on Linux

`BuildYourPlugins.sh` builds the library and plugin and installs them below
`$XDG_DATA_HOME/ETS2LA`, or `$HOME/.local/share/ETS2LA` when `XDG_DATA_HOME` is unset:

```bash
./BuildYourPlugins.sh
```

For a production-style local install, copy the resulting DLLs to the matching catalogue
directories under `dist/` or directly to your ETS2LA data directory. The GitHub Actions workflow
packages the same files automatically and does not require `dist/` to be committed.

### Build and Install on Windows

Run the batch script from a Developer PowerShell or a terminal with the .NET SDK available:

```bat
BuildYourPlugins.bat
```

The script installs catalogue-style DLLs under `%LOCALAPPDATA%\ETS2LA`.

## Installing the Built Plugin

There are two supported local installation layouts. Use one layout at a time to avoid loading the
same plugin twice.

### Manual Top-Level Scan

The manual scanner looks only for DLLs directly inside `Plugins/` and `Libraries/`. After creating
the ignored local `dist/` package described above, install its files into an ETS2LA data root:

Linux:

```bash
./InstallPlugins.sh "$HOME/.local/share/ETS2LA"
```

Windows PowerShell:

```powershell
./InstallPlugins.ps1 -Root "C:\path\to\ETS2LA\current"
```

Windows batch:

```bat
InstallPlugins.bat "C:\path\to\ETS2LA\current"
```

The install scripts copy the DLLs to:

```text
<ETS2LA root>/Plugins/srlily.i18n.dll
<ETS2LA root>/Libraries/srlily.i18n.library.dll
```

They also remove old manifest entries for these IDs to prevent duplicate loading. Restart ETS2LA
after installation.

### Catalogue-Style Manifest

To use the nested layout in `dist/` or the ETS2LA data directory, register both DLLs:

```bash
./RegisterPlugins.sh "$HOME/.local/share/ETS2LA"
```

The manifest entries are:

```text
Libraries/srlily.i18n.library/srlily.i18n.library.dll
Plugins/srlily.i18n/srlily.i18n.dll
```

Do not use `RegisterPlugins` and `InstallPlugins` simultaneously. The first uses the manifest and
the second uses the top-level manual scan.

## Continuous Integration and Releases

The workflow at `.github/workflows/build-release.yml` runs on every push to `main` and can also be
started manually from the GitHub Actions page. It performs the following steps:

1. Checks out ETS2LA and all submodules.
2. Installs the .NET 10 SDK.
3. Restores and builds the localization plugin in Release configuration.
4. Runs translation and plugin-loading tests.
5. Packages the library and plugin DLLs into a versioned ZIP archive.
6. Creates or updates a GitHub Release and its version tag.

The release version is read from `VERSION`. The current version is `1.1.2`, so the workflow uses
the tag `v1.1.2` and the archive name `ets2la-i18n-v1.1.2.zip`. `RELEASE_NOTES.md` is used as the
release body.

To publish a new release:

1. Update `VERSION` to the next semantic version, for example `1.1.3`.
2. Update `RELEASE_NOTES.md` with the changes for that version.
3. Update the plugin `Version` in `Plugins/Localization/Program.cs` if the displayed plugin
   version should change.
4. Commit and push to `main`.

The workflow creates the corresponding `v<version>` tag using the repository's GitHub Actions
token. Reusing an existing version updates its release asset instead of creating a second tag.
`dist/` remains ignored; built DLLs are available from the GitHub Release asset.

## Using the Plugin

1. Start ETS2LA and open the Plugin Manager.
2. Enable the `Localization` plugin. Its library dependency,
   `srlily.i18n.library`, must be available first.
3. Open the injected language selector in the sidebar or open the `Localization` tab under
   Settings.
4. Select `Chinese (Simplified) (简体中文)`.
5. The interface and active bottom-right notifications are retranslated immediately.

The selected language is saved in ETS2LA's configuration directory as
`LocalizationSettings.json`. The settings currently include:

- `LanguageCode`: selected BCP-47 language code.
- `TranslateWindowTitles`: whether window titles are translated.
- `TranslateAccessibilityNames`: whether accessibility names are translated.
- `ShowSidebarSelector`: whether the sidebar selector is visible.

## Adding a Language

Create a new file under `Libraries/LocalizationLibrary/Translations/`. The file must contain a
BCP-47 code, a native language name, an English language name, and a `strings` object:

```json
{
  "code": "de-DE",
  "name": "Deutsch",
  "englishName": "German",
  "strings": {
    "Dashboard": "Armaturenbrett",
    "Settings": "Einstellungen"
  }
}
```

Language files are embedded automatically by `LocalizationLibrary.csproj`; no registration code
is required. Rebuild the library and plugin after adding or changing a language pack.

Translation keys must match the English source text exactly, including:

- Capitalization.
- Leading or trailing spaces.
- Punctuation and newlines.
- Placeholder positions such as `{0}` and `{1}`.

For example, a dynamic notification with the source text
`Download progress: {0}%` should use a translation key with the same `{0}` placeholder:

```json
"Download progress: {0}%": "下载进度：{0}%"
```

Strings without a matching key remain unchanged in English. This is intentional and allows a
language pack to be expanded incrementally.

## Plugin Icon

The plugin icon is copied from:

```text
ETS2LA/Assets/Installer/favicon.ico
```

It is included in the plugin assembly as `Assets/favicon.ico` and exposed through:

```text
avares://srlily.i18n/Assets/favicon.ico
```

This avoids relying on the original repository path at runtime and keeps the icon available when
ETS2LA shadow-copies the plugin before loading it.

## Tests and Verification

Run the translation regression checks:

```bash
dotnet run --project Tools/TranslationTest/TranslationTest.csproj
```

The test covers exact strings, case-sensitive `on`/`off` variants, dynamic placeholders, and the
game-connection notification.

Run the headless Settings-page injection check after building the plugin:

```bash
dotnet run --project Tools/InjectTest/InjectTest.csproj
```

Run the plugin loading check with an ETS2LA data root and built DLLs:

```bash
dotnet run --project Tools/LoadTest/LoadTest.csproj -- "$HOME/.local/share/ETS2LA"
```

A successful plugin build should report `Build succeeded` with zero errors. Existing warnings in
the ETS2LA submodule do not prevent the localization plugin from being built, but new warnings or
errors in the localization projects should be investigated.

## Troubleshooting

### The interface is still in English

- Confirm that both `srlily.i18n.library.dll` and `srlily.i18n.dll` are installed.
- Confirm that the plugin is enabled in the Plugin Manager.
- Confirm that `zh-CN` is selected in the language selector.
- Restart ETS2LA after replacing DLLs; loaded assemblies are not replaced in the current process.

### The plugin does not appear

- Check ETS2LA's `ets2la.log` for `Loaded plugin` or `Failed to load plugin` entries.
- Ensure the library DLL is available before enabling the main plugin.
- Do not mix top-level manual scanning with manifest registration.
- Verify that the manifest path points to files that still exist.

### Notifications remain in English

Notifications are translated only while the localization plugin is enabled. Fixed ETS2LA
notifications require an exact translation key; messages generated by third-party plugins or
remote servers need their own entries in the selected language pack.

### The icon is missing

- Rebuild the plugin after changing the icon or `Localization.csproj`.
- Ensure the loaded DLL is the newly built DLL, not an older copy under `dist` or the ETS2LA data
  directory.
- Restart ETS2LA so its plugin shadow-copy directory is recreated.

## Development Notes

- The ETS2LA source is included as a git submodule and should not be modified for normal
  localization work.
- The localization plugin keeps original source values so changing languages does not permanently
  replace ETS2LA's English values.
- Notification data objects remain unchanged; only the active UI notification copy is translated.
- The plugin uses a low-cost periodic pass because some ETS2LA views are generated dynamically.
- New language packs should include tests for representative static and placeholder-based strings.

## Contributing

Contributions are welcome for new language packs, improved translations, compatibility fixes, and
test coverage. Keep translation keys identical to the English source text and run the translation
test plus a Release build before submitting changes.

## License

This project is released under the MIT License. See [LICENSE](LICENSE).
