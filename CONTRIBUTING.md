# Contributing

Bug reports, feature requests and pull requests are welcome. Open an issue or PR against this repo.

## Dev loop

There's no build system. `wk-vrcfury-qol` is a folder of `.cs` files that Unity compiles itself.

You need Unity 2022.3.x, a Unity project with VRCFury imported, and git.

Clone the repo outside your Unity project and link its `Editor/` folder into the project under any `Assets/...Editor/` path. Edits in the repo then show up in the project without copying anything.

Windows (PowerShell, run as admin):
```powershell
New-Item -ItemType Junction -Path "C:\Path\To\YourProject\Assets\VrcfQol" -Target "C:\Path\To\wk-vrcfury-qol\Editor"
```

Linux / macOS:
```sh
ln -s /path/to/wk-vrcfury-qol/Editor /path/to/YourProject/Assets/VrcfQol
```

Focus Unity once after linking so it compiles the scripts. After that, saving a `.cs` file triggers the package's hot reload, which runs `AssetDatabase.Refresh()` with Unity in the background. Its session logs are in `%LocalAppData%/WhyKnot/Logs/dev.whyknot.wk-vrcfury-qol.Editor.hotreload/`.

## Pull requests

Branch from `main` and open the PR against `main`. The [PR template](.github/PULL_REQUEST_TEMPLATE.md) fills in the description. Be honest on the checklist, especially "compiles in Unity 2022.3.x with no console errors".

If you add reflection-cache fields, make sure the tool still works when one is missing. `Editor/VrcfQol.cs` has examples of optional fields. VRCFury internals change between releases. If your PR depends on a new field, say which VRCFury version you tested against.

Keep each PR to one change.

## Commit messages

Commit subjects need a Conventional Commits prefix like `feat:`, `fix:`, `docs:`, `chore:`, `refactor:` or `ci:`, and CI checks it on every PR. Keep the subject to 72 characters or less. The diff already shows what changed. Use the body for why, what you tried and rejected, and any VRCFury version quirk you're working around.

## Security issues

Don't file a public issue for a vulnerability. Use the Security tab -> Report a vulnerability. See [SECURITY.md](.github/SECURITY.md).
