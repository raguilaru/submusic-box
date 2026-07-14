@docs/STATUS.md
@docs/requirements/10-final-requirements.md
@docs/requirements/50-acceptance-criteria.md
@docs/requirements/60-implementation-slices.md

# Project
BoxSubMusic — native Xbox Series X/S music streaming app for Subsonic/OpenSubsonic servers.
Primary target: UWP + XAML + C# with controller-first 10-foot UX.
This project is requirement-driven. The requirements docs are the source of truth.

# Core rules
- Do not invent APIs, payloads, server capabilities, or Xbox platform behavior.
- Prefer OpenSubsonic capabilities detection over hardcoded server-version branching.
- Implement one requirement slice at a time.
- Before coding, state which requirement sections are being implemented.
- Before finishing, verify the related acceptance criteria and list any gaps.
- If a requirement is ambiguous, ask instead of guessing.
- Do not rewrite unrelated files.
- Keep architecture simple and boring unless the requirements demand otherwise.

# Architecture guardrails
- UI stack is native XAML + C#; do not introduce WebView or HTML wrapper UI.
- Solution structure: UWP app project + Core library + Subsonic API client library
  (see docs/architecture/20-project-structure.md).
- Controller navigation and 10-foot readability take priority over desktop-style UX.
- Background playback, queue behavior, and Now Playing interactions are core features, not optional polish.
- Home/Search must always remain visible in the nav.
- Any feature that depends on an unsupported server extension must degrade explicitly and visibly.
- Credentials: server URL/username in local app settings; password/token in the
  Windows Credential Locker (PasswordVault). Never store secrets in plain settings.
- No reflection-based serializers (System.Text.Json, Newtonsoft.Json) anywhere:
  runtime risk under legacy .NET Native. Server XML is parsed with XDocument
  into typed models via explicit mapping; local structured persistence uses the
  same approach (requirements §16, architecture doc).

# Working style
- Use requirement-driven development.
- Work in small, reviewable increments.
- For each increment:
  1. Read the relevant requirement section.
  2. Propose a short implementation plan.
  3. Implement only that slice.
  4. Validate against acceptance criteria.
  5. Document any deviation or unresolved issue.

# Definition of done
A task is done only when:
- The implementation matches the referenced requirement section.
- Acceptance criteria for that slice are satisfied.
- Build compiles cleanly.
- No unrelated regressions were introduced.
- Any changed behavior is reflected in docs if needed.

# File-change policy
- Update docs when architecture, behavior, or workflow changes.
- Keep naming and folder structure consistent.
- Favor typed models and explicit contracts over ad hoc JSON/dynamic parsing.
- Prefer small services and view models over giant god-classes.

# Environment notes
- Development happens on macOS; builds/packaging happen in a Windows VM (Parallels)
  reached over SSH. The repo is shared into the VM via a Parallels shared folder,
  so there is one copy of the code — no sync step.
- Remote commands follow the pattern `ssh <vm-alias> "<command>"`. The alias,
  IP, path mapping (Mac path ↔ Windows drive letter), and current setup status
  live in CLAUDE.local.md (gitignored) — read it before running any VM command.
- Never edit code inside the VM; edit on the Mac side only. The VM is for
  build/package/deploy.
- Keep Windows-specific commands and machine-local paths out of committed docs.

# Commands
See CLAUDE.local.md for the concrete, machine-local invocations (SSH alias, full paths).
- Verify build loop: SSH echo test against the VM (must work non-interactively).
- Build (verified): VS MSBuild over SSH against src/BoxSubMusic.slnx with
  /restore /p:Configuration=Debug /p:Platform=x64 (x64 = Xbox architecture).
- Test (verified): dotnet test over SSH against each project under tests/.
- Deploy (console and local VM): VS F5 in the VM — this is the only working
  package+deploy path (command-line appx packaging fails with APPX0703).
  Rafael drives F5; details in CLAUDE.local.md and docs/STATUS.md.

# Open questions
- Search result paging/virtualization policy is not yet locked (browse views settled on incremental loading 2026-07-13; search may follow).
- Artwork fallback beyond server-provided metadata remains a risk item, not a committed feature.
- Playlists are read-only for v1 (decided 2026-07-13, §16/§17); create/edit/delete deferred beyond v1.
