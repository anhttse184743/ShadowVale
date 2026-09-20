param(
    [string]$UnityEditor = 'D:\6000.3.24f1\Editor\Unity.exe',
    [switch]$RunTests
)
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
if (-not (Test-Path -LiteralPath $UnityEditor)) { throw "Unity Editor not found: $UnityEditor" }
$logs = Join-Path $project 'Logs'
New-Item -ItemType Directory -Force -Path $logs | Out-Null
& $UnityEditor -batchmode -quit -projectPath $project -executeMethod ShadowVale.Map01.Editor.ForestMapBuilder.Build -logFile (Join-Path $logs 'Map01-build.log')
if ($LASTEXITCODE -ne 0) { throw "Map 1 build failed ($LASTEXITCODE). See Logs/Map01-build.log. Close the project in Unity before running CLI; ensure Unity Hub has an active Editor license." }
if ($RunTests) {
    & $UnityEditor -batchmode -projectPath $project -runTests -testPlatform EditMode -testFilter ShadowVale.Map01.Tests -testResults (Join-Path $logs 'Map01-tests.xml') -logFile (Join-Path $logs 'Map01-tests.log')
    if ($LASTEXITCODE -ne 0) { throw "Map 1 tests failed ($LASTEXITCODE). See Logs/Map01-tests.log." }
}
Write-Host 'Map 1 generated. Open Assets/_Project/Scenes/Maps/Map01_ForestFootprints.unity.'
