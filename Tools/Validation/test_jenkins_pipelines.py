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
    boolean enabled = true
    def script

    PipelineHarness(File source) {
        def binding = new Binding([env: env, params: params, currentBuild: currentBuild])
        binding.setVariable('pipeline', { Closure c -> c.call() })
        binding.setVariable('agent', { Closure c -> })
        binding.setVariable('options', { Closure c -> })
        binding.setVariable('environment', { Closure c -> c.call() })
        binding.setVariable('stages', { Closure c -> c.call() })
        binding.setVariable('post', { Closure c -> })
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
        binding.setVariable('archiveArtifacts', { Map options -> })
        binding.setVariable('withCredentials', { List options, Closure c -> c.call() })
        binding.setVariable('string', { Map options -> options })
        binding.setVariable('withEnv', { List values, Closure c -> c.call() })
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
    def path = 'Assets/App/Editor/Build/Configs/AddressablesProfileSettings.yaml'
    ['Dev', 'Release'].each { kind ->
        def route = kind.toLowerCase()
        check(h.script.versionedAssetPath(path, 'Build' + kind, '3.0.2', kind) == 'ServerData/' + route + '/v/3.0.2/', 'build route ' + kind)
        check(h.script.versionedAssetPath(path, 'Upload' + kind, '3.0.2', kind) == 's3://mcombat/' + route + '/v/3.0.2', 'upload route ' + kind)
    }
    def valid = h.files[path]
    h.files[path] = valid.replace('ServerData/dev/v/{version}/', 'ServerData/dev/v/3.0.1/')
    rejects({ h.script.versionedAssetPath(path, 'BuildDev', '3.0.2', 'Dev') }, 'explicit versions')
    h.files[path] = valid.replace('s3://mcombat/dev/v/{version}', 's3://mcombat/release/v/{version}')
    rejects({ h.script.versionedAssetPath(path, 'UploadDev', '3.0.2', 'Dev') }, 'dev/v/{version}')
    h.files[path] = valid.replace('ServerData/dev/v/{version}/', 'ServerData/dev/v/{version}/../')
    rejects({ h.script.versionedAssetPath(path, 'BuildDev', '3.0.2', 'Dev') }, 'dev/v/{version}')
    h.files['ProjectSettings/ProjectSettings.asset'] = 'PlayerSettings:\n  bundleVersion: 3.0.2-beta\n'
    rejects({ h.script.projectVersion() }, 'numeric dotted version')
    h.files['ProjectSettings/ProjectSettings.asset'] = 'bundleVersion: 3.0.2\n  bundleVersion: 3.0.1\n'
    rejects({ h.script.projectVersion() }, 'exactly one bundleVersion')
}

def ios = new PipelineHarness(iosFile)
ios.params.BUILD_KIND = 'Dev'
ios.params.AssetKind = 'Release'
ios.params.remove('AWS_PROFILE')
ios.runStage('Checkout')
ios.runStage('Preflight')
check(ios.env.ASSET_KIND == 'Dev' && ios.env.ASSET_PROFILE == 'dev', 'iOS Dev ignores conflicting legacy AssetKind')
check(ios.env.ASSET_BUILDPATH == 'ServerData/dev/v/3.0.2/', 'iOS Dev routes to current version')
check(ios.env.AWS_PROFILE == 'mcombatDev', 'iOS AWS profile has compatible default')
check(!ios.stages.containsKey('Addressables'), 'iOS has no independent pre-export asset build')
ios.shells.clear()
ios.runStage('Unity Export')
check(ios.shells.size() == 1 && ios.shells[0].contains('Cocone.ProjectP3.Client.Build'), 'iOS exports once even with legacy buildAsset false')
check(ios.shells[0].contains('-buildKind "$BUILD_KIND"') && ios.shells[0].contains('-assetProfile "$ASSET_PROFILE"'), 'Unity receives linked build and asset environment')

def release = new PipelineHarness(iosFile)
release.params.AssetKind = 'Dev'
release.runStage('Preflight')
check(release.env.ASSET_PROFILE == 'release' && release.env.ASSET_BUILDPATH == 'ServerData/release/v/3.0.2/', 'iOS Release ignores conflicting legacy AssetKind')
def configPath = 'Assets/App/Editor/Build/Configs/AddressablesProfileSettings.yaml'
release.files[configPath] = release.files[configPath].replace('ProfileRelease: release', 'ProfileRelease: dev')
rejects({ release.runStage('Preflight') }, 'must match BUILD_KIND')

['Publish Matching Addressables', 'Verify Published Addressables', 'Upload App Store'].each { stage ->
    [[BUILD_KIND: 'Dev'], [VALIDATE_ONLY: true], [SIGNING_VALIDATE_ONLY: true]].each { overrides ->
        def h = new PipelineHarness(iosFile)
        h.params.putAll(overrides)
        h.runStage(stage)
        check(h.shells.isEmpty(), stage + ' skipped for ' + overrides)
    }
}
def normal = new PipelineHarness(iosFile)
normal.runStage('Publish Matching Addressables')
check(normal.shells.size() == 1 && normal.shells[0].contains('Tools/publish_ios_addressables.py') && normal.shells[0].contains('--publish'), 'normal Release publishes matching export snapshot')
normal.runStage('Verify Published Addressables')
check(normal.shells.size() == 2 && normal.shells[1].contains('--check-remote'), 'normal Release verifies published catalog')
def ordered = normal.stages.keySet().toList()
check(ordered.indexOf('Unity Export') < ordered.indexOf('Archive Matching Addressables') && ordered.indexOf('Archive Matching Addressables') < ordered.indexOf('Publish Matching Addressables'), 'archive follows player export and precedes publish')
check(ordered.indexOf('Publish Matching Addressables') < ordered.indexOf('Verify Published Addressables') && ordered.indexOf('Verify Published Addressables') < ordered.indexOf('Upload App Store'), 'remote pair verification gates App Store upload')

def assets = new PipelineHarness(assetsFile)
assets.params.AssetKind = 'Dev'
assets.runStage('Checkout')
assets.runStage('Preflight')
check(assets.env.ASSET_PROFILE == 'dev' && assets.env.UPLOAD_S3_ADDRESS == 's3://mcombat/dev/v/3.0.2', 'independent Dev assets use current project version')
assets.params.AssetKind = 'Release'
rejects({ assets.runStage('Checkout') }, 'matching iOS player build')
assets.params.VALIDATE_ONLY = true
assets.runStage('Checkout')
assets.runStage('Preflight')
check(assets.env.ASSET_PROFILE == 'release', 'standalone Release asset validation stays available')
assets.runStage('Upload iOS Assets')
check(assets.shells.every { !it.contains('aws s3 cp') }, 'asset validation skips publication')
println 'PASS: ' + checks + ' pipeline version/routing/publishing checks'
def interpretedShells = []
[iosFile, assetsFile].each { source ->
    def h = new PipelineHarness(source)
    h.params.AssetKind = 'Dev'
    h.params.ANDROID = true
    h.stages.keySet().each { h.runStage(it) }
    interpretedShells.addAll(h.shells)
}
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
