# Security policy

## Reporting a vulnerability

If you find a security issue, don't file a public issue. Use GitHub's [private vulnerability reporting](https://github.com/RealWhyKnot/wk-vrcfury-qol/security/advisories/new).

I'll acknowledge within a week and aim to ship a fix or workaround within 30 days.

## Scope

`wk-vrcfury-qol` is a Unity Editor-only tool. It runs with your user privileges inside the Unity Editor process. It makes no network requests, loads no native code, runs no external binaries, and needs no elevated privileges. It only touches assets in the open project (scenes, prefabs, EditorPrefs) and logs to `<ProjectRoot>/Logs/VrcfQolHotReload.log`.

In scope:
- Editor script code paths that data crafted into a scene or prefab could trigger, such as malicious VRCFury data on a third-party prefab.
- File-system writes outside the project root through the hot-reload log path.

Out of scope:
- Bugs that need an attacker to already have write access to your project files.
- Bugs in Unity or VRCFury. Report those upstream.
- "The tool did the wrong thing". File a normal issue.
