# VRCFury QoL docs

`wk-vrcfury-qol` is a small framework and a set of Unity Editor tools that add actions directly to the [VRCFury](https://vrcfury.com/) component inspector. Right-click a page, click a button on a flipbook row, drop in two objects to swap references. No separate window.

The [README](../README.md) is the quick start.

## How it works

VRCFury's runtime types (`VF.Model.VRCFury`, `VF.Model.Feature.Toggle`, etc.) are `internal`, so a script in `Assets/Editor/` can't reference them. The package works around that in two parts:

1. A reflection cache (`Editor/VrcfQol.cs`, `ReflectionCache`) resolves VRCFury's types and fields by name on first use and hands typed handles to tools. If a VRCFury update renames a field, the cache returns null and tools degrade: the banner says what's missing and the affected right-click items drop out. Nothing crashes.
2. A registration API lets each tool plug in with one `[InitializeOnLoad]` static class. Tools never touch the inspector's visual tree. Right-click items ride on `EditorApplication.contextualPropertyMenu`, and a small UIElements overlay (`VrcfQolInspectorOverlay.cs`) scans inspector windows every ~250 ms and attaches buttons and banners next to recognisable labels.

[Architecture](Architecture.md) has the details.

## Pages

- [Installation](Installation.md): drop-in steps and the hot-reload bootstrap
- [Tools Overview](Tools-Overview.md): every tool and where to find it
- [Architecture](Architecture.md): registration, reflection, overlay
- [Adding a Tool](Adding-a-Tool.md): worked examples for every `Register*` API
- [Troubleshooting](Troubleshooting.md): what to check first
