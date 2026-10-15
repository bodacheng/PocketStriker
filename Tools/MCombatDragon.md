# MCombat 元素龙导入

角色 **17 / g_e_dragon** 已接入本项目，模型地址是 `e_dragon/g_e_dragon`。
随机 Boss 可选中它，技能箱、角色类型筛选、自动装备和进化选技按 `e_dragon` 使用专属技能。
这次修改没有发放账号角色，也没有发布远端资源；线上使用需要重新构建客户端和配套 Addressables。

## 资源与外观

- 预制体：`Assets/ExternalAssets/Unit/e_dragon/g_e_dragon.prefab`
- 模型、动作、材质和贴图：`Assets/OrganizedResources/InUse/MCombatDragon/`
- 头像：`Assets/OrganizedResources/InUse/ExternalAssets/Unit_Icon/17.png`
- 爪击特效：`Assets/ExternalAssets/HurtObjects/defaultmagic/El_Attack_Paw_Left.prefab`
- 实际渲染预览：`output/dragon-import/preview.png`；人物比例对照：`scale-comparison.png`

以 MCombat 的 Elemental Dragon Toon 网格、PA Generic Avatar、原骨骼及受击碰撞体为基础。
清除源预制体的场景位置偏移，使用本项目 Unit 层、受击闪色和阴影代理。
角色属性改为本项目的红属性（`Element.redMagic = 3`），由角色主表统一决定模型实例、默认特效路径与界面属性表现。
保留技能数值与动画事件，移动类型转换为本项目的 `Move2`，专属动画过渡时间保留 `0.3` 秒。

贴图使用内置 **imagegen** 对原 `El_Toon_Basic.png` 做 UV 图集重绘：砖红皮肤、米白腹甲、
深红翼膜和暖色骨角，降低细碎鳞片与写实高光。使用现有人物的 OmniShade/Standard URP
材质配置，关闭额外法线、金属和高光；翼膜双面可见。贴图有 mipmap，眼睛纹理限制为 512。
头像由实际新材质模型在 Unity 中渲染，保证与游戏模型一致。

龙使用独立的 `CombatBodyProfile` 身体接触配置：胶囊向胸部前移 `0.73`，
半径范围 `1.78–1.90`、高度范围 `3.6–3.8`，并保证高度不少于直径。
它按胸部的对角轮廓留出接触空间；翼、尾和攻击部位继续作为受击区域。
移动扫掠、击退跟随和团战编队边界同步使用该偏心碰撞体，人形保留原尺寸。

[来源及文件哈希](ArtSources/MCombatDragon/provenance.json) 记录只读 MCombat 来源，
[完整生成提示词](ArtSources/MCombatDragon/imagegen-prompt.txt) 记录贴图重绘输入。
共享包和 MCombat 源项目未修改。

## 动作适配

12 个已有技能定义 185–196 使用原专属动作。补齐本项目要求的基础动作键：
`death` 使用倒地动画，`block` 使用待机守势，`block_break` 使用受击动作，
`air` 使用飞行循环并去除战斗事件，背部受击使用原相应受击动画。
修复源控制器两个失效动画引用，以及爪击特效的旧粒子材质引用。
源 rush/getup 动作中未绑定的导出器 `Root` 曲线保留；实际 CG 骨架曲线均可解析。

龙只有少量 EX 技能，因此进化前两次各显示一个可用选项，第三次显示两个；
空选项隐藏，避免空技能或人形技能进入龙的技能组。

## 验证

停止播放后使用以下编辑器菜单：

- **PocketStriker → Validation → Dragon Import and Appearance**：红属性配置、预制体与完整依赖、
  29 个动画地址、12 个技能骨骼绑定、实际材质与角色比例渲染。
- **Dragon Production Animation Loading**：通过实际 Addressables 与动画加载器检查基础动作、
  六组受击／击飞资源和全部技能。
- **Dragon Playmode Smoke**：本地测试账号下，龙分别作为敌方／我方运行两场真实自动战斗，
  检查红属性及红属性特效路径、模型加载、材质、阴影、技能、伤害、死亡与结果；不请求奖励或变更账号库存。
  另用确定性的敌人死亡通知检查三次实际进化界面的 1／1／2 个选项、点击和恢复战斗。
- **Dragon Body Contact Playmode Smoke**：对照原小碰撞体，检查四个方向的静止和持续靠近接触，
  并从自然接触距离验证双方近战命中及红属性实例。报告位于 `Logs/Dragon/BodyContact`。
- **Battle Modes**：全部已发布关卡、随机 Boss、90 组必定选中龙的技能配置，以及六组人形／龙进化选项。

报告与截图写入 `Logs/Dragon/` 和 `Logs/Revival/battle-modes-report.json`。
批处理入口为 `PocketStrikerDragonValidation.ValidateBatch`、
`PocketStrikerCombatFlowSmoke.StartDragonBatch` 和 `PocketStrikerBattleModeValidation.ValidateBatch`。
实战为编辑器验证，不代替 iOS 真机或资源发布后的验证。

2026-10-13 验证：上述龙资源与加载验证、两场自然战斗、三次进化界面选择、90 组龙 Boss
技能组合及全部 120 个已发布关卡均通过。离线战斗模式 2,146 项和模型加载 39 项检查通过。

2026-10-14：红属性注册与资源检查通过；10 项实际身体接触对照、四个旋转方向的击退跟随、
双方近战伤害、两场自然战斗及三次进化选择通过。龙的实际接触半径约 `1.80`，
对照原 `1.08` 半径时前方躯干重叠约 `1.02`，新配置前方躯干间距约 `0.40`。
