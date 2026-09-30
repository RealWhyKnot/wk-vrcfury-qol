# Troubleshooting

Common failure modes and what to check first. If your problem isn't here, file a [bug report](https://github.com/RealWhyKnot/wk-vrcfury-qol/issues/new?template=bug_report.yml) with the Unity version, VRCFury version and the affected tool.

## "VRCFury runtime assembly ('VRCFury') not found."

A dialog shows this when you run a tool. The reflection cache couldn't find the VRCFury assembly in the current AppDomain.

Check:
- VRCFury is imported into the project. It shows up in `Packages/com.vrcfury.vrcfury/` (VCC install) or `Assets/VRCFury/` (manual install).
- The assembly name is still `"VRCFury"` (case-sensitive). If a VRCFury release renames it, the lookup fails until [`Editor/VrcfQol.cs`](https://github.com/RealWhyKnot/wk-vrcfury-qol/blob/main/Editor/VrcfQol.cs) is updated.

## "Could not locate one or more VRCFury internal types."

The assembly was found but a specific type (`VF.Model.VRCFury`, `VF.Model.Feature.Toggle`, etc.) couldn't be resolved.

Check your VRCFury version. This project is tested against 1.1303.x, and much older or newer versions may rename internals. If yours moved a type, [open a `vrcfury: compat` issue](https://github.com/RealWhyKnot/wk-vrcfury-qol/issues) with the new path and I'll update the cache.

## Right-click menu items don't appear

The framework hooks `EditorApplication.contextualPropertyMenu`, which runs when Unity right-clicks a serialized property. If items are missing:

- Most items target a specific property: a Toggle component, a flipbook page row, the Flipbook Builder action header. Right-clicking the inspector background, or a label outside a serialized property, won't trigger the menu.
- Tools register in their static constructor (`[InitializeOnLoad]`). Focus Unity once after install so it compiles and runs them. Look for `[VRCF QoL]` log lines on startup.
- A tool's `match()` may be returning false. To invoke *Migrate child toggles as pages*, the trigger has to be on the matching type. Its `match` expects a `[SerializeReference]` of `VF.Model.StateAction.FlipBookBuilderAction`, so right-clicking the page rows below it won't match.

## Inline buttons (e.g. Duplicate) don't appear

`VrcfQolInspectorOverlay.cs` injects the buttons. It scans inspector windows every ~250 ms for labels matching `^Page #\d+$`. If buttons don't show up:

- VRCFury may have restyled the inspector. This is best-effort UI injection. The right-click menu on a page row still works (`WhyKnot/wk-vrcfury-qol/Duplicate page to end`).
- The page label format may have changed. A future VRCFury version could localise or restyle "Page #N", and the overlay would need updating.

## Auto Global Parameter banner missing

The green banner at the top of every Toggle inspector confirms auto-sync is on. If it's absent:

- The overlay may have failed silently. Check the console for its try/catch logs.
- The Toggle needs a name. The sync skips toggles with an empty or whitespace name, since there's nothing to set `globalParam` to. Once you name it, the next 0.5 s tick syncs it and the banner appears.
- The component may be in a closed prefab stage. The sync only runs on the open scene and open prefab stage.

## Replace-References lists nothing

- No VRCFury components in the selection. The window walks every selected GameObject and its children. Try selecting the avatar root.
- All references are null. The scan skips properties whose value is `null`. If a reference should be set, fix it in the VRCFury inspector first.
- The reference is a string path, not an Object reference. The tool only handles `ObjectReference` properties. Some VRCFury fields store paths as strings (animation curve target paths, for example) and aren't covered.

## Replace-References "skipped N stale entries"

A row's underlying reference changed between *Scan* and *Apply*, either because you edited the field in another inspector after the scan or because another tool (or another Apply) changed it first. The stale row is skipped so the tool doesn't overwrite a value you didn't see. Click *Refresh* to re-scan.

## Move tool: "Merge into one component" is greyed out

The installed VRCFury version doesn't expose the legacy `VRCFuryConfig.features` list, or the lookup failed. Use *Move whole components* instead. The result is the same for most workflows, except it keeps the original component count.

If you need merge and the field has been renamed, [file a `vrcfury: compat` issue](https://github.com/RealWhyKnot/wk-vrcfury-qol/issues) with your version.

## Hot-reload log not updating

Logs are under `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol.Editor.hotreload/`. If the current `session-*.log` isn't growing when you edit this package's files:

- First-time install: focus Unity once so it compiles the package. After that the watcher runs.
- Wrong file: the watcher is package-scoped, so edits to unrelated `Assets/` scripts or third-party packages don't trigger it.
- `FileSystemWatcher` failed to start. This shows as an exception in the Unity console at editor startup, from anti-virus, sandboxing or unusual filesystem types. Unity's built-in focus-based refresh still works, the log just isn't written.

## Undo doesn't revert everything

Each tool collapses its operation into one Undo group, with a few exceptions:

- If you ran a tool, made manual edits, then pressed `Ctrl+Z`, you undo the manual edits first.
- Auto Global Parameter ticks aren't undoable. The polling sync doesn't register Undo because it would flood the stack. To stop it touching a toggle, opt out from the inspector banner.
- `ComponentUtility.PasteComponentAsNew` registers its own Undo entry. The Move tool collapses these into one group, and if Unity fails a paste mid-operation the tool's catch block calls `Undo.RevertAllInCurrentGroup`.
