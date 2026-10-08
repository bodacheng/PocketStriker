#!/usr/bin/env python3
"""Install only Editor capture changes into an existing isolated /tmp copy.
Does not copy a project, run Unity, or alter the source repository.
"""
import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import re
from datetime import datetime, timezone

parser = argparse.ArgumentParser()
parser.add_argument('--project', required=True)
parser.add_argument('--source', default='/Users/daisei/PocketStriker')
args = parser.parse_args()
project = Path(args.project).resolve()
source = Path(args.source).resolve()
if not str(project).startswith('/private/tmp/') and not str(project).startswith('/tmp/'):
    raise SystemExit('Refusing to edit a project outside /tmp.')
if project == source or not (project / 'ProjectSettings/ProjectVersion.txt').is_file():
    raise SystemExit('An existing isolated Unity project is required.')
editor = project / 'Assets/Editor'
target = editor / 'PocketStrikerUILiveReview.cs'
original = target.read_text()
existing_backup = project / 'StoreCaptureOriginalUILiveReview.cs.txt'
if 'static async UniTask StoreRun()' in original or 'Run() => StoreRun()' in original:
    if not existing_backup.is_file(): raise SystemExit('Original core backup missing.')
    original = existing_backup.read_text()

def method_span(text, signature):
    start = text.index(signature)
    opening = text.index('{', start)
    depth = 0
    quote = None
    line_comment = False
    block_comment = False
    escape = False
    i = opening
    while i < len(text):
        c = text[i]
        n = text[i+1] if i+1 < len(text) else ''
        if line_comment:
            if c == '\n': line_comment = False
        elif block_comment:
            if c == '*' and n == '/': block_comment = False; i += 1
        elif quote:
            if escape: escape = False
            elif c == '\\': escape = True
            elif c == quote: quote = None
        elif c == '/' and n == '/': line_comment = True; i += 1
        elif c == '/' and n == '*': block_comment = True; i += 1
        elif c in ['"', "'"]: quote = c
        elif c == '{': depth += 1
        elif c == '}':
            depth -= 1
            if depth == 0: return start, i+1
        i += 1
    raise ValueError('Unbalanced method: ' + signature)

start, end = method_span(original, 'static async UniTask Run()')
patched = original[:start] + 'static UniTask Run() => StoreRun();' + original[end:]
# Re-subscribe after both entry and exit domain reloads. SessionState contains
# the saved preferences, and completed reports are recovered from sanitized JSON.
ctor_start, ctor_end = method_span(patched, 'static PocketStrikerUILiveReview()')
patched = patched[:ctor_end-1] + '    StoreReattach();\n    ' + patched[ctor_end-1:]
# Remove unrelated executable review routes from this temporary core file.
for signature in ['static async UniTask Adventure()', 'static async UniTask Arena()',
                  'static async UniTask Collection()', 'static async UniTask Gacha()',
                  'static async UniTask Settings()', 'static async UniTask Mail()',
                  'static async UniTask Battle()', 'static async UniTask SharedModals()']:
    a, b = method_span(patched, signature)
    patched = patched[:a] + patched[b:]
a, b = method_span(patched, 'static async UniTask Home()')
patched = patched[:a] + '''static async UniTask Home()
    {
        await DismissStartupModals();
        if (Step == MainSceneStep.FrontPage && Ready && Layer<FrontLayer>() != null)
        { await Page<FrontLayer>(MainSceneStep.FrontPage, 30); return; }
        await Wait(() => Ready && Layer<LowerMainBar>() != null, 20, "native home navigation readiness");
        await Click(Tab("playTab")); await Page<FrontLayer>(MainSceneStep.FrontPage, 45);
        await DismissStartupModals(); await Page<FrontLayer>(MainSceneStep.FrontPage, 15);
    }''' + patched[b:]
patched, count = re.subn(r'static readonly string\[\] Inventory\s*=\s*\{.*?\};',
    'static readonly string[] Inventory = { "home", "skill-editor", "adventure-preparation", "random-boss", "battle-hud" };', patched, count=1, flags=re.S)
