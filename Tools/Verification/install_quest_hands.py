import json,shutil,subprocess,time
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];CLONE=ROOT/'Tools/Verification/UnityProject'
while True:
    stage=json.loads((ROOT/'Tools/Verification/automatic-release-status.json').read_text())['stage']
    if stage=='failed':raise RuntimeError('Previous build failed')
    if stage=='complete':break
    time.sleep(3)
source=Path('Assets/Scripts/Editor/QuestHandsInstaller.cs');shutil.copy2(ROOT/source,CLONE/source)
subprocess.run(['C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe','-batchmode','-force-d3d11','-projectPath',str(CLONE),'-executeMethod','EngineAssembly.Editor.QuestHandsInstaller.Install','-logFile',str(ROOT/'Tools/Verification/quest-hands-install.log')],check=True)
for file in ['manifest.json','packages-lock.json']:shutil.copy2(CLONE/'Packages'/file,ROOT/'Packages'/file)
shutil.copy2(CLONE/'Tools/Verification/quest-hands-package.json',ROOT/'Tools/Verification/quest-hands-package.json')
