"""Example: restore a MajdataPlay install to its pre-patch state.

Run this while the game is closed. It restores the original managed assembly,
settings.json and the four language packs from a backup folder that YOU must
prepare yourself -- this repository intentionally ships no game binaries and
no personal data.

Usage:
    python restore.py "C:\\path\\to\\MajdataPlay_Build-LATEST"
    python restore.py --backup "C:\\path\\to\\backup" "C:\\path\\to\\game"

Charts, skins and score databases are never touched.
"""
import argparse
import os
import shutil
import sys

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument('game', nargs='?', help='MajdataPlay install directory')
parser.add_argument('--backup', default=os.path.dirname(os.path.abspath(__file__)),
                    help='folder holding your own original DLL/settings/lang packs')
args = parser.parse_args()

GAME = args.game or input('MajdataPlay install directory: ').strip().strip('"')
BACKUP = args.backup

REQUIRED = ('Assembly-CSharp.dll', 'settings.json',
            'zh-CN.json', 'zh-TW.json', 'en-US.json', 'ja-JP.json')
missing = [name for name in REQUIRED
           if not os.path.isfile(os.path.join(BACKUP, name))]
if missing:
    sys.exit('Missing backup files in %s:\n  %s\n\n'
             'Keep your own copy of the original DLL, settings.json and '
             'language packs; they are not distributed with this repository.'
             % (BACKUP, ', '.join(missing)))


def restore(src, dst):
    target = os.path.join(GAME, dst)
    os.makedirs(os.path.dirname(target), exist_ok=True)
    shutil.copy2(os.path.join(BACKUP, src), target)
    print('restored', dst)


restore('Assembly-CSharp.dll', 'MajdataPlay_Data/Managed/Assembly-CSharp.dll')
restore('settings.json', 'settings.json')
for name in ('zh-CN', 'zh-TW', 'en-US', 'ja-JP'):
    restore(name + '.json', 'MajdataPlay_Data/StreamingAssets/Langs/' + name + '.json')
print('DONE - original build restored')
