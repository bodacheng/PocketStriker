# PocketStriker Jenkins 管线

更新日期：2026-10-01。

## 程序与资源独立构建

恢复为两个独立任务：

- `CustomIOSBuild_V`：检出程序、Unity 导出、CocoaPods、Xcode 编译、签名、IPA 导出和 App Store 上传。**不编译或发布 Addressables**，不使用 AWS，也不要求资源来自同一次程序构建。
- `AssetDev_V`：独立构建并发布 Dev 或 Release 的资源。默认 `IOS=true`、`ANDROID=false`；Android 保留为可选平台。

两者共用 `MCombat/Version Sync` 设置的项目版本。程序的 `BUILD_KIND` 决定使用 `dev` 或 `release` 地址；资源任务的 `AssetKind` 决定发布到哪一个环境。路径为：

```text
https://mcombat.s3.ap-northeast-1.amazonaws.com/{dev|release}/v/{项目版本}/{iOS|Android}/
```

程序构建明确使用 `DoNotBuildWithPlayer`，关闭 Unity 隐式资源构建。现有资源组的本地／远端标记保持原样：本地 bundle 随程序打包，远端 bundle 在运行时从对应地址下载。包内的 Resources 下载界面保留原有加载方式。

## 新版本操作顺序

1. 在 `MCombat/Version Sync` 设置版本并保存项目。
2. 运行 `AssetDev_V`，选择环境和平台，构建并上传资源。
3. 运行 `CustomIOSBuild_V`，选择对应 `BUILD_KIND`，构建程序。

资源任务同时发布 `player-bootstrap.zip`，包含 Addressables 运行初始化配置、初始 catalog、**已编译的本地 bundle** 和 IL2CPP 类型保留文件。程序导出从所选资源地址读取这份现成产物，并把其中的本地 bundle 放进程序，因此新工作区或清空 `Library` 后也能构建；它不会运行资源编译器。地址、版本、平台不符或初始化配置缺失时，程序构建会明确失败，不会使用旧缓存。

资源上传顺序为远端 bundle、catalog、程序所需的资源产物、最后 catalog hash。重新发布不会删除旧远端 bundle。独立资源发布不再使用程序构建快照或版本目录占用 manifest。

新机制第一次使用时，需先运行对应环境的资源任务生成远端资源和 `player-bootstrap.zip`。修改本地 bundle 后需要重新出程序；远端更新应保持与已安装程序的本地 bundle 和脚本类型兼容。旧安装包仍使用旧代码和包内资源，需重新构建、安装后才能验证新机制。

## Jenkins 入口与验证模式

管线源码位于 `/Users/daisei/MCombat_tool/pipeline_script/PocketStriker/`；两个任务从工具仓库 `master` 读取：

- [CustomIOSBuild_V](http://localhost:8080/job/CustomIOSBuild_V/)：`ios.groovy`。
- [AssetDev_V](http://localhost:8080/job/AssetDev_V/)：`assets.groovy`。

项目代码与工具仓库的修改都需提交、推送后才会被 Jenkins 的检出使用。任务各使用自己的工作区并禁止同任务并发。Unity 固定为 `6000.5.1f1`，构建前检查编辑器版本、平台模块及 Metal 工具链。

- 程序 `VALIDATE_ONLY=true`：读取已有资源初始化配置、Unity 导出、CocoaPods 和未签名原生编译；跳过 IPA 导出与上传。
- 程序 `SIGNING_VALIDATE_ONLY=true`：签名归档与 IPA 导出；跳过 App Store 验证和上传。
- 资源 `VALIDATE_ONLY=true`：构建并检查资源及初始化配置；跳过 S3 上传。

旧程序任务参数 `AssetKind` 与 `buildAsset` 不参与管线，程序环境统一由 `BUILD_KIND` 选择。资源任务继续使用 `AWS_PROFILE` 与可选 `AWS_CACHE`；catalog、hash 和初始化配置使用 `no-cache`。

## 回归检查

```bash
python3 Tools/Validation/test_jenkins_pipelines.py --tool-repo /Users/daisei/MCombat_tool
python3 Tools/Validation/test_addressables_bootstrap.py
bash Tools/validate_unity.sh check ios
bash Tools/validate_unity.sh compile ios
```

Groovy 回归执行真实管线的受控 DSL，覆盖程序任务不构建／发布资源、独立 Dev／Release 发布、环境与版本路由、验证模式及 Bash 语法。初始化配置回归覆盖独立资源输出、错误配置和安全归档。Unity 的 `PocketStrikerVersionValidation.Validate` 检查版本与构建选项。

2026-10-01 的程序任务 #28／#29 失败于下载 `player-bootstrap.zip`，S3 上缺少这个新产物，匿名请求返回 403。现有 `Release / 3.0.2 / iOS` 已从与线上 catalog 完全配套的成功构建 #25 补齐该文件，保留原有 96 个本地 bundle 和 linker。401 个远端文件均核对了大小与 ETag；未重新编译资源或替换已发布 catalog／bundle。后续新版本由独立资源任务自动生成这份产物。

补齐后，Jenkins 程序任务 #30 以 `VALIDATE_ONLY=true` 成功通过 Unity 导出、CocoaPods 和 Xcode 无签名编译。导出的 96 个本地 bundle、catalog、settings 与 linker 均与配套历史产物逐字节一致，程序任务未调用资源编译。该验证跳过签名、IPA 导出与 App Store 上传。

## 既有签名配置与历史诊断

iOS Release 使用 `PocketStriker App Store 2026-09-27` profile，Dev 使用 `PocketStriker Ad Hoc 2026-09-27`，两者均含 Sign In with Apple；到期日为 2027-03-13 UTC。已有 Release 正式归档和 IPA 导出验证通过，尚不代表本次机制已完成真机或 App Store 验证。

2026-09-28 至 09-30 的旧管线曾混用不同构建的本地 bundle 与远端 catalog，并出现 OutsideDataLink 缺失等问题。此前加入的“程序构建同时生成并发布资源”机制现已撤除，改为复用独立资源任务生成的 catalog 和本地 bundle。

原始 Jenkins 配置备份位于 `/Users/daisei/.jenkins/backups/pocketstriker-20260927/`。纯诊断工具 `Tools/Validation/verify_ios_addressables_pair.py` 保留用于核对已有产物，不作为程序构建或发布门槛。
