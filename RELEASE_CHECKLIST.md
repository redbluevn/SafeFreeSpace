# Release Checklist — SafeFreeSpace

## Pre-release

- [ ] All unit tests pass.
- [ ] All opt-in integration tests pass on VHD.
- [ ] `dotnet format --verify-no-changes` passes.
- [ ] Version bumped in `Directory.Build.props`.
- [ ] `CHANGELOG.md` updated.
- [ ] Security review completed (threat model, command injection, privilege escalation, log privacy).

## Build

- [ ] Run `publish.ps1 -VersionPrefix X.Y.Z -VersionSuffix ''`.
- [ ] Verify `SafeFreeSpace.exe` manifest is `asInvoker`.
- [ ] Verify `SafeFreeSpace.ElevatedWorker.exe` manifest is `requireAdministrator`.
- [ ] Verify both executables are self-contained x64.
- [ ] Verify `SHA256SUMS.txt` is present.

## Packaging

- [ ] Portable ZIP generated.
- [ ] Installer built (future).
- [ ] PDBs archived separately (future).

## Signing (future)

- [ ] Code-sign both executables.
- [ ] Verify signature with `signtool verify /pa`.

## Distribution

- [ ] Upload signed package and checksums.
- [ ] Tag release in git.
- [ ] Publish release notes.

## Post-release

- [ ] Smoke test on clean Windows VM.
- [ ] Confirm dev build warning does not appear on signed release.
