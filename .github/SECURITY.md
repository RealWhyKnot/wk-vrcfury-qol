# Security policy

If you find a security issue, please don't file a public issue. Use GitHub's [private vulnerability reporting](https://github.com/RealWhyKnot/wk-vrcfury-qol/security/advisories/new).

I'll acknowledge it within a week and try to have a fix or workaround released within 30 days.

## Scope

`wk-vrcfury-qol` is a Unity Editor-only tool that runs with your user privileges inside the Unity Editor process. It doesn't make network requests, load native code, run external binaries or need elevated privileges. The only things it changes are assets in the open project (scenes, prefabs, EditorPrefs), and its logs go under `%LocalAppData%/WhyKnot/Logs/`.

In scope:
- Editor code paths that crafted scene or prefab data can trigger, like malicious VRCFury data on a third-party prefab.
- File writes outside the project and that log folder through the logging code.

Out of scope:
- Bugs that need an attacker to already have write access to your project files.
- Bugs in Unity or VRCFury. Report those upstream.
- The tool doing the wrong thing. File a normal issue for that.
