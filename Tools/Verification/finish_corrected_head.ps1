param([int]$BakeProcessId = 0)
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$cloneRoot = Join-Path $PSScriptRoot 'UnityProject'
$unityEditor = 'C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
Set-Location -LiteralPath $projectRoot
if ($BakeProcessId -gt 0 -and (Get-Process -Id $BakeProcessId -ErrorAction SilentlyContinue)) {
    Write-Output 'Waiting for the corrected head decomposition to finish.'
    Wait-Process -Id $BakeProcessId
}
$resultPath = 'Tools/Colliders/HeadCorrected/head.output.json'
if (!(Test-Path -LiteralPath $resultPath)) { throw 'The corrected head decomposition did not produce an output.' }
$result = Get-Content -LiteralPath $resultPath -Raw | ConvertFrom-Json
if ($result.hulls.Count -lt 1) { throw 'The corrected head decomposition has no hulls.' }
Write-Output "Corrected CoACD result: $($result.hulls.Count) hulls."

$backup = 'Tools/Verification/BeforeHeadGeometryFix'
foreach ($name in @('head.input.json','head.output.json','head.output.json.raw.json')) {
    $previous = Join-Path 'Tools/Colliders/HeadDetailed' $name
    $backupPath = Join-Path $backup $name
    if ((Test-Path -LiteralPath $previous) -and !(Test-Path -LiteralPath $backupPath)) { Copy-Item -LiteralPath $previous -Destination $backupPath }
    foreach ($targetRoot in @($projectRoot,$cloneRoot)) {
        $destination = Join-Path $targetRoot 'Tools/Colliders/HeadDetailed'
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path 'Tools/Colliders/HeadCorrected' $name) -Destination (Join-Path $destination $name)
    }
}

# Start from the latest saved scenes and prefabs to preserve unrelated authoring changes.
$scenePaths = @('Assets/Scenes/AssemblyWorkshop.unity','Assets/Scenes/MobileAssembly.unity')
$prefabPaths = @('Assets/Prefabs/ThreeCylinderEngine_CoACD.prefab','Assets/Prefabs/CylinderHead_Precise.prefab')
foreach ($path in ($scenePaths + $prefabPaths)) { Copy-Item -LiteralPath $path -Destination (Join-Path $cloneRoot $path) }

function Invoke-Verification([string]$Method, [string]$LogName) {
    Write-Output "Running $Method"
    $logPath = Join-Path $PSScriptRoot $LogName
    $arguments = "-batchmode -force-d3d11 -projectPath `"$cloneRoot`" -executeMethod $Method -logFile `"$logPath`""
    $editorProcess = Start-Process -FilePath $unityEditor -ArgumentList $arguments -WindowStyle Hidden -PassThru
    $editorProcess.WaitForExit()
    if ($editorProcess.ExitCode -ne 0) { throw "$Method failed. Inspect $logPath" }
}
Invoke-Verification 'EngineAssembly.Editor.ValidatedEngineColliderBake.RunBatch' 'corrected-validated-colliders.log'
Invoke-Verification 'EngineAssembly.Editor.CylinderHeadColliderBake.VerifyOriginalGeometry' 'corrected-head-final.log'
Invoke-Verification 'EngineAssembly.Editor.ValvePlacementVerification.RunBatch' 'corrected-valve-final.log'

$key = 'e2e9117cfca116ed47ff4ebece6a17bfab806163a2276bd0d9dd100fe9244df0'
$colliderPaths = @("Assets/GeneratedColliders/EngineCoACDValidated/$key.asset",'Assets/GeneratedColliders/CylinderHeadExactNormalized.asset')
foreach ($path in ($scenePaths + $prefabPaths + $colliderPaths)) {
    Copy-Item -LiteralPath (Join-Path $cloneRoot $path) -Destination (Join-Path $projectRoot $path)
    $meta = Join-Path $cloneRoot ($path + '.meta')
    if (Test-Path -LiteralPath $meta) { Copy-Item -LiteralPath $meta -Destination (Join-Path $projectRoot ($path + '.meta')) }
}
foreach ($name in @('validated-colliders-report.json','head-surface-report.json','valve-placement-report.json')) {
    Copy-Item -LiteralPath (Join-Path $cloneRoot ('Tools/Verification/' + $name)) -Destination (Join-Path $PSScriptRoot $name)
}
Write-Output 'Corrected head colliders validated and saved to both scenes and both prefabs.'
