param(
    [switch]$Installer,
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectFile = Join-Path $projectRoot "LazyCatCompanion.csproj"
$publishDirectory = Join-Path $projectRoot "artifacts\publish\win-x64"

# Same lookup as run.bat: a system .NET SDK first, then the temporary SDK used in this workspace.
$dotnet = Get-Command dotnet -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1
if ($dotnet -and -not (& $dotnet --list-sdks 2>$null | Where-Object { $_ -match '^10\.' })) {
    # A dotnet on PATH with only runtimes installed can't build.
    $dotnet = $null
}

if (-not $dotnet -and (Test-Path -LiteralPath "$env:TEMP\lazycat-dotnet-sdk\dotnet.exe")) {
    $dotnet = "$env:TEMP\lazycat-dotnet-sdk\dotnet.exe"
}

if (-not $dotnet) {
    throw ".NET 10 SDK is required to build Lazy Cat Companion. Install it from https://dotnet.microsoft.com/download/dotnet/10.0 and run this script again."
}

& $dotnet publish $projectFile `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --output $publishDirectory

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

if ($Installer) {
    # @() keeps a single match a list; otherwise [0] would pick the first letter of the path.
    $compilerCandidates = @(@(
        (Get-Command ISCC.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source -First 1),
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { $_ -and (Test-Path -LiteralPath $_) })

    if (-not $compilerCandidates) {
        throw "Published the app, but Inno Setup 6 was not found. Install it from https://jrsoftware.org/isdl.php, then rerun .\build.ps1 -Installer."
    }

    $installerScript = Join-Path $projectRoot "installer\LazyCatCompanion.iss"
    $outputDirectory = Join-Path $projectRoot "artifacts\installer"
    New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
    & $compilerCandidates[0] $installerScript "/DPublishDir=$publishDirectory" "/DInstallerOutput=$outputDirectory"
    if ($LASTEXITCODE -ne 0) {
        throw "Inno Setup failed with exit code $LASTEXITCODE."
    }
}

Write-Host "Published app: $publishDirectory\LazyCatCompanion.exe"
if ($Installer) {
    Write-Host "Installer: $projectRoot\artifacts\installer\LazyCatCompanion-Setup.exe"
}
