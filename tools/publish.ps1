param(
    [ValidateSet("win-x64")]
    [string]$RuntimeIdentifier = "win-x64"
)

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $projectRoot "src\ZapretManager.App\ZapretManager.App.csproj"
$artifactsRoot = Join-Path $projectRoot "artifacts\ZapretManager"
$outputDirectory = Join-Path $artifactsRoot $RuntimeIdentifier
$expectedExecutable = Join-Path $outputDirectory "Zapret Manager.exe"

$resolvedProjectRoot = [System.IO.Path]::GetFullPath($projectRoot)
$resolvedOutput = [System.IO.Path]::GetFullPath($outputDirectory)
if (-not $resolvedOutput.StartsWith($resolvedProjectRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Publish output is outside the repository: $resolvedOutput"
}

if (Test-Path -LiteralPath $resolvedOutput) {
    Remove-Item -LiteralPath $resolvedOutput -Recurse -Force
}

dotnet publish $projectPath `
    --configuration Release `
    --runtime $RuntimeIdentifier `
    --self-contained true `
    --output $resolvedOutput `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    -p:DebugType=None `
    -p:DebugSymbols=false

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if (-not (Test-Path -LiteralPath $expectedExecutable -PathType Leaf)) {
    throw "The expected executable was not produced: $expectedExecutable"
}

$resolvedExecutable = [System.IO.Path]::GetFullPath($expectedExecutable)
$unexpectedFiles = Get-ChildItem -LiteralPath $resolvedOutput |
    Where-Object { $_.FullName -ne $resolvedExecutable }
if ($unexpectedFiles) {
    $names = $unexpectedFiles.Name -join ", "
    throw "Single-file publish contains unexpected files: $names"
}

$sizeMiB = [Math]::Round((Get-Item -LiteralPath $expectedExecutable).Length / 1MB, 2)
Write-Host "Build is ready: $expectedExecutable ($sizeMiB MiB)"
