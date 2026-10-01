#!/usr/bin/env python3
"""Exercise the actual PocketStriker Groovy pipelines with a controlled Jenkins DSL.

No pipeline shell command, checkout, build, credential lookup, or upload is run.
Requires the Groovy jar bundled with Jenkins; also checks every Bash block with bash -n.
"""

import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile


HARNESS = r'''
class PipelineHarness {
    Map params = [UNITY_VERSION: '6000.5.1f1', BUILD_KIND: 'Release', AssetKind: 'Dev',
        machine_name: 'rudel', BRANCH: 'master', needCleanWorkspace: false,
        CLEAR_CACHE: false, developmentBuild: false, INSTALL_POD: true,
        VALIDATE_ONLY: false, SIGNING_VALIDATE_ONLY: false, IOS: true, ANDROID: false,
        AWS_PROFILE: 'mcombatDev', AWS_CACHE: false, buildAsset: false]
    Map env = [WORKSPACE: '/controlled/workspace', BUILD_NUMBER: '42']
    Map currentBuild = [:]
    Map files = [
        'ProjectSettings/ProjectSettings.asset': 'PlayerSettings:\n  bundleVersion: 3.0.2\n',
        'Assets/App/Editor/Build/Configs/AddressablesProfileSettings.yaml':
            'ProfileDev: dev\nBuildDev: ServerData/dev/v/{version}/\nUploadDev: s3://mcombat/dev/v/{version}\n' +
            'ProfileRelease: release\nBuildRelease: ServerData/release/v/{version}/\nUploadRelease: s3://mcombat/release/v/{version}\n',
        'Assets/App/Editor/Build/Configs/DevBuildSettings.yaml': 'cfBundleName: controlledDev\n',
        'Assets/App/Editor/Build/Configs/ReleaseBuildSettings.yaml': 'cfBundleName: controlled\n']
    Map stages = new LinkedHashMap()
    List shells = []
    List archives = []
    List environmentScopes = []
    boolean enabled = true
    def script
    Closure postSteps

    PipelineHarness(File source) {
        def binding = new Binding([env: env, params: params, currentBuild: currentBuild])
        binding.setVariable('pipeline', { Closure c -> c.call() })
        binding.setVariable('agent', { Closure c -> })
        binding.setVariable('options', { Closure c -> })
        binding.setVariable('environment', { Closure c -> c.call() })
        binding.setVariable('stages', { Closure c -> c.call() })
        binding.setVariable('post', { Closure c -> postSteps = c })
        binding.setVariable('always', { Closure c -> c.call() })
        binding.setVariable('stage', { String name, Closure c -> stages[name] = c })
        binding.setVariable('steps', { Closure c -> if (enabled) c.call() })
        binding.setVariable('script', { Closure c -> c.call() })
        binding.setVariable('when', { Closure c -> c.call() })
        binding.setVariable('expression', { Closure c -> enabled = c.call() })
        binding.setVariable('readFile', { String path ->
            if (!files.containsKey(path)) throw new IllegalStateException('Unexpected fixture read: ' + path)
            files[path]
        })
        binding.setVariable('error', { String message -> throw new IllegalStateException(message) })
        binding.setVariable('fileExists', { String path -> true })
        binding.setVariable('dir', { String path, Closure c -> c.call() })
        binding.setVariable('deleteDir', { -> })
        binding.setVariable('checkout', { Map options -> })
        binding.setVariable('archiveArtifacts', { Map options -> archives.add(options) })
        binding.setVariable('withCredentials', { List options, Closure c -> c.call() })
        binding.setVariable('string', { Map options -> options })
        binding.setVariable('withEnv', { List values, Closure c ->
            environmentScopes.add(values.collect { it.toString() })
            c.call()
        })
        binding.setVariable('sh', { command ->
            def value = command instanceof Map ? command.script : command
            shells.add(value.toString())
            command instanceof Map && command.returnStdout ? 'build_ios/IPA/controlled.ipa\n' : null
        })
        script = new GroovyShell(binding).parse(source)
        script.run()
        ['LANG', 'LC_ALL', 'UNITY_PATH', 'BUILD_CONFIG_DIR', 'OUTPUT_PATH', 'ARCHIVE_PATH',
            'IPA_OUTPUT_PATH', 'DERIVED_DATA_PATH'].each { name ->
            if (binding.hasVariable(name)) env[name] = binding.getVariable(name)
        }
    }

    void runStage(String name) {
        enabled = true
        stages[name].call()
    }

    void runPost() {
        postSteps.call()
    }
}

int checks = 0
def check = { boolean value, String description ->
    if (!value) throw new AssertionError(description)
    checks++
}
def rejects = { Closure action, String expected ->
    try { action.call() } catch (IllegalStateException e) {
        check(e.message.contains(expected), 'failure explains ' + expected)
        return
    }
    throw new AssertionError('Expected rejection: ' + expected)
}

def iosFile = new File(args[0])
def assetsFile = new File(args[1])
[iosFile, assetsFile].each { source ->
    def h = new PipelineHarness(source)
    check(h.script.projectVersion() == '3.0.2', source.name + ' reads project version')
    h.files['ProjectSettings/ProjectSettings.asset'] = 'PlayerSettings:\n  bundleVersion: 3.0.2-beta\n'
    rejects({ h.script.projectVersion() }, 'numeric dotted version')
    h.files['ProjectSettings/ProjectSettings.asset'] = 'bundleVersion: 3.0.2\n  bundleVersion: 3.0.1\n'
    rejects({ h.script.projectVersion() }, 'exactly one bundleVersion')
}

def configPath = 'Assets/App/Editor/Build/Configs/AddressablesProfileSettings.yaml'
def assets = new PipelineHarness(assetsFile)
['Dev', 'Release'].each { kind ->
    def route = kind.toLowerCase()
    check(assets.script.versionedAssetPath(configPath, 'Build' + kind, '3.0.2', kind) == 'ServerData/' + route + '/v/3.0.2/', 'build route ' + kind)
    check(assets.script.versionedAssetPath(configPath, 'Upload' + kind, '3.0.2', kind) == 's3://mcombat/' + route + '/v/3.0.2', 'upload route ' + kind)
}
def valid = assets.files[configPath]
assets.files[configPath] = valid.replace('ServerData/dev/v/{version}/', 'ServerData/dev/v/3.0.1/')
rejects({ assets.script.versionedAssetPath(configPath, 'BuildDev', '3.0.2', 'Dev') }, 'explicit versions')
assets.files[configPath] = valid.replace('s3://mcombat/dev/v/{version}', 's3://mcombat/release/v/{version}')
rejects({ assets.script.versionedAssetPath(configPath, 'UploadDev', '3.0.2', 'Dev') }, 'dev/v/{version}')
assets.files[configPath] = valid.replace('ServerData/dev/v/{version}/', 'ServerData/dev/v/{version}/../')
rejects({ assets.script.versionedAssetPath(configPath, 'BuildDev', '3.0.2', 'Dev') }, 'dev/v/{version}')

def interpretedShells = []
['Dev', 'Release'].each { kind ->
    def ios = new PipelineHarness(iosFile)
    ios.params.BUILD_KIND = kind
    ios.params.AssetKind = kind == 'Dev' ? 'Release' : 'Dev'
    ios.params.remove('AWS_PROFILE')
    // The pipeline selects a profile; Unity generates the player's content locally.
    // Pipeline-only upload paths and credentials are unnecessary for player exports.
    ios.files[configPath] = 'ProfileDev: dev\nProfileRelease: release\n'
    ios.stages.keySet().each { ios.runStage(it) }
    ios.runPost()
    check(ios.env.ASSET_PROFILE == kind.toLowerCase(), 'iOS ' + kind + ' selects the matching remote profile despite legacy AssetKind')
    ['ASSET_BUILDPATH', 'UPLOAD_S3_ADDRESS', 'AWS_PROFILE'].each { key ->
        check(!ios.env.containsKey(key), 'iOS ' + kind + ' does not configure ' + key)
    }
    ['Addressables', 'Archive Matching Addressables', 'Publish Matching Addressables', 'Verify Published Addressables'].each { stage ->
        check(!ios.stages.containsKey(stage), 'iOS has no asset stage: ' + stage)
    }
    def exports = ios.shells.findAll { it.contains('Cocone.ProjectP3.Client.Build') }
    check(exports.size() == 1, 'iOS exports the program once even with legacy buildAsset false')
    check(exports[0].contains('-buildKind "$BUILD_KIND"') && exports[0].contains('-assetProfile "$ASSET_PROFILE"'), 'Unity receives matching build and remote asset profile')
    check(ios.shells.every { !(it =~ /BuildAddressableAssets|publish_ios_addressables|verify_ios_addressables_pair|\baws\b|AWS_|ServerData|addressables_/) }, 'iOS ' + kind + ' has no standalone resource build or resource publication commands')
    check(ios.archives.every { !(it.artifacts =~ /addressables|ServerData/) }, 'iOS ' + kind + ' archives program artifacts only')
    interpretedShells.addAll(ios.shells)
}

def release = new PipelineHarness(iosFile)
release.files[configPath] = release.files[configPath].replace('ProfileRelease: release', 'ProfileRelease: dev')
rejects({ release.runStage('Preflight') }, 'must match BUILD_KIND')

[[BUILD_KIND: 'Dev'], [VALIDATE_ONLY: true], [SIGNING_VALIDATE_ONLY: true]].each { overrides ->
    def h = new PipelineHarness(iosFile)
    h.params.putAll(overrides)
    h.runStage('Upload App Store')
    check(h.shells.isEmpty(), 'App Store upload skipped for ' + overrides)
}
def normal = new PipelineHarness(iosFile)
normal.runStage('Upload App Store')
check(normal.shells.size() == 1 && normal.shells[0].contains('altool --upload-app'), 'normal Release still uploads the app')

['Dev', 'Release'].each { kind ->
    def h = new PipelineHarness(assetsFile)
    h.params.AssetKind = kind
    h.params.ANDROID = true
    h.stages.keySet().each { h.runStage(it) }
    h.runPost()
    check(h.env.ASSET_PROFILE == kind.toLowerCase(), 'independent ' + kind + ' assets select the matching profile')
    check(h.env.ASSET_BUILDPATH == 'ServerData/' + kind.toLowerCase() + '/v/3.0.2/', 'independent ' + kind + ' assets build to the current version')
    check(h.env.UPLOAD_S3_ADDRESS == 's3://mcombat/' + kind.toLowerCase() + '/v/3.0.2', 'independent ' + kind + ' assets upload to the current version')
    check(h.shells.findAll { it.contains('BuildAddressableAssets.BatchBuild') }.size() == 2, kind + ' builds both selected platforms independently')
    check(h.shells.every { !it.contains('package_addressables_bootstrap') && !it.contains('player-bootstrap.zip') }, kind + ' publishes assets without preparing a player bootstrap artifact')
    def uploads = h.shells.findAll { it.contains('aws s3 cp') }
    check(uploads.size() == 2, kind + ' publishes both selected platforms independently')
    uploads.each { upload ->
        def bundle = upload.indexOf("--exclude 'catalog_*'")
        def catalog = upload.indexOf("--include 'catalog_*.bin' --include 'catalog_*.json'")
        def hash = upload.indexOf("--include 'catalog_*.hash'")
        check([bundle, catalog, hash].every { it >= 0 }, kind + ' publishes bundles, catalogs, and hashes in distinct passes')
        check(bundle < catalog && catalog < hash, kind + ' publishes bundles before catalogs, then advertises the hash last')
        check(upload.substring(upload.lastIndexOf('aws s3 cp')).contains("--include 'catalog_*.hash'"), kind + ' has no S3 upload after the catalog hash')
        check(upload.count("--cache-control 'no-cache'") == 2, kind + ' publishes mutable catalog and hash without caching')
        check(upload.contains('"${UPLOAD_S3_ADDRESS%/}/$ASSET_TARGET/"'), kind + ' uploads to the selected version and platform')
    }
    check(h.environmentScopes.count { it == ['ASSET_TARGET=iOS'] } == 2 && h.environmentScopes.count { it == ['ASSET_TARGET=Android'] } == 2, kind + ' builds and publishes each selected platform')
    interpretedShells.addAll(h.shells)

    def validation = new PipelineHarness(assetsFile)
    validation.params.AssetKind = kind
    validation.params.ANDROID = true
    validation.params.VALIDATE_ONLY = true
    validation.params.remove('AWS_PROFILE')
    validation.runStage('Checkout')
    validation.runStage('Preflight')
    validation.shells.clear()
    ['Build iOS Assets', 'Upload iOS Assets', 'Build Android Assets', 'Upload Android Assets'].each { validation.runStage(it) }
    check(validation.shells.findAll { it.contains('BuildAddressableAssets.BatchBuild') }.size() == 2, kind + ' validation still builds selected platforms')
    check(validation.shells.every { !it.contains('aws s3 cp') }, kind + ' validation skips S3 publication without AWS credentials')
    interpretedShells.addAll(validation.shells)
}
println 'PASS: ' + checks + ' pipeline version/routing/decoupling checks'
println 'BASH_SCRIPTS:' + groovy.json.JsonOutput.toJson(interpretedShells)
'''


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--tool-repo', type=Path, required=True)
    parser.add_argument('--groovy-jar', type=Path, default=Path(os.environ.get(
        'JENKINS_HOME', str(Path.home() / '.jenkins'))) / 'war/WEB-INF/lib/groovy-all-2.4.21.jar')
    args = parser.parse_args()
    sources = [args.tool_repo / 'pipeline_script/PocketStriker' / name
               for name in ('ios.groovy', 'assets.groovy')]
    if not args.groovy_jar.is_file():
        parser.error('Pass --groovy-jar pointing to the installed Jenkins Groovy jar.')
    with tempfile.TemporaryDirectory(prefix='pocketstriker-pipeline-check-') as temp:
        directory = Path(temp)
        harness = directory / 'CheckPipelines.groovy'
        harness.write_text(HARNESS)
        result = subprocess.run(['java', '-cp', str(args.groovy_jar), 'groovy.ui.GroovyMain',
                                 str(harness), *(str(source) for source in sources)],
                                check=True, capture_output=True, text=True)
        shell_records = [line.removeprefix('BASH_SCRIPTS:') for line in result.stdout.splitlines()
                         if line.startswith('BASH_SCRIPTS:')]
        if len(shell_records) != 1:
            raise RuntimeError('The controlled pipeline did not return its interpreted Bash blocks.')
        for line in result.stdout.splitlines():
            if not line.startswith('BASH_SCRIPTS:'):
                print(line)
        bash_blocks = 0
        for script in json.loads(shell_records[0]):
            shell = directory / f'block-{bash_blocks}.sh'
            shell.write_text(script)
            subprocess.run(['bash', '-n', str(shell)], check=True)
            bash_blocks += 1
        print(f'PASS: Groovy 2.4 compilation and {bash_blocks} Bash syntax checks')


if __name__ == '__main__':
    main()
