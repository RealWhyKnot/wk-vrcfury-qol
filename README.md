# VRCFury QoL

[![License: GPLv3](https://img.shields.io/badge/license-GPLv3-blue.svg)](LICENSE)
[![VRCFury](https://img.shields.io/badge/VRCFury-1.1303.x-7e57c2.svg)](https://vrcfury.com/)
[![Unity](https://img.shields.io/badge/Unity-2022.3-000000.svg?logo=unity)](https://unity.com/)

Quality-of-life editor tools for [VRCFury](https://vrcfury.com/). They show up where you're already working: the right-click menu on a page, buttons on flipbook rows, a banner on each Toggle, and a window where you drop in two objects to swap references.

## Tools

- Move all VRCFury components between GameObjects in one Undo step. "Move whole components" moves each component as it is. "Merge into one component" puts every feature on a single carrier object. Right-click a GameObject -> WhyKnot -> wk-vrcfury-qol -> Move all VRCFury components to...
- Replace references in selection. Pick GameObjects and the window lists every distinct Object their VRCFury components reference, one row per value with a count of where it's used. Drag a replacement onto a row and click Apply, and every occurrence is swapped in one Undo step. *Include children* is a per-selection toggle. Open it from Tools -> WhyKnot -> wk-vrcfury-qol -> Replace References..., or right-click a selection -> WhyKnot -> wk-vrcfury-qol -> Replace references in selection...
- Missing-reference warning. On editor startup and after every assembly reload, it scans the open scene's VRCFury components for `Object` references whose target was deleted. If it finds any, a non-modal window lists each one with a Ping button. Dismiss it and it stays dismissed until the next reload. Run it again any time from Tools -> WhyKnot -> wk-vrcfury-qol -> Check for missing references...
- Global Parameter on every Toggle. Each Toggle gets `useGlobalParam = true` and `globalParam = MenuPath`, kept in sync, and a green banner on the Toggle inspector shows it. That stops VRCFury from renaming the parameter when it regenerates the avatar. Opt a toggle out from the banner button or the right-click menu.
- Preview toggles and flipbooks. The Toggle banner and each flipbook page row have a `Preview` button. It makes a temporary copy of the avatar in place that can't be saved, applies the toggle or page to the copy, and hides the source avatar in Scene view. The Scene camera doesn't move. While the copy exists the button turns into a red `Stop Previewing`, which destroys the copy and shows the source avatar again.
- Migrate child toggles into a Flipbook. Right-click a Flipbook Builder action. It finds the non-flipbook VRCFury Toggles on the same GameObject and its descendants, adds each one to the flipbook as a page, and deletes the original components. A confirmation dialog lists what will happen first.
- Duplicate a flipbook page below the current one with the `Duplicate` button next to each `Page #N` label, or to the end from the right-click menu.
- Duplicate one state action in place with `Duplicate item`, or use `Copy to page` on a flipbook page action to append just that BlendShape, Material Swap or other action to another page.
- Hot reload and logs. The package watches its own source files (not the rest of the project) and runs `AssetDatabase.Refresh()` even when Unity is unfocused. Session logs go to `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol/` and hot-reload sessions to `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol.Editor.hotreload/`. Open the logs from Window -> WhyKnot -> VRCFury QoL -> Logs, and check the watcher at Window -> WhyKnot -> VRCFury QoL -> Hot Reload Status.

## Installation

VCC is the easiest way in. The manual install is for projects VCC doesn't manage.

### VCC (recommended)

Add the WhyKnot VPM listing to the [VRChat Creator Companion](https://creators.vrchat.com/) and the package appears under Manage Project -> Add Package.

1. Open <https://vpm.whyknot.dev/>. It redirects to a `vcc://` URL and VCC opens with the listing filled in. Click "I Understand, Add Repository".
2. If VCC doesn't open, go to Settings -> Packages -> Add Repository in VCC, paste `https://vpm.whyknot.dev/index.json` and click "I Understand, Add Repository".
3. Open a project, click Manage Project, find VRCFury QoL and click Add.

Unity compiles the package into a `dev.whyknot.wk-vrcfury-qol.Editor` assembly. It's all under `Editor/` and none of it ends up in runtime builds. It depends on `com.vrcfury.vrcfury` 1.1300.0 or newer, and VCC won't install it without VRCFury.

### Manual install

Download `dev.whyknot.wk-vrcfury-qol-X.Y.Z.zip` from [the latest release](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/latest) and unzip it into `Packages/dev.whyknot.wk-vrcfury-qol/`, so that `Packages/dev.whyknot.wk-vrcfury-qol/package.json` exists. Unity's Package Manager picks it up on the next refresh. VRCFury has to be in the project already.

I test it against VRCFury 1.1303.x on Unity 2022.3.

## Adding your own tool

A tool is a small `[InitializeOnLoad]` static class that registers itself with `VrcfQol`. Use the framework's typed helpers instead of reading the reflection cache yourself.

## Back up first

These tools make real changes to your scene. A migration deletes the original VRCFury components, reference replacement works in bulk, and `useGlobalParam` is forced on by default. Commit your project or duplicate the avatar before you start, and try a tool on one small group before you point it at anything large.

## Contributing

Bug reports, feature requests and pull requests are welcome. [CONTRIBUTING.md](CONTRIBUTING.md) has the dev setup and PR conventions.

## License

GNU General Public License v3.0 or later. See [LICENSE](LICENSE).
