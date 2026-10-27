param(
    [string]$auditProject = 'C:\Users\Admin\.codex\worktrees\map1-boot-investigation-20261023\ShadowVale',
    [string]$auditName = 'audit-tests',
    [string]$auditFilter = 'Map02DayTwoRouteTests|Map02PerformanceTests|Map02NightTwoRouteTests|Map02ChapterMechanicsTests|Map02FinaleTests|Map02SuppliesAndCastTests|Map02DayTwoStateTests'
)
$ErrorActionPreference = 'Stop'
$auditOutput = 'D:\DO_AN_GAME\ShadowVale\Docs\Reviews\Map02_Audit_20261026'
$auditArguments = @('-batchmode', '-projectPath', ('"' + $auditProject + '"'), '-runTests', '-testPlatform', 'EditMode', '-assemblyNames', 'ShadowVale.Map01.Tests', '-testFilter', $auditFilter, '-testResults', ('"' + $auditOutput + '\' + $auditName + '.xml"'), '-logFile', ('"' + $auditOutput + '\' + $auditName + '.log"'))
$auditProcess = Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.3.24f1\Editor\Unity.exe' -ArgumentList $auditArguments -WindowStyle Hidden -PassThru
Write-Output ('Audit Unity PID: ' + $auditProcess.Id)
$auditProcess.WaitForExit()
Write-Output ('Audit Unity exit: ' + $auditProcess.ExitCode)
exit $auditProcess.ExitCode
