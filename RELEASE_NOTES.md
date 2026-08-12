# ETS2LA Localization v1.1.2 / ETS2LA 本地化 v1.1.2

## Fixes / 修复

- Restores translated UI text when the localization plugin is disabled.
  关闭本地化插件后会恢复界面原始文本，移除相关通知翻译。
- Removes the injected sidebar language selector and Settings Localization tab on disable.
  插件关闭时会移除侧边栏语言选择器和设置页中的本地化选项卡。
- Keeps the plugin instance available across disable and re-enable cycles.
  修复插件重新启用后本地化设置页面无法打开的问题。
- Reattaches the language selector and Settings Localization tab when the plugin is enabled again.
  插件重新启用后会重新挂载语言选择器和本地化设置页面。
- Clears the injected tab's selected state when another Settings page or main page is selected.
  切换到其他设置页面或主页面时，会取消本地化选项卡的选中状态。

## Compatibility / 兼容性

- ETS2LA: `v3.4.37`
- Plugin version: `1.1.2`
- Language pack: Simplified Chinese (`zh-CN`)

## Package Contents / 发布包内容

- `srlily.i18n.library.dll`
- `srlily.i18n.dll`
- `README.md`
- `LICENSE`

Untranslated strings continue to fall back to English.
未完成翻译的文本仍会自动回退为英文。
