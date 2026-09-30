# VRCFury QoL

[![License: GPLv3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)
[![VRCFury](https://img.shields.io/badge/VRCFury-1.1303.x-7e57c2.svg)](https://vrcfury.com/)
[![Unity](https://img.shields.io/badge/Unity-2022.3-000000.svg?logo=unity)](https://unity.com/)

Quality-of-life Editor tools for [VRCFury](https://vrcfury.com/). The tools show up where you're already working: right-click a page, click a button on a flipbook row, see a banner on a Toggle, drop in two objects to swap references. No separate window to dig through.

Adding a new tool is usually one small file with an `[InitializeOnLoad]` registration. See [Adding a Tool](wiki/Adding-a-Tool.md).

## Tools

- Move all VRCFury components between GameObjects in one Undo step. "Move whole components" keeps the serialization shape; "Merge into one component" puts every feature on a single carrier object. Right-click a GameObject -> WhyKnot -> wk-vrcfury-qol -> Move all VRCFury components to...
- Replace references in selection. Pick GameObjects and the window lists every distinct Object their VRCFury components reference, one row per value with a count of where it's used. Drag a replacement onto a row and click Apply, and every occurrence is swapped in one Undo step. *Include children* is a per-selection toggle. Open it from Tools -> WhyKnot -> wk-vrcfury-qol -> Replace References..., or right-click a selection -> WhyKnot -> wk-vrcfury-qol -> Replace references in selection...
- Missing-reference warning. On editor startup and after every assembly reload, it scans the open scene's VRCFury components for `Object` references whose target was deleted, and opens a non-modal window listing each with a Ping button. Dismiss it and it stays dismissed until the next reload. Re-run it any time from Tools -> WhyKnot -> wk-vrcfury-qol -> Check for missing references...
- Auto-synced Global Parameter on every Toggle. A green banner on the Toggle inspector confirms `useGlobalParam = true` and `globalParam = MenuPath` are kept in sync, so VRCFury can't rename parameters when it regenerates the avatar. Opt out per toggle from the banner button or the right-click menu.
- Preview toggles and flipbooks. The Toggle banner and each flipbook page row have a `Preview` button. It makes a temporary, non-saveable copy of the avatar in place, applies the toggle or page to the copy, and hides the source avatar in Scene view. The Scene camera doesn't move. While the copy exists the button becomes a red `Stop Previewing` that destroys it and restores visibility.
- Migrate child toggles into a Flipbook. Right-click a Flipbook Builder action. It finds non-flipbook VRCFury Toggles on the same GameObject and its descendants, folds each into the flipbook as a page, and deletes the source components. A confirmation dialog lists what will happen.
- Duplicate a flipbook page below the current one with the inline `Duplicate` button next to each `Page #N` label, or to the end from the right-click menu.
- Duplicate one state action in place with `Duplicate item`, or use `Copy to page` on a flipbook page action to append just that BlendShape, Material Swap or other action to another page.
- Hot reload and logs. Watches this package's own source files and runs `AssetDatabase.Refresh()` even when Unity is unfocused. It doesn't watch the rest of the project. Session logs go to `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol/`, and hot-reload sessions to `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol.Editor.hotreload/`. Open the logs from Window -> WhyKnot -> VRCFury QoL -> Logs, and check the watcher at Window -> WhyKnot -> VRCFury QoL -> Hot Reload Status.

Each tool is covered in more detail in [Tools Overview](wiki/Tools-Overview.md).

## Installation

### VCC (recommended)

Add the WhyKnot VPM listing to the [VRChat Creator Companion](https://creators.vrchat.com/) and the package appears under **Manage Project -> Add Package**.

1. Open <https://vpm.whyknot.dev/>. It redirects to a `vcc://` URL and VCC opens with the listing filled in. Click **I Understand, Add Repository**.
2. If that doesn't work, go to **Settings -> Packages -> Add Repository** in VCC, paste `https://vpm.whyknot.dev/index.json`, and click **I Understand, Add Repository**.
3. Open a project, click **Manage Project**, find **VRCFury QoL** and hit **Add**.

Unity compiles the package into a `dev.whyknot.wk-vrcfury-qol.Editor` assembly. It's `Editor/` only, so nothing ends up in runtime builds. It depends on `com.vrcfury.vrcfury` (>= 1.1300.0), and VCC won't install it without VRCFury.

### Manual install

For Unity projects not managed by VCC, download `dev.whyknot.wk-vrcfury-qol-X.Y.Z.zip` from [the latest release](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/latest) and unzip it into `Packages/dev.whyknot.wk-vrcfury-qol/`, so that `Packages/dev.whyknot.wk-vrcfury-qol/package.json` exists. Unity's Package Manager picks it up on the next refresh. VRCFury must already be in the project.

Tested against VRCFury **1.1303.x** on Unity **2022.3**. Per-clone setup, like the hot-reload bootstrap, is in [Installation](wiki/Installation.md).

## Adding your own tool

A tool is a small `[InitializeOnLoad]` static class that registers itself with `VrcfQol`. The framework has typed helpers, so you don't walk the reflection cache yourself. [Adding a Tool](wiki/Adding-a-Tool.md) has examples for every `Register*` method.

## Docs

- [Tools Overview](wiki/Tools-Overview.md)
- [Architecture](wiki/Architecture.md): how the framework hooks VRCFury through reflection and a UI overlay
- [Adding a Tool](wiki/Adding-a-Tool.md)
- [Troubleshooting](wiki/Troubleshooting.md)

## Back up first

These tools make real changes to your scene: deleting source VRCFury components during a migration, replacing object references in bulk, forcing `useGlobalParam` on by default. Commit your project or duplicate the avatar before you start, and try a tool on one small group before pointing it at anything large.

## Contributing

Bug reports, feature requests and pull requests are welcome. [CONTRIBUTING.md](CONTRIBUTING.md) covers the dev loop and PR conventions.

## License

GNU General Public License v3.0 or later. See [LICENSE](LICENSE).
