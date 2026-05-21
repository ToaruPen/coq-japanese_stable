# Runtime Evidence Workflow

Use this guide for runtime logs, Phase F route-proof checks, local mod sync,
deployment checks, and decompiled-source tracing. Translation ownership
decisions still belong in `docs/RULES.md`.

## Runtime logs

Use runtime logs as evidence, not as the primary behavior definition.

Important paths:

- current log: `~/Library/Logs/Freehold Games/CavesOfQud/Player.log`
- previous log: `~/Library/Logs/Freehold Games/CavesOfQud/Player-prev.log`
- build log: `~/Library/Application Support/Freehold Games/CavesOfQud/build_log.txt`

Useful markers:

- `[QudJP] Build marker`
- important error or warning lines that affect shipping behavior
- sink-required signals that prove a route still needs sink observation
- `MODWARN`

Verbose probe markers such as `DynamicTextProbe/v1`, `FinalOutputProbe/v1`,
`SinkObserve/v1`, `[QudJP] Translator: missing key`, and `no pattern for` are
development evidence by default. Add or change them through
`RuntimeDiagnostics` (`LogVerboseProbe(...)` or the existing helper APIs that
call it), not by writing directly to Unity logs. Release artifacts must not
retain direct verbose probe log markers; `scripts/verify_release_dll.py`
rejects known direct marker strings in release DLLs and release ZIPs.

Known non-QudJP runtime noise:

- `GALAXY - Error initializing` with
  `System.DllNotFoundException: GalaxyCSharpGlue` can appear during local
  direct/Rosetta validation before QudJP bootstrap. Treat it as game/Galaxy SDK
  platform noise unless fresh evidence also shows a QudJP build marker,
  QudJP-owned compile error, `MODWARN`, missing glyph warning, or QudJP
  exception that makes it actionable for this mod.

On Apple Silicon, use Rosetta for in-game evidence:

- `scripts/launch_rosetta.sh`
- `Launch CavesOfQud (Rosetta).command`

Do not treat native ARM64 runtime logs as localization observability evidence.

Do not launch the GUI game automatically during an agent run unless the user
explicitly asks for an in-game smoke pass. Launching the game can block the
session and requires a human-observable path through the UI. If fresh evidence
requires gameplay, sync the mod first, then ask the user to run the Rosetta
launcher or state that post-sync runtime evidence is still pending.

When the user asks to check fresh runtime evidence after a sync, complete the
rebuild/sync first. If no post-sync Rosetta log exists yet, report the runtime
evidence as pending and tell the user exactly which launcher/log path is needed
for the next check. Resume the evidence check only after a new post-sync log is
available. The post-sync Rosetta log to check is
`~/Library/Logs/Freehold Games/CavesOfQud/Player.log`.

Treat a runtime log as fresh post-sync evidence only when its modification time
is after the rebuild/sync being evaluated, or when its `[QudJP] Build marker`
matches the build under review. If the latest log predates the sync or lacks a
matching marker, summarize it as stale context and do not use it as proof that
the current worktree regressed or succeeded.

For issue closeout PRs that rely on runtime evidence, state the closeout
decision explicitly in both the PR body and the runtime report:

- identify the decision type:
  - root-cause fix: fixes the underlying failure so the original route works
    normally, such as correcting a broken API call;
  - supported fallback path: adopts a controlled alternate route when the
    original route is unreliable, such as rendering through a replacement UI
    child;
  - observability-only mitigation: adds evidence or monitoring without claiming
    user-visible behavior is fixed, such as bounded diagnostic log markers;
- if the issue acceptance criteria allowed multiple outcomes, name the chosen
  outcome before using `Closes #...`;
- include the final smoke log time or matching build marker, the success
  marker counts, and the relevant failure marker counts;
- if a supported fallback path is adopted provisionally, document the runtime
  signals that should reopen root-cause investigation, such as performance
  regressions, visual regressions, or instability in the affected UI route.

## Phase F boundary

Phase F means runtime route-proof evidence. It is distinct from static coverage,
and it does not replace the source-first scanner or fixed-leaf workflow.

For the first PR in issue #358:

- keep the scope on runtime observability and triage
- keep SoT cross-reference deferred until the post-#357 integration follow-up
- keep `DynamicTextProbe` and `SinkObserve` as runtime evidence records, not
  static coverage verdicts

Shared defaults for this boundary are fixed in the parent roadmap and repeated
here for convenience:

- `template_id` is a transport-slot field in this PR, and runtime emitters use
  `<missing>` until the #357 follow-up owns the canonical static SoT side
- `family` uses the parent-roadmap vocabulary and is not renamed here
- `route` is emitted verbatim and is not normalized

Required verification commands for this boundary:

```bash
just runtime-evidence-check
```

Use these commands when checking Phase F docs, runtime observability, or the
first-PR boundary.

## QudTest final-text and binding artifact checks

