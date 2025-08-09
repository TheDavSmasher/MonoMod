param(
    [Parameter(Mandatory, ParameterSetName="version-bump")]
    [switch]$CmdVersion,

    [Parameter(Mandatory, Position=0, ParameterSetName="version-bump")]
    [ValidateSet('Major', 'Minor', 'Patch')]
    [string] $Bump = 'Patch',

    [Parameter(ValueFromRemainingArguments, ParameterSetName="version-bump")]
    [string[]] $Projects = @("MonoMod.Core", "MonoMod.Utils", "MonoMod.RuntimeDetour"),

    [Parameter(Mandatory, ParameterSetName="lockfile")]
    [switch]$CmdLockFile,

    [Parameter(Mandatory, ParameterSetName="lockfile")]
    [switch]$Generate = $true
)

$ErrorActionPreference = 'Stop';
Set-StrictMode -Version 3;

# move to the root
Set-Location (Join-Path $PSScriptRoot .. ..);

if ($CmdVersion)
{
    # do the version bump
    ./tools/bump_version.ps1 -BumpVersion $Bump -Projects $Projects;
    # do a forced restore
    dotnet restore --force-evaluate;
    if ($LastExitCode -ne 0) { throw "Restore failed!"; }
    # then update compat suppressions
    dotnet pack -c Release -clp:NoSummary -noAutoRsp -p:RunAnalyzers=false -p:ApiCompatGenerateSuppressionFile=true;
    if ($LastExitCode -ne 0) { throw "ApiCompat suppression update failed"; }

    Write-Output @"
commit=true
message=[BOT]: $($Bump) version bump for $($Projects -join ', ')
"@ >> $env:GITHUB_OUTPUT

    return;
}

if ($CmdLockFile && $Generate)
{
    # do a forced restore
    dotnet restore --force-evaluate;
    if ($LastExitCode -ne 0) { throw "Restore failed!"; }

    Write-Output @"
commit=true
message=[BOT]: Regenerate package lock files
"@ >> $env:GITHUB_OUTPUT

    return;
}

Write-Error "No command specified";