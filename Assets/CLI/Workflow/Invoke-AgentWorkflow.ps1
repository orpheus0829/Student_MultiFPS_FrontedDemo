param(
    [ValidateSet("Quick", "Normal", "Full")]
    [string]$Profile = "Normal",
    [string]$ProjectPath = "",
    [string]$OutputRoot = ""
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ProjectPath))
{
    $ProjectPath = (Resolve-Path (Join-Path $PSScriptRoot "..\..\..")).Path
}

$ProjectPath = (Resolve-Path -LiteralPath $ProjectPath).Path
if ([string]::IsNullOrWhiteSpace($OutputRoot))
{
    $OutputRoot = Join-Path $ProjectPath "Temp\AgentReports"
}

$unityCli = Get-Command unity -ErrorAction SilentlyContinue
if (!$unityCli)
{
    $fallback = Join-Path $env:LOCALAPPDATA "Unity\bin\unity.exe"
    if (!(Test-Path -LiteralPath $fallback))
    {
        throw "没有找到 Unity CLI。"
    }

    $unityCli = $fallback
}
else
{
    $unityCli = $unityCli.Source
}

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$runFolder = Join-Path $OutputRoot "$timestamp-$($Profile.ToLowerInvariant())"
New-Item -ItemType Directory -Path $runFolder -Force | Out-Null

$steps = [System.Collections.Generic.List[object]]::new()
$hasFailure = $false

function Invoke-CapturedStep
{
    param(
        [string]$Name,
        [string]$FileName,
        [scriptblock]$Action,
        [switch]$Required
    )

    $startedAt = Get-Date
    $output = & $Action 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode)
    {
        $exitCode = 0
    }

    $outputPath = Join-Path $runFolder $FileName
    Set-Content -LiteralPath $outputPath -Value $output -Encoding UTF8
    $passed = $exitCode -eq 0
    $script:steps.Add([PSCustomObject]@{
        Name = $Name
        Passed = $passed
        ExitCode = $exitCode
        DurationSeconds = [Math]::Round(((Get-Date) - $startedAt).TotalSeconds, 2)
        Evidence = $FileName
    })

    if ($Required -and !$passed)
    {
        $script:hasFailure = $true
    }

    return $passed
}

