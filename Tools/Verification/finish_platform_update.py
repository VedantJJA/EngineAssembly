"""Verify the inventory, XR grip, tutorial and display changes, then package both players."""
import json
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CLONE = ROOT / 'Tools/Verification/UnityProject'
UNITY = 'C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Unity.exe'
STATUS = ROOT / 'Tools/Verification/platform-release-status.json'


def state(stage, error=''):
    STATUS.write_text(json.dumps(dict(stage=stage, error=error,
                                     updated=time.strftime('%Y-%m-%d %H:%M:%S')), indent=2))


def accept_report(name):
    source = CLONE / 'Tools/Verification' / name
    result = json.loads(source.read_text())
    shutil.copy2(source, ROOT / 'Tools/Verification' / name)
    if not result['passed']:
        raise RuntimeError(result.get('error', name + ' failed'))
    return result


try:
    state('accepting-platform-presentation-checks')
    presentation = accept_report('platform-presentation-report.json')
    for capture in presentation['captures']:
        source = CLONE / capture['file']
        for destination in ['Tools/Verification', 'Docs/Previews']:
            (ROOT / destination).mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, ROOT / destination / source.name)
    state('preparing-verification-snapshot')
    for folder in ['Assets/Scripts', 'Assets/Shaders', 'Assets/Resources',
                   'Assets/Audio/Narration/English', 'ProjectSettings']:
        shutil.copytree(ROOT / folder, CLONE / folder, dirs_exist_ok=True)
    if '--resume' not in sys.argv:
        state('connecting-platform-narration')
        subprocess.run([UNITY, '-batchmode', '-quit', '-force-d3d11', '-projectPath', str(CLONE),
                        '-executeMethod', 'EngineAssembly.Editor.AssemblyAudioSetup.Run',
                        '-logFile', str(ROOT / 'Tools/Verification/platform-audio-connection.log')],
                       cwd=CLONE, check=True)
    accept_report('narration-connection-report.json')
    for method, report in [
        ('PlayerCollisionInventoryVerification', 'player-collision-inventory-report.json'),
        ('QuestGripPlacementVerification', 'quest-grip-placement-report.json'),
        ('SubAssemblyDropVerification', 'subassembly-drop-report.json'),
        ('QuestLiftVerification', 'quest-lift-report.json')
    ]:
        if '--resume' in sys.argv and method in ['PlayerCollisionInventoryVerification',
                                                'QuestGripPlacementVerification']:
            accept_report(report)
            continue
        state(method)
        subprocess.run([UNITY, '-batchmode', '-force-d3d11', '-projectPath', str(CLONE),
                        '-executeMethod', 'EngineAssembly.Editor.' + method + '.RunBatch',
                        '-logFile', str(ROOT / ('Tools/Verification/platform-' + method + '.log'))],
                       cwd=CLONE, check=True)
        accept_report(report)
    state('full-experience-regressions-and-release-builds')
    subprocess.run([sys.executable, str(ROOT / 'Tools/Narration/finish_display_update.py'),
                    '--repair'], cwd=ROOT, check=True)
    state('complete')
except Exception as error:
    state('failed', str(error))
    raise
