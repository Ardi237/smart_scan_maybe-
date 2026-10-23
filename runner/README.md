# SmartScanner Runner Proof of Concept

This proof of concept runs untrusted C# console-project samples in a local Docker container. It proves the minimum runner behaviors needed by SmartScanner before the application UI is built.

The image uses the .NET 10 SDK because the supplied 2026 submissions target `net10.0`.

## Controls exercised

- The source folder is mounted read-only at `/input`.
- The project is copied into a temporary container workspace before restore and build.
- The container has no network connection.
- The container filesystem is read-only except for temporary mounts.
- The process runs as the non-root `runner` user.
- CPU, memory, process count, and runtime are limited.
- Containers use `--rm`; the launcher verifies that no test container remains.

## Cases

| Case | Expected result |
|---|---|
| `valid` | Build and functional run pass |
| `build-fail` | Build fails with exit code `21` |
| `wrong-output` | Functional output mismatch with exit code `44` |
| `infinite-loop` | Runtime timeout with exit code `42` |
| `suspicious-target` | Build target attempting to write to the read-only input mount is blocked with exit code `21` |

## Run

From this directory in an elevated PowerShell that can access Docker:

```powershell
.\Invoke-RunnerPoc.ps1
```

Run one case only:

```powershell
.\Invoke-RunnerPoc.ps1 -Case infinite-loop
```

## Verification record

Verified locally on 2026-10-12:

- Network-isolation probe passed with Docker `--network none`.
- `valid` built and accepted redirected standard input, then returned the expected output.
- `build-fail` returned the expected build-failure result.
- `wrong-output` returned the expected output-mismatch result.
- `infinite-loop` was stopped by the runner timeout.
- `suspicious-target` was blocked from writing to the read-only `/input` mount.
- No `smartscanner-poc` Docker containers or host probe files remained after the tests.

This proof of concept demonstrates the intended runner controls. It is not a security certification; the production runner must retain these controls and receive dedicated security review before handling real submissions.
