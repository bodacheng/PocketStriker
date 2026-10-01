# PocketStriker Jenkins 管线

更新日期：2026-10-01。

## 与 MCombat 相同的独立构建方式

`CustomIOSBuild_V` 和 `AssetDev_V` 可以按任意顺序构建。程序编译不读取已发布资源，不下载 `player-bootstrap.zip`，也不要求先执行资源任务。

- `CustomIOSBuild_V`：检出程序、Unity 导出、CocoaPods、Xcode 编译、签名和 IPA 导出。Unity 使用 `BuildWithPlayer` 在本地构建 Addressables，生成该程序的初始 catalog、包内 bundle 和 IL2CPP 类型保留文件。程序任务不上传资源、不使用 AWS；正常 Release 模式仍按原配置上传 App Store。
- `AssetDev_V`：独立构建 Dev 或 Release 资源并上传远端 bundle、catalog 和 hash。默认 `IOS=true`、`ANDROID=false`；Android 保留为可选平台。不再打包或发布程序 bootstrap。

这与 MCombat 的 `DevBuild/ios/build.groovy`、`Design/build_asset_upload_custom_server.groovy` 和 `BuildWithPlayer` 设置一致。程序构建会在本地运行资源编译器，但不会触发、等待或发布资源任务。两条任务使用各自工作区。

## 资源地址与版本

两者共用 `MCombat/Version Sync` 设置的项目版本。程序的 `BUILD_KIND` 选择 `dev` 或 `release` profile；资源任务的 `AssetKind` 选择发布环境。地址为：

```text
https://mcombat.s3.ap-northeast-1.amazonaws.com/{dev|release}/v/{项目版本}/{iOS|Android}/
```

程序中的 Addressables profile 确定该地址，版本来自 `PlayerSettings.bundleVersion`，平台来自当前 build target。catalog 名称同样使用项目版本。构建不会根据线上“最新”资源反向改变地址。

程序启动后，Addressables 检查固定地址的远端 catalog，再从 catalog 解析 asset address 的下载位置。**程序可以先编译，资源后编译；使用远端资源前需要将对应环境、版本和平台的资源发布到上述地址。**

模型 `Units` 组按 MCombat 配置改为远端，并采用包含内容 hash 的 bundle 文件名。新 catalog 引用的模型与共享脚本 bundle 来自同一次资源构建，避免旧包内模型引用新远端脚本。其余资源组保留既有本地／远端配置。

共享 MonoScript bundle 使用 `DefaultGroupGuid` 命名，避免 Jenkins 工作区目录名不同导致脚本引用变化。版本同步和构建校验会检查此设置、`BuildWithPlayer`、远端 catalog 启动检查和模型的远端配置。

## 操作与发布

1. 用 `MCombat/Version Sync` 设置版本，保存、提交并推送项目代码。
2. 按需要运行程序或资源任务，无固定编译顺序。两个任务选择相同的环境、版本和兼容的代码／资源。
3. 新程序需要使用远端资源时，确认资源任务已发布对应地址。

资源上传顺序为 bundle、catalog、最后 catalog hash。catalog/hash 使用 `no-cache`；旧 bundle 不删除。新工作区或清空 `Library` 后，程序任务仍可直接从源码生成启动数据，无需已有资源任务产物。

这次模型从包内切换至远端，需要安装一次新程序并发布对应资源；旧 build 31 无法靠重新下载替换包内模型。此后的程序和资源任务可以独立构建。脚本类型或包内资源不兼容的变更仍需新程序；常规远端内容更新使用兼容版本。

## Jenkins 入口与验证模式

管线源码位于 `/Users/daisei/MCombat_tool/pipeline_script/PocketStriker/`；两个任务从工具仓库 `master` 读取：

- [CustomIOSBuild_V](http://localhost:8080/job/CustomIOSBuild_V/)：`ios.groovy`。
- [AssetDev_V](http://localhost:8080/job/AssetDev_V/)：`assets.groovy`。

项目代码与工具仓库的修改都需提交、推送后才会被 Jenkins 检出使用。任务禁止同任务并发，Unity 固定为 `6000.5.1f1`。构建前检查编辑器版本、平台模块及 Metal 工具链。

- 程序 `VALIDATE_ONLY=true`：自行生成 Addressables 启动数据、Unity 导出、CocoaPods 和未签名原生编译；跳过 IPA 导出与上传。
- 程序 `SIGNING_VALIDATE_ONLY=true`：签名归档与 IPA 导出；跳过 App Store 验证和上传。
- 资源 `VALIDATE_ONLY=true`：构建资源；跳过 S3 上传。

旧程序参数 `AssetKind` 和 `buildAsset` 不参与程序管线，程序环境统一由 `BUILD_KIND` 选择。Unity 自动在程序导出中构建所需资源，因此不必另设先行资源阶段。资源任务继续使用 `AWS_PROFILE` 与可选 `AWS_CACHE`。

## 回归检查

```bash
python3 Tools/Validation/test_jenkins_pipelines.py --tool-repo /Users/daisei/MCombat_tool
bash Tools/validate_unity.sh check ios
bash Tools/validate_unity.sh compile ios
```

Unity 的 `PocketStrikerVersionValidation.Validate` 检查版本和资源配置。Groovy 回归执行实际管线的受控 DSL，覆盖固定环境路由、独立资源发布、程序无 AWS／资源上传、发布顺序、验证模式和 Bash 语法。

本次清除 Addressables 启动缓存后，先运行实际 `Client.Build` 导出 Release iOS，再运行独立 `BuildAddressableAssets.BatchBuild` 清理构建，均成功。两次的 81 个启动数据文件（含 77 个本地 bundle）和 420 个远端文件逐字节一致；程序内无模型 bundle，19 个远端模型都引用匹配的共享脚本 CAB。该导出的 Xcode Release 无签名编译、版本配置回归、80 项管线检查和 29 个 Bash 语法检查均通过。诊断报告保存在 `Logs/Revival/MCombatPipeline/build-order-report.json`。本次尚未发布资源或安装新程序验证真机。

## 2026-10-01 模型失败诊断

build 31 在资源 #13 前完成。真机下载新 catalog 后，`human/fire_golem`、`human/robot3` 报 `Unit model is missing root OutsideDataLink` 和 missing script。该类型已经保留在 linker 和 IL2CPP 中。

旧 MonoScript 命名取工作区目录名：程序与资源任务生成了不同的脚本 bundle。build 31 本地模型引用 `CAB-5d02b076c6a0b4706b4985fd9f685aa8`，#13 catalog 加载 `CAB-a46040fdba52065f5f6d0bc3c37c7bea`，因此脚本引用无法解析。#13 与 build 31 的 96 个本地 bundle 中有 52 个不同。

此前新增的远端 bootstrap 下载机制另外引入了编译顺序依赖，现已移除。早先 #28/#29 的 bootstrap 下载失败及 #30 的补齐验证仅是该机制的历史记录，不再是构建前置条件。

诊断工具 `verify_ios_addressables_pair.py`、`verify_addressables_local_compatibility.py` 和 `package_addressables_bootstrap.py` 保留供核对历史产物使用；它们不参与当前程序构建或资源发布。

## 签名

iOS Release 使用 `PocketStriker App Store 2026-09-27` profile，Dev 使用 `PocketStriker Ad Hoc 2026-09-27`，均包含 Sign In with Apple；到期日为 2027-03-13 UTC。真机测试需确保设备已包含在所选 profile 中。
