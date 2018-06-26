param(
    $Configuration = 'Release'
)

$DotNetChannel = "Current";
$DotNetVersion = "2.1.301";
$DotNetInstallerUri = "https://dot.net/dotnet-install.ps1";

# Make sure tools folder exists
$PSScriptRoot = Split-Path $MyInvocation.MyCommand.Path -Parent
$ToolPath = Join-Path $PSScriptRoot "tools"
if (!(Test-Path $ToolPath)) {
    Write-Verbose "Creating tools directory..."
    New-Item -Path $ToolPath -Type directory | out-null
}

###########################################################################
# INSTALL .NET CORE CLI
###########################################################################

Function Remove-PathVariable([string]$VariableToRemove)
{
  $path = [Environment]::GetEnvironmentVariable("PATH", "User")
  $newItems = $path.Split(';') | Where-Object { $_.ToString() -inotlike $VariableToRemove }
  [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "User")
  $path = [Environment]::GetEnvironmentVariable("PATH", "Process")
  $newItems = $path.Split(';') | Where-Object { $_.ToString() -inotlike $VariableToRemove }
  [Environment]::SetEnvironmentVariable("PATH", [System.String]::Join(';', $newItems), "Process")
}

# Get .NET Core CLI path if installed.
$FoundDotNetCliVersion = $null;
if (Get-Command dotnet -ErrorAction SilentlyContinue) {
    $FoundDotNetCliVersion = dotnet --version;
}

if($FoundDotNetCliVersion -ne $DotNetVersion) {
    $InstallPath = Join-Path $PSScriptRoot ".dotnet"
    if (!(Test-Path $InstallPath)) {
        mkdir -Force $InstallPath | Out-Null;
    }
    (New-Object System.Net.WebClient).DownloadFile($DotNetInstallerUri, "$InstallPath\dotnet-install.ps1");
    & $InstallPath\dotnet-install.ps1 -Channel $DotNetChannel -Version $DotNetVersion -InstallDir $InstallPath;

    Remove-PathVariable "$InstallPath"
    $env:PATH = "$InstallPath;$env:PATH"
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
    $env:DOTNET_CLI_TELEMETRY_OPTOUT=1
}

###########################################################################
# RUN BUILD SCRIPT
###########################################################################

Push-Location

Invoke-Expression "dotnet restore"
Invoke-Expression "dotnet build -c $Configuration"

Get-ChildItem test -Recurse -Filter *.Tests.csproj | ForEach-Object {
    Push-Location $_.DirectoryName
    Invoke-Expression "dotnet test -c $Configuration /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=./lcov.info"
    if($LASTEXITCODE -ne 0) {
        Pop-Location;
        Pop-Location;
        exit $LASTEXITCODE;
    }
    Pop-Location;
}

Get-ChildItem src -Recurse -Filter *.csproj | ForEach-Object {
    Push-Location $_.DirectoryName
    Invoke-Expression "dotnet pack -c $Configuration -o $PSScriptRoot\artifacts"
    if($LASTEXITCODE -ne 0) {
        Pop-Location;
        Pop-Location;
        exit $LASTEXITCODE;
    }
    Pop-Location;
}

if($LASTEXITCODE -ne 0) {
    Pop-Location;
    exit $LASTEXITCODE;
}