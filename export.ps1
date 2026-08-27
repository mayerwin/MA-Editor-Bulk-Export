# Bulk Export Tool For Microsoft Advertising Editor - PowerShell Launcher
[CmdletBinding()]
param (
    [switch]$All,
    [string]$Account,
    [string]$Output,
    [switch]$List,
    [string]$EditorPath
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projPath = Join-Path $scriptDir "src\MAEditorBulkExport.csproj"

$argsList = @()
if ($All) { $argsList += "--all" }
if ($Account) { $argsList += "--account"; $argsList += $Account }
if ($Output) { $argsList += "--output"; $argsList += $Output }
if ($List) { $argsList += "--list" }
if ($EditorPath) { $argsList += "--editor-path"; $argsList += $EditorPath }

dotnet run --project $projPath -- $argsList
