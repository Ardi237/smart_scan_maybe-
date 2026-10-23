[CmdletBinding()]
param(
    [ValidateSet('all', 'valid', 'build-fail', 'wrong-output', 'infinite-loop', 'suspicious-target')]
    [string]$Case = 'all'
)

$ErrorActionPreference = 'Stop'

$projectRoot = Split-Path -Parent $PSScriptRoot
$samplesRoot = Join-Path $projectRoot 'samples'
$imageName = 'smartscanner-runner-poc:1'

function Assert-Equal {
    param(
        [Parameter(Mandatory)]$Actual,
        [Parameter(Mandatory)]$Expected,
        [Parameter(Mandatory)][string]$Message
    )

    if ($Actual -ne $Expected) {
        throw "$Message Expected '$Expected', got '$Actual'."
    }
}

function Test-NetworkIsolation {
    $probeExitCode = 0
    & docker run --rm --network none --read-only --cap-drop ALL --security-opt no-new-privileges --entrypoint sh $imageName -lc 'getent hosts example.com >/dev/null 2>&1'
    $probeExitCode = $LASTEXITCODE

    if ($probeExitCode -eq 0) {
        throw 'Network isolation failed: the runner resolved example.com despite --network none.'
    }

    Write-Host 'PASS network isolation probe'
}

function Invoke-PocCase {
    param(
        [Parameter(Mandatory)][string]$CaseName,
        [Parameter(Mandatory)][int]$ExpectedExitCode,
        [Parameter(Mandatory)][AllowEmptyString()][string]$InputText,
        [Parameter(Mandatory)][AllowEmptyString()][string]$ExpectedOutput
    )

    $casePath = Join-Path $samplesRoot $CaseName
    $resolvedCasePath = (Resolve-Path -LiteralPath $casePath).Path
    $containerName = "smartscanner-poc-$CaseName-$([guid]::NewGuid().ToString('N'))"
    $hostProbePath = Join-Path $resolvedCasePath 'host-probe.txt'

    if (Test-Path -LiteralPath $hostProbePath) {
        Remove-Item -LiteralPath $hostProbePath -Force
    }

    $mount = "type=bind,src=$resolvedCasePath,dst=/input,readonly"

    & docker run --rm `
        --name $containerName `
        --network none `
        --read-only `
        --cap-drop ALL `
        --security-opt no-new-privileges `
        --pids-limit 64 `
        --memory 256m `
        --cpus 1 `
        --user 10001:10001 `
        --tmpfs /tmp:rw,exec,size=64m `
        --tmpfs /workspace:rw,exec,size=128m `
        --tmpfs /home/runner/.nuget:rw,size=64m `
        --mount $mount `
        -e "INPUT_TEXT=$InputText" `
        -e "EXPECTED_OUTPUT=$ExpectedOutput" `
        -e 'RUN_TIMEOUT_SECONDS=3' `
        $imageName

    $actualExitCode = $LASTEXITCODE
    Assert-Equal -Actual $actualExitCode -Expected $ExpectedExitCode -Message "POC case '$CaseName' returned an unexpected runner exit code."

    if (Test-Path -LiteralPath $hostProbePath) {
        throw "Containment failed: suspicious project created '$hostProbePath' on the host."
    }

    $leftover = & docker ps -a --filter "name=^/$containerName$" --format '{{.ID}}'
    if ($leftover) {
        throw "Cleanup failed: container '$containerName' still exists."
    }

    Write-Host "PASS $CaseName (exit code $actualExitCode)"
}

Push-Location $PSScriptRoot
try {
    & docker build --tag $imageName .
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not build the SmartScanner runner image.'
    }

    Test-NetworkIsolation

    $cases = @(
        @{ Name = 'valid'; ExpectedExitCode = 0; InputText = "10`n20`n"; ExpectedOutput = '30' },
        @{ Name = 'build-fail'; ExpectedExitCode = 21; InputText = ''; ExpectedOutput = '' },
        @{ Name = 'wrong-output'; ExpectedExitCode = 44; InputText = "10`n20`n"; ExpectedOutput = '30' },
        @{ Name = 'infinite-loop'; ExpectedExitCode = 42; InputText = ''; ExpectedOutput = '' },
        @{ Name = 'suspicious-target'; ExpectedExitCode = 21; InputText = ''; ExpectedOutput = '' }
    )

    foreach ($pocCase in $cases) {
        if ($Case -eq 'all' -or $Case -eq $pocCase.Name) {
            Invoke-PocCase `
                -CaseName $pocCase.Name `
                -ExpectedExitCode $pocCase.ExpectedExitCode `
                -InputText $pocCase.InputText `
                -ExpectedOutput $pocCase.ExpectedOutput
        }
    }

    Write-Host 'SmartScanner runner POC completed successfully.'
}
finally {
    Pop-Location
}
