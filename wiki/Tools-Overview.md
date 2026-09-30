# Tools Overview

Every shipping tool, where to find it, and what it does. All tools wrap destructive changes in a single Undo group, so `Ctrl+Z` reverts them.

## Move VRCFury Components

Right-click a GameObject in the hierarchy -> *WhyKnot -> wk-vrcfury-qol -> Move all VRCFury components to...*. Also available under *GameObject -> WhyKnot -> wk-vrcfury-qol -> Move all VRCFury components to...* in the menu bar.

Moves every VRCFury MonoBehaviour from a source GameObject to a destination GameObject in one Undo step. Two modes:

- Move whole components (default): each VRCFury MonoBehaviour is recreated on the destination as its own component. Preserves serialization shape exactly via `ComponentUtility.CopyComponent` + `PasteComponentAsNew` (so `[SerializeReference]` graphs round-trip correctly through Unity's own pipeline).
- Merge into one component: all features get appended into a single VRCFury component on the destination, using the legacy `config.features` list. Useful for authoring everything on one carrier object. Greyed out if the installed VRCFury version doesn't expose `VRCFuryConfig.features`.

The dialog enforces same-scene-as-source and disables itself on identical source/destination.

## Replace References

*Tools -> WhyKnot -> wk-vrcfury-qol -> Replace References...*, or right-click a hierarchy selection -> *WhyKnot -> wk-vrcfury-qol -> Replace references in selection...* (which pre-fills the search list and scans immediately).

Lists every distinct `Object` referenced by any VRCFury component on the selected hierarchy: one row per unique referenced object, with a count of how many places it's used. Drag a replacement onto the rows you want to swap and click Apply.

For each VRCFury component on the search roots, the tool builds a `SerializedObject` and iterates with `SerializedProperty.NextVisible(true)`, which descends into `[SerializeReference]` polymorphic graphs (Toggle, ArmatureLink, FullController) on its own. Every `ObjectReference` property with a non-null value is recorded; rows are then grouped by the underlying object so duplicates collapse into a single row.

Each entry in the search list has an *Include children* toggle. On (the default), the scan recurses into the GameObject's descendants. Off, it checks only components on that GameObject, which helps when two avatars share a parent or you want to limit the scan to one component.

In the window:
- Drop a replacement into any row's *Replace* field. Rows without a replacement are left alone.
- Expand the *Locations* foldout on a multi-reference row to see exactly where it's used (each entry has a *Ping* button).
- *Only queued* filters the list to just the rows you've staged.
- *Refresh* re-runs the scan (the references you applied should now show their new values).

Apply is grouped into one Undo step. If a site's current value drifted between scan and apply (e.g. another tool changed it in the meantime), that site is skipped and logged as "stale"; the rest still apply.

## Missing Reference Warning

Auto-pops on editor startup, scene-open, and prefab-stage-open. Manual re-check via *Tools -> WhyKnot -> wk-vrcfury-qol -> Check for missing references...*.

Walks every VRCFury component in the open scene(s) and prefab stage looking for `Object` reference properties whose serialized instance ID is non-zero but whose runtime value resolved to `null`, which means a deleted asset or scene object that the VRCFury data still expects to find. If anything matches, a non-modal window opens listing every offender with its GameObject path, feature type, property path, and a Ping button.

Closing the auto-popped window sets a session-scoped dismiss flag, so the warning won't pop again until the next assembly reload (script recompile, *Reload Domain*, or restart). It's reload-scoped on purpose. Missing refs are usually transient (notice, fix, recompile re-arms the scanner), and a persistent "don't ask again" would let real problems linger.

The manual menu entry always opens the window, and says so when the scan is clean.

## Auto Global Parameter

Always-on background sync; visible as a colored banner at the top of every VRCFury Toggle inspector. Right-click anywhere on a Toggle for the manual control menu.

Every ~0.5 s, for every VRCFury Toggle in the open scene / prefab stage, this tool enforces:

- `Use Global Parameter` = true
- `Global Parameter` = the toggle's Menu Path

The Toggle inspector shows a green banner confirming auto-sync is active. The banner has an inline *Turn off* button to opt out on a per-toggle basis (or right-click -> *WhyKnot -> wk-vrcfury-qol -> Disable global parameter sync*). Opt-outs are stored in `EditorPrefs` keyed by the component's `GlobalObjectId`, so the preference follows the scene/prefab on this machine but doesn't travel cross-machine.

Why: when VRCFury regenerates your avatar it can rename the internal parameter it picked for each toggle. Anything you've pinned to those names (animator constraints, external OSC clients, VRChat's per-avatar parameter memory) gets silently wiped. Setting `Global Parameter = MenuPath` makes VRCFury keep the same name, so those customisations survive a rebuild.