assert count == 1
old = 'SessionState.SetString(Key + ".Output", "Logs/UIArt/Live/" + label);'
assert old in patched
patched = patched.replace(old, 'SessionState.SetString(Key + ".Output", StoreOutput);')
old = 'static async UniTask Screenshot(string name)\n    {\n        if (finishing) return;'
assert old in patched
patched = patched.replace(old, '''static async UniTask Screenshot(string name)
    {
        // Startup dialogs, failure overlays and every non-whitelist output are omitted.
        if (!Inventory.Contains(name) || finishing) return;
        Require(!BannerSimulation, "Simulated banner capture is prohibited.");
        if (name == "battle-hud") Require(!FightLogger.value.GameOver.Value && Layer<CommonFightResult>() == null, "Battle ended before the capture frame.");''')
old = 'Require(width == Screen.width && height == Screen.height, "Screenshot dimensions differ from the live backbuffer.");'
assert old in patched
patched = patched.replace(old, old + '\n        StoreRecordScreenshot(path, width, height);')
patched = patched.replace('5, "native screenshot " + name)', '20, "native screenshot " + name)')
# Let the editor stop, restore scenes and restore AppSetting's original bytes
# before any automated process exit. Never use the original early batch Exit.
old = 'if (Application.isBatchMode) EditorApplication.Exit(report.passed ? 0 : 1);\n        else EditorApplication.isPlaying = false;'
assert old in patched
patched = patched.replace(old, 'EditorApplication.isPlaying = false;')

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()
def git(*cmd): return subprocess.check_output(['git','-C',str(source),*cmd], text=True).strip()
tracked_changes = git('diff','--name-only','HEAD').splitlines()
untracked = git('ls-files','--others','--exclude-standard').splitlines()
dirty = []
for name in sorted(set(tracked_changes + untracked)):
    if name.startswith(('Assets/','Packages/','ProjectSettings/','Tools/','UserSettings/')):
        p = source / name
        dirty.append({'path':name,'sha256':sha(p) if p.is_file() else None,'untracked':name in untracked})
manifest = {
 'generatedUtc': datetime.now(timezone.utc).isoformat(),
 'captureType':'editor-current-source39-plus-dirty',
 'physicalDeviceCapture':False,
 'sourceGitCommit':git('rev-parse','HEAD'),
 'sourceGitDirtyFiles':dirty,
 'sourceScope':'Current local copy, including unsubmitted download/loading changes. No Loading screenshot is whitelisted.',
 'sourceCoreSha256':hashlib.sha256(original.encode()).hexdigest(),
 'patchedCoreSha256':hashlib.sha256(patched.encode()).hexdigest(),
 'runnerSha256':sha(Path(__file__).with_name('PocketStrikerUILiveReview.StoreCapture.cs')),
 'allowedPages':['home','skill-editor','adventure-preparation','random-boss','battle-hud'],
 'allowedLanguages':['ja','en','zh'],
 'allowedDimensions':[[1290,2796],[2048,2732]],
 'actualAccountLogin':True,
 'accountFixtureUsed':False,
 'simulatedBannerUsed':False,
 'hiddenComboActivated':False,
 'runtimeNotYetExecuted':True,
 'referenceCatalogsAreNotRuntimeUsedCatalogs':True,
 'referenceIOSCatalogs':[{'path':str(p.relative_to(project)),'sha256':sha(p),'usedInCapture':False} for p in (project/'Library/com.unity.addressables/aa/iOS').glob('catalog*') if p.is_file()],
 'limitations':'Editor/local Addressables; inspect real account nickname and every output image before upload. Production login/check-in and AI battle prefetch can have ordinary server side effects.'
}
backup = project / 'StoreCaptureOriginalUILiveReview.cs.txt'
backup.write_text(original)
target.write_text(patched)
runner_target = editor / 'PocketStrikerUILiveReview.StoreCapture.cs'
runner_target.write_bytes(Path(__file__).with_name(runner_target.name).read_bytes())
(project / 'StoreCaptureSourceManifest.json').write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+'\n')
print(json.dumps({'installedProject':str(project),'runner':str(runner_target),'sourceManifest':str(project/'StoreCaptureSourceManifest.json'),'sourceCommit':manifest['sourceGitCommit'],'dirtyFileCount':len(dirty),'unityStarted':False}, ensure_ascii=False))
