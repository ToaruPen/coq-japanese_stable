# Runtime Diagnostics Probe API Plan

Issue: <https://github.com/ToaruPen/coq-japanese_stable/issues/588>

## Goal

Centralize QudJP runtime diagnostics so verbose probe logging is easy to add in
development builds but is filtered out of shipping builds by default. The API
must reduce future drift: new probe emitters should use one approved entrypoint,
and direct verbose probe writes should fail deterministic checks.

## Scope

1. Extend `RuntimeDiagnostics` with:
   - `LogImportant(string)` for build markers, warnings, errors, and
     sink-required shipping signals.
   - `LogVerboseProbe(Func<string?>)` for development-only probe logging.
2. Compile verbose probe calls out of shipping builds with
   `Conditional("QUDJP_DEV_BUILD")`.
3. Migrate existing verbose probe emitters away from direct Unity/trace logging.
4. Add analyzer coverage (`QJ004`) for direct verbose probe log writes through
   `QudJPMod.LogToUnity`, `UnityEngine.Debug.Log`, and
   `Trace.TraceInformation`.
5. Strengthen release DLL verification so ASCII and .NET UTF-16 metadata marker
   leaks are rejected.
6. Update agent and runtime-evidence docs with the new diagnostics policy.

## Acceptance Criteria

- Development tests still observe verbose probe output when
  `QUDJP_DEV_BUILD` is enabled.
- Release builds retain important markers but do not retain known verbose probe
  marker strings such as `SinkObserve/v1`, `Translator: missing key`, or
  `no pattern for`.
- Analyzer tests prove direct verbose probe writes are rejected and approved
  `RuntimeDiagnostics` calls are allowed.
- Python release verifier tests cover both ASCII fixtures and UTF-16LE .NET
  metadata strings.
- `just test-l1`, analyzer tests, Python checks/tests, `just build`, and
  `just build-release` pass.

## Non-Goals

- Do not change translation ownership rules.
- Do not remove important shipping warnings or error logs.
- Do not change Steam Workshop deployment behavior in this branch.