QudTest is the preferred lightweight check when the question is "what final
string does QudJP send through this runtime route?" The default path is
headless: it loads repository-owned fixtures from `QudTest/fixtures/*.json`,
executes the same QudJP route helpers used by the mod, and writes artifacts
under `.artifacts/qudtest`.

QudTest also has a generic patch-binding lane for questions like "does this
patch still resolve to the current runtime method?" `qudtest:bindings` invokes
the patch's `TargetMethod()` / `TargetMethods()` entrypoint and compares the
resolved `DeclaringType|MethodName|ReturnType|ParamType...` signatures against
fixture `expectedTargets`. This catches stale patch targets and fixture drift,
but it does not prove that Harmony applied the patch in a live game or that the
translated final text is correct.

Use `qudtest:bindings-all` when you need a broad sweep across every patch type
with a Harmony target entrypoint. It does not compare fixture signatures; it
fails unknown empty target sets and target-resolution exceptions, while keeping
known intentional zero-target patches visible in the artifact with diagnostics.
Promote any high-risk patch from this broad sweep into a `qudtest:bindings`
fixture when its exact target signature should be frozen.

Workflow:

```bash
just qudtest-headless
just qudtest-headless qudtest:bindings .artifacts/qudtest-bindings
just qudtest-headless qudtest:bindings-all .artifacts/qudtest-bindings-all
```

Use the in-game wish path when you specifically need evidence that the deployed
mod loads and the wish bridge is discoverable by the game:

```bash
just deploy-mod
# In game, open wish and run one of:
#   qudtest
#   qudtest:runtime
#   qudtest:wish
#   qudtest:bindings
#   qudtest:bindings-all
just qudtest-inspect-game
```

Useful commands:

```bash
just qudtest-headless qudtest:wish
just qudtest-headless qudtest:bindings .artifacts/qudtest-bindings
just qudtest-headless qudtest:bindings-all .artifacts/qudtest-bindings-all
just qudtest-results
just qudtest-history
```

The latest headless result is:

```text
.artifacts/qudtest/results.json
```

The latest in-game result is:

```text
~/Library/Application Support/Freehold Games/CavesOfQud/Local/QudTest/results.json
```

`qudtest-inspect-game` compares runtime results to repository fixtures for the
artifact's suite, rejects stale artifacts, checks the QudJP fixture language tag
`modLanguage=ja`, reports failed cases, and scans `Player.log` for fatal mod
markers. The `qudtest-headless` recipe intentionally passes
`--skip-player-log`; runtime log validation only belongs to the in-game path.

QudTest final-text fixtures prove the final text value emitted by the selected
QudJP route. Binding fixtures prove target resolution against the current game
DLL. Neither mode proves actual rendered pixels, font fallback, layout, UI
visibility, or live Harmony application. For display issues, keep using the L3
manual smoke path and screenshots/logs.

## Diagnostics and probe logging

Runtime diagnostics must keep shipping logs small and actionable:

- startup/status, warning, and error logs go through `RuntimeDiagnostics`
- verbose route-proof probes go through `RuntimeDiagnostics.LogVerboseProbe`
- expensive probe message construction belongs inside the lazy message factory
- dev-only probe builders and probe patch classes should stay behind
  `QUDJP_DEV_BUILD` when their marker strings are not needed in release builds
- release artifacts must not contain obvious verbose probe markers such as
  `DynamicTextProbe/v1`, `FinalOutputProbe/v1`, or `SinkObserve/v1`
- QJ004 is a bypass guard for statically visible verbose probe markers on
  direct logging calls. It is intentionally not a general formatter,
  exception-message, or Unity logging API analyzer. If new probes need stronger
  guarantees, tighten `RuntimeDiagnostics`, the marker convention, release
  verification, or focused analyzer coverage before adding broad static
  inference.

Use dev builds for route-proof collection. Release/shipping builds keep the
important QudJP startup, warning, and error logs, but verbose probes are off by
default and are rejected by the release DLL marker gate when obvious marker
strings leak into the shipped DLL.

## Mod sync and deployment

Preferred agent deploy path:

```bash
just deploy-mod
```

Helpful variants:

```bash
just sync-mod-dry-run
just sync-mod-exclude-fonts
```

`scripts/sync_mod.py` deploys only game-essential files to the platform default
mod directory. Use `--destination` if your install uses a non-standard path.

Do not deploy arbitrary source files. The game will try to compile any `.cs`
file it finds, and only `Bootstrap.cs` is meant to be game-compiled.

## Decompiled game source

Decompiled source is a tracing aid, not a shipped artifact. The issue-357
Roslyn pilot treats this tree as read-only external input.

- location: `~/dev/coq-decompiled_stable/`
- regenerate with `scripts/decompile_game_dll.sh`
- never commit decompiled output or game binaries

Use decompiled code to:

- trace upstream producers
- verify method signatures and UI plumbing
- identify renderer-side stop points
- distinguish repo-owned bugs from game-owned limits
