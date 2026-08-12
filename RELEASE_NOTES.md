# ETS2LA Localization v1.1.1 / ETS2LA 本地化 v1.1.1

## Highlights / 主要更新

- Added Simplified Chinese translations for `Match Game` and `Overlay Interaction`.
  新增 `Match Game` 和 `Overlay Interaction` 的简体中文翻译。
- Updated the plugin compatibility target to the latest official ETS2LA release, `v3.4.37`.
  插件支持版本已更新为 ETS2LA 官方最新版本 `v3.4.37`。
- Removed generated `dist/` binaries from the Git repository.
  移除仓库中的生成目录 `dist/`，避免提交构建产物。
- Kept build output in GitHub Actions and attached the packaged DLLs to the GitHub Release.
  构建产物改由 GitHub Actions 自动生成，并作为 Release 附件发布。
- Improved the release workflow and version-driven tag generation.
  完善自动发布流程，并根据 `VERSION` 自动生成版本标签。

## Compatibility / 兼容性

- ETS2LA: `v3.4.37`
- Plugin version: `1.1.1`
- Language pack: Simplified Chinese (`zh-CN`)

## Package Contents / 发布包内容

- `srlily.i18n.library.dll`
- `srlily.i18n.dll`
- `README.md`
- `LICENSE`

Untranslated strings continue to fall back to English.
未完成翻译的文本仍会自动回退为英文。
