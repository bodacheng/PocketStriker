# 战斗输入与冲击验收

使用 Unity 6000.5.1f1、iOS 目标和本地 Addressables Fast Mode。先正常关闭
当前项目的交互编辑器；保留场景及所有已有修改。以下检查不会发布程序或资源，
不调用 AI 故事、网络奖励或登录服务。

```sh
unity_editor=/Applications/Unity/Hub/Editor/6000.5.1f1/Unity.app/Contents/MacOS/Unity
"$unity_editor" -batchmode -projectPath "$PWD" -buildTarget iOS \
  -executeMethod PocketStrikerBattleHUDValidation.ValidateAllBatch \
  -logFile "$PWD/Logs/combat-hud.log"
POCKETSTRIKER_IMPACT_REVIEW=Review375 POCKETSTRIKER_CAMERA_SIZE=375x667 \
  "$unity_editor" -batchmode -projectPath "$PWD" -buildTarget iOS \
  -executeMethod PocketStrikerBattleCameraSmoke.StartBatch \
  -logFile "$PWD/Logs/combat-impact-375.log"
POCKETSTRIKER_IMPACT_REVIEW=Review390 POCKETSTRIKER_CAMERA_SIZE=390x844 \
  "$unity_editor" -batchmode -projectPath "$PWD" -buildTarget iOS \
  -executeMethod PocketStrikerBattleCameraSmoke.StartBatch \
  -logFile "$PWD/Logs/combat-impact-390.log"
Tools/validate_unity.sh compile
python3 Tools/Validation/validate_battle_modes.py
```

Play-mode 检查由验证方法自行退出，不能添加 `-quit`。新报告标签避免覆盖历史证据。
HUD 报告位于 `Logs/UILayout/BattleHUD/report.json`；实战报告位于
`Logs/CombatImpact/<标签>/report.json` 和 `impact.json`。必须同时确认两个报告
通过且 `impact.complete=true`、失败列表为空；仅看到外层 PASS 不足以验收。

停机 HUD 夹具检查五种手机／平板安全区、扩大点击边缘、六种禁用／恢复输入及
摇杆隔离。夹具没有真实角色焦点，需在每轮模式切换后恢复其显式展示，避免把无焦点
时生产逻辑隐藏 Dash/Dream 的行为误报为摇杆遮挡。生产透明点击层禁止透明 mesh 剔除。

实战夹具通过实际 FightScene、角色动画事件、命中查询和 Rigidbody 求解器验证：

- 开局、重试、多人和群战倒计时的 Dash/Dream 显示与禁用，以及 0%、35%、100%
  能量数据、控件和材质的一致性；替补 50% 能量及旧角色订阅隔离。
- 普通战斗开局、替补、重试和多人战斗的实际 EventSystem 中心／扩大边缘点击，
  正常按下与释放；群战正式战斗按规则关闭手动动作与摇杆。
- 技能 145 天降螺旋脚连续命中、落空、双方死亡、替补后命中和实际伤害致死。
  自然致死夹具设低 HP 以缩短时间，保留第一击并让后续真实命中致死；其他运动案例
  使用无敌隔离运动。技能 148、150 也检查仍能命中。
- 冲击期间胶囊分离、结束后恢复碰撞，击飞后 Grounded 并稳定留在地面。
- 全部注册模型一个实体胶囊、肢体伤害触发器及统一尺寸范围、Continuous 检测。
  六人初始挤压分别记录全部角色，检查末段位移、速度和实际可碰撞对的穿透。

接地判断使用 0.001 世界单位容差，消除恢复移动后的浮点残差；不改变落地轨迹，
验收仍要求真实高度小于 0.05 且末段持续 Grounded。

单帧位移比较必须同时查看采样间隔；Rigidbody 速度不等于脚本驱动的物理根速度。
既有质量参数保持不变，重量感来自稳定接触形状与统一位移控制，不能称为增加质量。
编辑器验证不代替真机多点触控、低帧率、设备性能或独立 IL2CPP 玩家运行。

## 可见体积与四项视觉回归（2026-10-02）

`CombatBodyProfile` 首次加载读取躯干 Box/Capsule 的世界尺寸，加入 0.12 接触余量，
半径统一限制在 0.88–1.08，高度结合头部与可见模型顶部，限制在 2.8–3.4。
跨回合和未激活替补保持首次校准，不能从攻击姿势重新生成尺寸。全部 19 种模型的
尺寸审计不等同于逐张模型视觉验收；正常／轮廓对照重点覆盖 9/9、9/18、8/11、13/16。

实战夹具增加四组贴身躯干代理间隙和九项异体型 145/148/150 案例；胶囊穿透与
躯干代理间隙均需配合正常画面判断。手臂／武器的伤害体保持 trigger。
独立受击案例先经 Move→Empty 回到中性状态、清空击飞计量，避免强制 Empty 残留的
LastState=KnockOff 混入下一项；另以预置累计计量单独验证实际击飞和落地。
自然死亡后停用尸体的高度只记录，不算存活者落地通过。

六人故意深度重叠的起点与 3 秒后的稳定期分别检查：初始分离帧位移 <0.2、
Rigidbody 速度 <3；稳定期帧位移 <0.02、速度 <0.15、穿透 <0.04。
较大的体积在解重叠初段可能比旧小体积位移大，保留完整峰值与失败校准材料。
接地静止且未受伤的 Empty/Stand/Defend 才衰减水平残余速度，保留 Y，
移动、Dash、技能、受伤、跟随冲击、击飞和起身均跳过，不再引入根位置写入者。