The sync doesn't wrap each tick in Undo, which would flood the Undo stack. It calls `SetDirty` only when something changes. Direct user edits to either field still undo normally.

## Migrate Child Toggles into Flipbook

Right-click a Flipbook Builder action -> *WhyKnot -> wk-vrcfury-qol -> Migrate child toggles as pages*.

Scans the Flipbook's GameObject + descendants for non-flipbook VRCFury Toggles. Folds each into the flipbook as a new page (reusing the source's `State` directly, with no clone, so actions stay byte-identical). Source VRCFury components are deleted afterward. A confirmation dialog shows the exact list of toggles that will be migrated and deleted.

Single Undo step reverts everything.

## Flipbook Page Operations

Right-click a `Page #N` row, or use the inline buttons next to every `Page #N` label.

Four variants:
- Preview: creates a temporary avatar copy with that page applied, hides the source avatar in Scene view, and turns into a red *Stop Previewing* button so you can remove the copy when done. It does not move or reframe the Scene camera.
- Duplicate page below (inline `Duplicate`): deep-clones the page and inserts the copy at index + 1, shifting later pages down.
- Insert empty page below (inline `Insert blank`): inserts an empty page at index + 1, like VRCFury's own "+" button but positioned where you want it.
- Duplicate page to end: deep-clones the page and appends the copy at the end of the flipbook. It stays on the right-click menu for when you want the new page out of the way.

Page deep-clone uses a `JsonUtility.ToJson` / `FromJson` round-trip on every action, so the new page's state is fully independent of the original.

The inline buttons are best-effort UI injection: if a future VRCFury version changes the page layout, the buttons silently disappear and the right-click menu still works.

## Duplicate State Action

Click the inline *Duplicate item* button on a state action, or right-click any state action inside a VRCFury Toggle (or inside a flipbook page's state) -> *WhyKnot -> wk-vrcfury-qol -> Duplicate this action*.

Deep-clones a single state action (ObjectToggleAction, BlendShape, MaterialPropertyAction, AnimationClipAction, etc.) and inserts the copy at index + 1 in the same actions list. Works at any nesting depth. The path resolver walks the SerializedProperty path with reflection and recognises `.actions.Array.data[N]` boundaries, so it handles top-level Toggle actions and flipbook-page actions equally.

When the action lives inside a flipbook page, an inline *Copy to page* button appears next to *Duplicate item*. It opens a page picker and appends a clone of just that one action to the selected page, handy for copying one BlendShape or Material Swap without duplicating the whole page.

## Preview Toggle / Flipbook

Click *Preview* in the Toggle banner, click *Preview* next to a flipbook `Page #N`, or right-click a Toggle / Flipbook Builder / page row -> *WhyKnot -> wk-vrcfury-qol -> Preview ...*.

Creates a temporary, non-saveable copy of the avatar in place, applies the selected toggle or flipbook page to that copy, and hides the source avatar in Scene view. It does not move, reframe, or switch the Scene camera. While a preview is active, the inline button changes to a red *Stop Previewing* button. Clicking it destroys the copy and restores visibility/selection if needed. The preview copy is also destroyed before entering play mode, before script reloads, and on editor quit. If you navigate away from the original inspector, *Tools -> WhyKnot -> wk-vrcfury-qol -> Stop previewing* clears the active copy. The original avatar is never mutated.

Supported visual actions include object toggles, blendshapes, material swaps, material properties, animation clips, Poiyomi flipbook frames, UV tile rows, and scale actions. Nonvisual actions are skipped. If a Toggle contains a Flipbook Builder, Preview opens a page picker so you can preview one page at a time.

## Hot Reload + Logs

Always-on background tool. Status at *Window -> WhyKnot -> VRCFury QoL -> Hot Reload Status*. Main package logs are under `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol/`; hot-reload sessions are under `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol.Editor.hotreload/`.

A `FileSystemWatcher` on this package's own source root triggers `AssetDatabase.Refresh()` shortly after a package file changes, so saving from an external editor with Unity unfocused still picks up package edits. It does not watch the whole project. Subscribes to `CompilationPipeline.assemblyCompilationFinished` and writes a one-line summary per assembly plus one line per error to the hot-reload session log.

Bootstrap: focus Unity once after the first install so it compiles the scripts. From then on the watcher runs whenever Unity is open.
