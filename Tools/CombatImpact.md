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
