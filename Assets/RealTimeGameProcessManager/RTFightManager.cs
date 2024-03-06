using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Threading.Tasks;
using UniRx;

namespace FightScene
{
    //角色列表的职责现在不光是负责两侧菜单中角色的icon，也负责被控制角色又上角血条和ex条
    public class RTFightManager : MonoBehaviour
    {
        public UnitsManger team1, team2;
        
        [Header("Basic Element")]
        public CameraManager _CameraManager;
        
        public readonly TeamConfig heroTeamConfig = new TeamConfig("1", Team.player1, new List<Team>() { Team.player2 });
        public readonly TeamConfig EnemyTeamConfig = new TeamConfig("2", Team.player2, new List<Team>() { Team.player1 });
        
        public static RTFightManager Target;
        public static Team playerTeam = Team.player1;
        
        public readonly IDictionary<Data_Center, UnitInfo> UnitInfoRef = new Dictionary<Data_Center, UnitInfo>();
        public readonly IDictionary<Data_Center, ReactiveProperty<float>> RefreshTimeDic = new Dictionary<Data_Center, ReactiveProperty<float>>();
        
        FightInfo _loadFight;

        public CompositeDisposable Disposables = new CompositeDisposable();
        
        void Awake()
        {
            Target = this;
        }
        
        public async UniTask LoadUnits(FightInfo info)
        {
            await UniTask.WhenAll(
                team1._UnitsLoad(info.FightMembers.HeroSets, UnitInfoRef), 
                team2._UnitsLoad(info.FightMembers.EnemySets, UnitInfoRef)
            );
        }
        
        public void SetGame(FightInfo stage)
        {
            _loadFight = stage;
            FightScene.target.LoadStageFinished.Value = true;
        }
        
        public void ModeStart()
        {
            switch (_loadFight.team1Mode)
            {
                case TeamMode.MultiRaid:
                    team1.AllUnitsStartOff();
                    break;
                case TeamMode.Rotation:
                    team1.UnitStartOff();
                    break;
            }
            
            switch (_loadFight.team2Mode)
            {
                case TeamMode.MultiRaid:
                    team2.AllUnitsStartOff();
                    break;
                case TeamMode.Rotation:
                    team2.UnitStartOff();
                    break;
            }
        }
        
        // 战斗模式相机。根据选择队伍做相应调整。
        public void CameraAdjustment(Team myTeam, TeamMode teamMode, FightEventType eventType, Transform me = null)
        {
            C_Mode cMode;
            if (teamMode == TeamMode.Rotation)
                cMode = C_Mode.CertainYAntiVibration;
            else
            {
                cMode = eventType == FightEventType.Gangbang ? C_Mode.TopDown : C_Mode.WatchOver;
            }
            
            var ts = myTeam == Team.player1 ? team1.GetFightingUnitTs() : team2.GetFightingUnitTs();
            var tsOpponents = GetOpponents();
            
            List<Transform> GetOpponents()
            {
                List<Transform> returnValue;
                if (teamMode == TeamMode.Rotation)
                {
                    returnValue = myTeam == Team.player1
                        ? new List<Transform>() { team2.GetRModeUnitT() }
                        : new List<Transform>() { team1.GetRModeUnitT() };
                }
                else
                {
                    returnValue = myTeam == Team.player1
                        ? team2.GetFightingUnitTs()
                        : team1.GetFightingUnitTs();
                }
                return returnValue;
            }
            
            if (eventType == FightEventType.Gangbang)
            {
                _CameraManager.Assign_Camera(cMode, null, null);
            }
            else
            {
                if (cMode == C_Mode.WatchOver)
                {
                    var center = (me != null ? me : null);
                    _CameraManager.Assign_Camera(cMode, center, tsOpponents, ts);
                }
                else
                {
                    var center = (me != null ? me : ( ts.Count > 0 ? ts[0]: null ));
                    _CameraManager.Assign_Camera(
                        cMode,
                        center,
                        tsOpponents
                    );
                }
            }
        }
        
        public void ClearUnitData()
        {
            foreach (var one in team1.teamMembers.GetValues())
            {
                one.CleanClear();
            }
            foreach (var one in team2.teamMembers.GetValues())
            {
                one.CleanClear();
            }
        }
    }
}