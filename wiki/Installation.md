# Installation

`wk-vrcfury-qol` is a flat folder of `.cs` files that Unity compiles itself. No asmdef, no package manifest, no native binaries. For the VCC route, see the [README](../README.md#installation).

## Option A: drop it into your project

Copy the `Editor/` folder into your Unity project under any path that ends in or contains `Editor/`. Unity picks it up as an editor-only assembly.

```
Assets/
  YourFolder/
    Editor/
      VrcfQol.cs
      VrcfQolInspectorOverlay.cs
      VrcfQolHotReload.cs
      Tools/
        AutoGlobalParameterTool.cs
        DuplicateFlipbookPageTool.cs
        MigrateIntoFlipbookTool.cs
        MoveVrcfComponentsTool.cs
        ReplaceReferencesTool.cs
        ReplaceReferencesWindow.cs
```

## Option B: symlink for live development

Clone the repo outside your Unity project and link its `Editor/` folder in. Edits in the repo apply to the live project without copying.

Windows (PowerShell, run as admin):
```powershell
New-Item -ItemType Junction -Path "C:\Path\To\YourProject\Assets\VrcfQol" -Target "C:\Path\To\wk-vrcfury-qol\Editor"
```

Linux / macOS:
```sh
ln -s /path/to/wk-vrcfury-qol/Editor /path/to/YourProject/Assets/VrcfQol
```

## Hot-reload bootstrap

After the first install, focus Unity once so it compiles the new scripts. From then on the hot-reload watcher runs `AssetDatabase.Refresh()` whenever one of this package's source files changes, even when Unity is unfocused. It doesn't watch unrelated `Assets/` or third-party package files.

It also writes per-session compile logs under `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol.Editor.hotreload/`. Tail the current `session-*.log` to watch compiles:

```powershell
Get-Content "$env:LocalAppData\WhyKnot\Logs\dev.whyknot.wk-vrcfury-qol.Editor.hotreload\session-*.log" -Wait
```

Compile errors include the file path and line and column.

## Compatibility

- Unity 2022.3.x is what I test against.
- VRCFury 1.1303.x is the latest version I've checked. Older versions usually work. If the reflection cache can't resolve a field, the tool does nothing or shows an error dialog (see [Troubleshooting](Troubleshooting.md)).

## Uninstalling

Delete the folder you installed into. Per-toggle opt-outs from the [Auto Global Parameter](Tools-Overview.md#auto-global-parameter) tool stay in EditorPrefs. They're harmless, but you can remove them by searching EditorPrefs for keys starting with `VrcfQol.AutoUpdateParam.OptOut.`.