Push-Location $ProjectPath
try
{
    git status --short | Set-Content -LiteralPath (Join-Path $runFolder "git-status-before.txt") -Encoding UTF8
    git diff --binary | Set-Content -LiteralPath (Join-Path $runFolder "git-diff.patch") -Encoding UTF8

    Invoke-CapturedStep "Pipeline连接" "pipeline.json" {
        & $unityCli pipeline list --format json
    } -Required | Out-Null

    Invoke-CapturedStep "修改前项目快照" "before.json" {
        & $unityCli command --project-path=$ProjectPath gundemo project_snapshot --format json
    } -Required | Out-Null

    $compiled = Invoke-CapturedStep "Unity编译" "compile.json" {
        & $unityCli recompile --project-path=$ProjectPath --format json
    } -Required

    if ($compiled)
    {
        $verified = Invoke-CapturedStep "项目专属检查" "verification.json" {
            & $unityCli command --project-path=$ProjectPath gundemo verification_bundle --format json
        } -Required

        if ($verified)
        {
            $verification = Get-Content -LiteralPath (Join-Path $runFolder "verification.json") -Raw | ConvertFrom-Json
            if (!$verification.data.result.Passed)
            {
                $steps[$steps.Count - 1].Passed = $false
                $steps[$steps.Count - 1].ExitCode = 2
                $hasFailure = $true
            }
        }

        if ($Profile -ne "Quick")
        {
            Invoke-CapturedStep "EditMode测试" "editmode-results.json" {
                & $unityCli command --project-path=$ProjectPath test run --mode editor --timeout 300 --format json
            } -Required | Out-Null
        }

        if ($Profile -eq "Full")
        {
            Invoke-CapturedStep "PlayMode测试" "playmode-results.json" {
                $startText = & $unityCli command --project-path=$ProjectPath test run --mode playmode --async-tests true --timeout 300 --format json 2>&1 | Out-String
                Set-Content -LiteralPath (Join-Path $runFolder "playmode-start.json") -Value $startText -Encoding UTF8
                if ($LASTEXITCODE -ne 0)
                {
                    Write-Output $startText
                    return
                }

                $deadline = (Get-Date).AddSeconds(300)
                while ((Get-Date) -lt $deadline)
                {
                    Start-Sleep -Seconds 1
                    $statusText = & $unityCli command --project-path=$ProjectPath test status --format json 2>&1 | Out-String
                    if ($LASTEXITCODE -ne 0)
                    {
                        continue
                    }

                    $status = $statusText | ConvertFrom-Json
                    if ($status.data.result.status -eq "completed")
                    {
                        Write-Output $statusText
                        if ($status.data.result.summary.failed -gt 0)
                        {
                            $global:LASTEXITCODE = 1
                        }
                        else
                        {
                            $global:LASTEXITCODE = 0
                        }
                        return
                    }
                }

                Write-Output '{"success":false,"error":"PlayMode tests timed out"}'
                $global:LASTEXITCODE = 1
            } -Required | Out-Null

            Invoke-CapturedStep "PlayMode画面截图" "capture.json" {
                $startText = & $unityCli command --project-path=$ProjectPath editor.playmode start --format json 2>&1 | Out-String
                Set-Content -LiteralPath (Join-Path $runFolder "playmode-capture-start.json") -Value $startText -Encoding UTF8
                $startExitCode = $LASTEXITCODE
                if ($startExitCode -ne 0)
                {
                    Write-Output $startText
                    $global:LASTEXITCODE = $startExitCode
                    return
                }

                Start-Sleep -Seconds 3
                $captureText = & $unityCli command --project-path=$ProjectPath capture game_view --output-path (Join-Path $runFolder "game-view.png") --format json 2>&1 | Out-String
                $captureExitCode = $LASTEXITCODE
                & $unityCli command --project-path=$ProjectPath editor.playmode stop --format json | Set-Content -LiteralPath (Join-Path $runFolder "playmode-capture-stop.json") -Encoding UTF8
                Write-Output $captureText
                $global:LASTEXITCODE = $captureExitCode
            } | Out-Null
        }

        Invoke-CapturedStep "修改后项目快照" "after.json" {
            & $unityCli command --project-path=$ProjectPath gundemo project_snapshot --format json
        } -Required | Out-Null
    }

    Invoke-CapturedStep "Console日志" "console.json" {
        & $unityCli command --project-path=$ProjectPath console get --tail 200 --level warn --format json
    } | Out-Null

    $statusText = if ($hasFailure) { "失败" } else { "通过" }
    $stepLines = foreach ($step in $steps)
    {
        $mark = if ($step.Passed) { "通过" } else { "失败" }
        "| $($step.Name) | $mark | $($step.ExitCode) | $($step.DurationSeconds) 秒 | $($step.Evidence) |"
    }

    $report = @"
# Agent 开发验证报告

- 验证档位：$Profile
- 验证结果：$statusText
- 工程路径：$ProjectPath
- 生成时间：$(Get-Date -Format "yyyy-MM-dd HH:mm:ss zzz")

## 执行结果

| 步骤 | 结果 | 退出码 | 耗时 | 证据 |
| --- | --- | ---: | ---: | --- |
$($stepLines -join "`n")

## 问题描述

请根据失败步骤及对应证据填写；自动化结果只说明可重复检查是否通过。

## 复现步骤

使用相同档位重新运行 Assets/CLI/Workflow/Invoke-AgentWorkflow.ps1。

## 日志证据

参见本目录中的 compile.json、测试 JSON、console.json 和 verification.json。

## 怀疑文件或行

由 Agent 根据编译错误、测试堆栈和 Console 堆栈填写。

## 建议修复

由 Agent 在确认根因后填写，不以猜测代替证据。

## 验证结果

$statusText

## 人工判断

由用户填写。手感、趣味性、画面效果和数值公平性不由自动化结果裁决。
"@
    Set-Content -LiteralPath (Join-Path $runFolder "bug-report.md") -Value $report -Encoding UTF8

    $steps | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $runFolder "workflow.json") -Encoding UTF8

    Write-Output "验证结果: $statusText"
    Write-Output "证据目录: $runFolder"
    if ($hasFailure)
    {
        exit 1
    }
}
finally
{
    Pop-Location
}