Dash 使用原 `rush` 人物纹理的逐字节副本，由独立 UI glyph 显示；UI shader 将黑底
转为透明，旧双箭头移除，粒子中的重复人物 renderer 停用，环和反馈保留。
不依赖角色焦点或粒子池，大乱斗倒计时也能显示同一人物。
实际保存禁用、按住、正常三态，继续检查小屏中心和扩大边缘的按下／释放。

相机额外入口：`PocketStrikerBattleCameraOpeningValidation.ValidateBatch`；实际场景用
`POCKETSTRIKER_CAMERA_OPENING=1 POCKETSTRIKER_CAMERA_REVIEW=<新标签>` 调用现有
`PocketStrikerBattleCameraSmoke.StartBatch`。尺寸可指定 `375x667`、`390x844`、
`834x1194`。倒计时检验玩家右下／敌人左上的像素团队轴；1v1 正式战斗的自动旋转
改为将双方地面轴对齐屏幕水平线，继续保留原有震动过滤、慢速旋转和距离迟滞。
另检验完整模型角点、替补／死亡／重试、
高低模型、腾空／落地及 6 人／200 人阵容。约 45° 指屏幕斜线，安全拟合可能为极高
模型扩大镜距；不承诺所有阵容都比横排更近。

**PocketStriker → Validation → Combat Visuals**（批处理入口
`PocketStrikerCombatVisualValidation.Validate`，带 `-quit`）统一检查完整模型构图、
大乱斗全战场轨道、倒计时、水平自动旋转／震动稳定性、技能特效依赖和准备页相机／loading 排版。
总报告位于 `Logs/CombatVisuals/report.json`，特效审计和渲染对照位于
`Logs/EffectResources`。场景中的 UnitCamera 和展示用 DedicatedCameraConnector
显式请求深度纹理，各档 URP Renderer 在不透明物体之后复制深度，供随后的透明特效使用。
MCombat 的软粒子材质依赖这一纹理，资源文件齐全也可能因相机缺少深度、或生成过晚而
隐藏光柱、光环。特效验证使用全部六档画质，以及世界和角色相机均关闭深度的负对照。

大乱斗全战场相机（2026-10-03）替代按角色动态取景的 Group 相机。**Group Battle Camera**
专项检查完整圆形地面边界贴近屏幕侧边、站立体积和纵向技能高度余量、720 种屏幕／半径／旋角组合、手指捕获与 UI 控件隔离，
报告位于 `Logs/CameraFraming/Group/report.json`。**Battle Camera Playmode Smoke** 的默认大乱斗
及重试使用古城，检查真实地图配置、完整边界、角色移动／相机焦点参数变更／死亡后的镜头稳定，
以及实际 EventSystem 对战场和暂停按钮的命中分流；不等同于真机触控验证。
**Ancient Empire Readability** 用实际地图、角色和战斗灯光保存三种视角的前后对照，
并检查角色亮度、共享材质与资源恢复，报告和图片位于 `Logs/Rendering/AncientEmpireReadability`。

战斗 Auto 的亮暗底色、明确 ON/OFF 文本与左右滑块通过 **Battle HUD** 检查实际开关回调、
禁用、程序状态变化与隐藏控件，五种屏幕保存开启／关闭／禁用对照。
**Skill Icon MCombat Reference** 检查 96 个实际 Sprite 地址；
`Tools/Validation/validate_skill_icon_reference.py --reference <MCombat项目目录>` 对照注册 GUID 与原图像素，
替换 38、99、100、181、197、198、199 七张图，保留本项目 `.meta` 与引用。
MCombat 的 108 地址重复绑定，首个正常目录图与本项目相同，未采用临时目录里的另一张图。
报告和对照位于 `Logs/SkillIconReference`。

**Battle Effect Invalidation** 验证当帧处理队列之外的特效也立即失效、碰撞与缓存命中关闭、
子粒子清除、旧回调无法重启、新回合租借恢复；报告位于 `Logs/CombatEffects/report.json`。
**Evolution Heal Playmode Smoke** 另检查真实重开边界、三次死亡／选技期间已有武器与视觉特效失效，
以及失效前发起的延迟加载无法生成旧特效、同一池的新旧体部请求竞争不会清掉新效果。
杀敌当帧保留已完成命中的统计并取消同批剩余命中；梦幻连击的迟到请求按回合及连击序列检查后才更新增益。
角色死亡后的异步退场只操作原战斗的原模型，重开后不会生成旧退场效果或隐藏新角色。
报告位于 `Logs/Evolution/Playmode/report.json`。

准备页退出验证通过 `PocketStrikerUIInterruptionValidation.StartBatch` 运行
（不带 `-quit`）；实际单人／团战准备页的角色及预览相机必须在释放动画资源前同步
停用。`python3 Tools/Validation/validate_model_loading.py` 另检查延迟角色加载／
预热回调不会复活关闭的界面或关闭后续战斗 loading。

登录美术的原图、提示词、可恢复方式及离线真实标题层入口见
`Tools/ArtSources/LoginBackground/README.md`。最终验收报告和源码一致性记录存于
`Logs/CombatVisualReview`。原编辑器占用时可以使用完整本地项目隔离副本，必须逐项
核对源码／配置哈希并在报告中标明执行目录，不能写成原窗口或真机实测。
