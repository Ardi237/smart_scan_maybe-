# SmartScanner

SmartScanner is a local pre-screening application for C# console-programming submissions. It reports whether a submission passes objective checks; it does not issue academic grades.

## Current scope

- ASP.NET Core Razor Pages application
- SQLite database stored under `data/`
- Assignment catalog seeded with `LATIHAN_1` and `JURNAL_1`
- Assignment list page at `/Assignments`
- Upload, archive validation, safe extraction, and persistent screening history
- One screening pipeline per submission: administrative checks followed by an isolated Docker build
- Functional testing with redirected stdin, captured stdout/stderr, per-test timeout, `LAST_NUMBERS`, and decimal tolerance
- Submission report with status/class/assignment/reason filters, run details, and recheck action
- General Settings for attendance-number to assistant-code mapping
- Docker runner proof of concept under `runner/`

## Run locally

### Requirements

- [.NET SDK 9](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) running locally

### Start the application

From the project root:

```powershell
dotnet restore .\SmartScanner.sln
docker build -t smartscanner-runner-poc:1 .\runner
dotnet run --project .\src\SmartScanner.Web --urls http://127.0.0.1:5089
```

Open `http://127.0.0.1:5089/Submissions` in a browser. To use a different port, replace `5089` in the final command.

The application applies its initial SQLite migration automatically on startup. By default, the database is created at `src/SmartScanner.Web/data/smartscanner.db`.

## Screening result

Each upload creates a `screening_run` and saves every check and functional-test result in SQLite. The submission moves through `RECEIVED`, `PROCESSING`, then becomes `PASSED`, `FAILED`, or `SYSTEM_ERROR`. When the project structure permits it, the pipeline builds and runs it in the `smartscanner-runner-poc:1` Docker image with no network, a read-only input mount, resource limits, and timeouts. Build stdout/stderr is retained under `data/runs/<run-id>/build.log`; temporary extraction workspaces are removed after every run.

The seeded filename contracts follow the PDFs: `{KODE_ASISTEN}_LATIHANX_{NAMA}_{NIM}.zip` and `{KODE_ASISTEN}_JURNAL1_{NAMA}_{NIM}.zip`. The assistant code is derived from General Settings using the submitted attendance number. Class names remain text, so values such as `26-06` are valid.

The Docker image is required for compilation and functional testing. If it has not been built yet, run:

```powershell
docker build -t smartscanner-runner-poc:1 .\runner
```

## Test

```powershell
dotnet test .\SmartScanner.sln
```
