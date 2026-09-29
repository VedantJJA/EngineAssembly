$ErrorActionPreference = 'Stop'
$unityDir = 'C:\Program Files\Unity\Hub\Editor\6000.5.3f1\Editor\Data'
$projectDir = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
Set-Location $projectDir
New-Item -ItemType Directory -Force Tools\Verification\bin | Out-Null
$rspFiles = Get-ChildItem Library\Bee\artifacts -Recurse -Filter 'Assembly-CSharp.rsp'
$sourceRsp = $rspFiles | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (!$sourceRsp) { throw 'Unity compilation reference list not available. Open the project in Unity first.' }
$baseArgs = Get-Content $sourceRsp.FullName | Where-Object { $_ -match '^[-/]' -and $_ -notmatch '^[-/](out:|refout:|analyzer:|additionalfile:)' }
$runtime = Get-ChildItem Assets\Scripts -Recurse -Filter '*.cs' | Where-Object { $_.FullName -notmatch '\\Editor\\|\\Tests\\' }
$runtimeArgs = @($baseArgs) + '-out:"Tools/Verification/bin/Assembly-CSharp.dll"' + ($runtime | ForEach-Object { '"' + $_.FullName + '"' })
$runtimeArgs | Set-Content Tools\Verification\runtime.rsp
& "$unityDir\DotNetSdk\dotnet.exe" "$unityDir\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll" '@Tools/Verification/runtime.rsp'
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$editorRsp = Get-ChildItem Library\Bee\artifacts -Recurse -Filter 'Assembly-CSharp-Editor.rsp' | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$editorArgs = Get-Content $editorRsp.FullName | Where-Object { $_ -match '^[-/]' -and $_ -notmatch '^[-/](out:|refout:|analyzer:|additionalfile:)' -and $_ -notmatch 'Assembly-CSharp\.(ref\.)?dll' }
$editorArgs += '-reference:"Tools/Verification/bin/Assembly-CSharp.dll"'
$editorArgs += '-out:"Tools/Verification/bin/Assembly-CSharp-Editor.dll"'
$editorArgs += Get-ChildItem Assets\Scripts\Editor -Filter '*.cs' | ForEach-Object { '"' + $_.FullName + '"' }
$editorArgs | Set-Content Tools\Verification\editor.rsp
& "$unityDir\DotNetSdk\dotnet.exe" "$unityDir\DotNetSdk\sdk\8.0.318\Roslyn\bincore\csc.dll" '@Tools/Verification/editor.rsp'
exit $LASTEXITCODE
