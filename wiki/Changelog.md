# Changelog

All notable changes to wk-vrcfury-qol. Format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [Semantic Versioning](https://semver.org/).

The most recent release is at the top.

<!-- Entries under "## Unreleased" are appended automatically by the changelog-append GitHub
     workflow on every push to main, then promoted to the versioned section by release.yml when
     a tag is cut. Don't hand-edit Unreleased -- your edits will be overwritten on the next push.
     To override an entry, amend the commit subject before merge. -->

## Unreleased

_No notable changes since the last release._

---

## [v1.2.3](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.2.3) -- 2026-05-28

### Added
- **ui:** Fit editor tool windows (84932da)
- **ui:** Add WhyKnot logo chrome (6b18d27)
- **ui:** Add responsive branded editor chrome (a068d90)

### Fixed
- **ui:** Remove animated title chrome (891f16e)

---

## [v1.2.2](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.2.2) -- 2026-05-27

### Fixed
- **hot-reload:** Watch only package root (2725f6b)

---

## [v1.2.1](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.2.1) -- 2026-05-25

### Fixed
- **Hot reload status menu collision**: `WkHotReloadStatus.cs` carried a `[MenuItem("Window/WhyKnot/Hot Reload Status")]` attribute that Unity refused on a second registration ("a menu item with the same name already exists") whenever both vrcfury-qol and avatar-qol were installed in the same project -- their two synced copies of the file both tried to register the identical menu path. The attribute moves out of the synced source file; this package now wires its own `Window/WhyKnot/VRCFury QoL/Hot Reload Status` entry from non-synced code so the two packages stop racing for the path.

---

## [v1.2.0](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.2.0) -- 2026-05-25

### Added
- **Window / WhyKnot / VRCFury QoL / Logs**: per-package session-log viewer with level filter chips (Debug / Info / Warning / Error), free-text search, live tail via `FileSystemWatcher`, and Open in Explorer buttons. Tabbed by every WkLogger registered with this package's registry.
- **Project Settings / WhyKnot / VRCFury QoL**: Console mirror toggles per registered logger, default theme override (WhyKnot / VRCFury), hot-reload watcher enable toggle that takes effect on next Editor startup, and an Optional integrations status panel showing which versionDefine symbols (WK_NDMF, WK_VRC_SDK_AVATARS) are active.
- **Window / WhyKnot / Hot Reload Status**: refresh counter, last compile result, recent file events, Open Log button, watcher-enabled toggle.

### Changed
- Bundled `Editor/Internal/` refreshed from the wk-core 1.2.0 surface. The 1.2.0 source ships an NDMF-first build pipeline (`WkAvatarPass<TSession>` + `WkAvatarPipeline.Register` routing through NDMF when installed and through four phase-bucket `IVRCSDKPreprocessAvatarCallback` implementations as a fallback), a three-tier generated-asset scope (Temporary / Session / Persistent), an in-house animator-builder (`WkAac.For` returning a fluent `IWkAnimatorBuilder`; no third-party AAC dependency), a `WkAvatarPreviewSession` with original<->proxy mapping + force-reset + crash recovery, UI Toolkit primitive set + USS theme stylesheets, a buffered `WkLogger` writer with full lifecycle envelope, and the hot-reload watcher now covers `.asmdef` + `.asmref` changes with `InternalBufferOverflowException` recovery.
- Existing tool code keeps the prior API surface; subsequent feature work picks up the new helpers incrementally.

---

## [v1.2.0-beta.2](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.2.0-beta.2) -- 2026-05-25

### Breaking
- Package ID renamed from `dev.whyknot.vrcfury-qol` to `dev.whyknot.wk-vrcfury-qol`. GitHub repo renamed from `RealWhyKnot/vrcfury-qol` to `RealWhyKnot/wk-vrcfury-qol`. VCC has no in-place upgrade path between different package IDs -- remove the old package and add the new one. Menu entries move from `Tools/WhyKnot/vrcfury-qol/...` to `Tools/WhyKnot/wk-vrcfury-qol/...` (same for `GameObject/WhyKnot/wk-vrcfury-qol/...` right-click entries).

### License
- `LICENSE` now carries a GPL-3.0 Section 7 author-reservation clause: the copyright holder (WhyKnot) reserves the right to incorporate this software into closed-source works distributed by the copyright holder, in particular VRChat avatar uploads. Recipients' GPL rights and obligations are unchanged.

---

## [v1.2.0-beta.1](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.2.0-beta.1) -- 2026-05-25

### Changed
- Bundled `Editor/Internal/` refreshed from the wk-core 1.2.0 source. Picks up an NDMF-`ErrorReport`-shaped scope stack on top of `WkLogger` (`WkLogContext`, `BeginTask`, `InfoBlock` / `WarningBlock` / `ErrorBlock`); new utility helpers (`MeshUtility`, `BlendShapeUtility`, `FolderUtility`, `UndoUtility`); new reflection helpers (`WkReflection`, `WkReflectionCache`, `WkGlobalId`, `WkJsonClone`); an `EditorApplication.update` ticker (`WkEditorTicker`) and a typed `EditorPrefs` wrapper (`WkEditorPrefs` + `WkSessionState`); a `WkToolWindow` / `WkInspectorEditor` / `WkMenuPaths` scaffolding tier; thirteen new `WkStyles` primitives (`SubtleDivider`, `Foldout`, `TwoColumn`, `SearchField`, `TabBar`, `ProgressBar`, `ObjectFieldRow`, `DangerButtonInline`, `SecondaryButtonInline`, `StatusBanner`, `Checker`, `RectBorder`, `TitleBar`) and four new `GUIStyle`s (`Caption`, `Code`, `TitleBarStyle`, `RowAlt`); a broadened theme palette (`DividerSubtle`, `BackgroundEmphasis`, `ButtonHover`) and a `NoticeKind.Danger` value; and theme-routed `EditorElementWalker` chrome that reads from `WkStyles.Current` instead of baking the VRCFury palette in literals. No user-visible behaviour change in this version -- the existing tool code still uses the prior API surface. Future feature work picks up these helpers.
- Split the four large editor files into per-concern partial classes (1ce59bd)

---

## [v1.1.0](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.1.0) -- 2026-05-25

### Added
- **inspector:** Per-toggle Preview banner, inline action tools, multi-toggle action resolver (24e3922)
- **logging:** Every diagnostic line in this package now routes through `VrcfQolLogger.Instance` (the package's registered `WkLogger`). Sessions are written to `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol/session-<timestamp>.log`, capped at 3 retained sessions per package. Each line carries a level tag, source file:line, calling method, and message. Info, Warning, and Error mirror to the Unity Console as before; Debug stays file-only. The session file is project-independent so a bug report can point at the same path regardless of which Unity project surfaced it.
- **theming:** Tool window OnGUI bodies (`ReplaceReferencesWindow`, `MissingReferenceWindow`, `MoveVrcfComponentsTool`) open `using (WkStyles.Scope(WkTheme.VRCFury))` so the palette emitted by `WkStyles` matches VRCFury's dark-gray row chrome and warm accents. The inspector overlay (UIElements) already painted these colors via its hardcoded chrome helpers; the IMGUI tool windows now match.
- **toggle:** Auto-rename globalParam on menu-path collision (1fee7c2)

### Changed
- **deps:** Bumped `dev.whyknot.core` dependency to `>=1.1.0` so the new theming system and `WkLogger` are guaranteed available.
- **deps:** Add `dev.whyknot.core` (>=1.0.0) as a hard `vpmDependency`. VCC auto-installs the shared utility package alongside wk-vrcfury-qol. Internal-only refactor that consolidates the hot-reload watcher, the `GetGameObjectPath` helper, and the six UIElements / banner-chrome helpers behind a single shared assembly; no user-visible behaviour change. The compile log moves from `Logs/VrcfQolHotReload.log` to `Logs/WkCore.log`.
- Bump actions/checkout from 4 to 6 (#1) (16c696c)

### Fixed
- **overlay+ci:** De-dup Toggle banner, autoload logger version (1.1.0-beta.5) (7fd7811)
- **logger:** Qualify PackageInfo to avoid ambiguity with UnityEditor.PackageInfo (4ff15bc)

---

## [1.0.1](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.0.1) -- 2026-05-07

### Changed
- License: switched from MIT to GPL-3.0-or-later. Same set of users can use, modify, and redistribute; downstream forks now propagate the GPL terms instead of MIT's permissive ones.
- Repo infra: auto-maintained `CHANGELOG.md` via verified bot commits on every push to `main` (conventional-commit subjects bucket into Added/Changed/Fixed). Branch protection ruleset on `main` now requires signed commits.

---

## [1.0.0](https://github.com/RealWhyKnot/wk-vrcfury-qol/releases/tag/v1.0.0) -- 2026-05-03

First release as a VRChat Package Manager (VPM) package, installable via the Creator Companion at `https://vpm.whyknot.dev/index.json`.

### Added
- VPM package metadata (`package.json`) declaring `dev.whyknot.wk-vrcfury-qol` with a hard `vpmDependencies` on `com.vrcfury.vrcfury` (>= 1.1300.0). VCC will refuse to install this package without VRCFury present.
- Editor assembly definition (`Editor/dev.whyknot.wk-vrcfury-qol.Editor.asmdef`) scoping the tools to the Editor platform. The asmdef intentionally does **not** declare an assembly reference to `VRCFury` because all VRCFury access in this package goes through runtime reflection (`VrcfQol.Reflection.VRCFuryType`) -- that lets the package compile against future VRCFury versions whose internal layout has shifted, and surface a friendly "VRCFury runtime assembly not found" message rather than a hard compile error.

### Changed
- **Breaking for loose-script users.** Prior to 1.0.0 the recommended install was to drop the `Editor/` folder anywhere under your `Assets/` tree; Unity compiled the scripts into the project's default editor assembly. With the new asmdef, code now compiles into a dedicated `dev.whyknot.wk-vrcfury-qol.Editor` assembly. If you were previously importing as loose scripts and you upgrade by adding the asmdef in place, *internal* type references inside this package keep working, but any **external** code in your project that referenced these tools' types (e.g. `WhyKnot.VrcfQol.VrcfQol` from your own scripts) will need its asmdef to add `dev.whyknot.wk-vrcfury-qol.Editor` to its `references`.
- Recommended migration: remove the old loose-script copy from `Assets/` and reinstall via VCC. Unity asset GUIDs are regenerated on import; nothing inside this package references its own files by GUID, so no project-side cleanup is required beyond removing the duplicate.
