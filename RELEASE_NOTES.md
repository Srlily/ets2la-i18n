# ETS2LA Localization v1.1.0

## Highlights

- Added the runtime localization plugin and its companion language-pack library.
- Added Simplified Chinese (`zh-CN`) translations for the ETS2LA interface.
- Added translation support for dynamic Avalonia controls, window titles, tooltips, and
  accessibility names.
- Added translation support for ETS2LA's bottom-right Growl notifications.
- Added translations for `Current Speed`, `Speed Limit`, `on`, `off`, and the game connection
  error notification.
- Added an embedded plugin icon based on ETS2LA's `favicon.ico`.
- Added language selection in the sidebar and Settings view.
- Added translation, UI injection, and plugin loading test tools.

## Build

The release package is built by GitHub Actions from the `main` branch. It contains:

- `srlily.i18n.library.dll`
- `srlily.i18n.dll`

Untranslated strings intentionally fall back to English.
