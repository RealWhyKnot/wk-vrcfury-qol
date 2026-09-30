# Contributing

Bug reports, feature requests and pull requests are welcome. Open an issue or PR against this repo.

## Dev loop

There's no build system. `wk-vrcfury-qol` is a flat folder of `.cs` files that Unity compiles itself.

You need Unity 2022.3.x, a Unity project with VRCFury imported, and git.

Clone the repo outside your Unity project and link its `Editor/` folder into the project under any `Assets/...Editor/` path. Edits in the repo then show up in the live project without copying.

Windows (PowerShell, run as admin):
```powershell
New-Item -ItemType Junction -Path "C:\Path\To\YourProject\Assets\VrcfQol" -Target "C:\Path\To\wk-vrcfury-qol\Editor"
```

Linux / macOS:
```sh
ln -s /path/to/wk-vrcfury-qol/Editor /path/to/YourProject/Assets/VrcfQol
```

Once linked, `VrcfQolHotReload.cs` picks up `.cs` saves and runs `AssetDatabase.Refresh()` even when Unity isn't focused. Tail `<ProjectRoot>/Logs/VrcfQolHotReload.log` to watch compiles. Focus Unity once after the first install so it compiles the scripts; after that the watcher takes over.

## Pull requests

- Branch from `main` and open the PR against `main`.
- The [PR template](.github/PULL_REQUEST_TEMPLATE.md) fills in the description. Be honest on the checklist, especially "compiles in Unity 2022.3.x with no console errors".
- If you added reflection-cache fields, check that they degrade gracefully when missing. `Editor/VrcfQol.cs` has examples of optional fields.
- Keep PRs focused.
- VRCFury internals change between releases. If your PR depends on a new field, say which VRCFury version you tested against.

## Commit messages

Prefixes like `feat:`, `fix:`, `docs:`, `chore:`, `refactor:` and `ci:` are appreciated but not enforced. Keep the subject to 72 characters or less. Use the body for why: the diff shows what changed, so say what you rejected and any VRCFury-version gotcha you're working around.

## Security issues

Don't file a public issue for a vulnerability. Use the Security tab -> Report a vulnerability. See [SECURITY.md](.github/SECURITY.md).
