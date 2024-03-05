using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using dataAccess;
using UnityEngine;
using UnityEngine.UI;
using DummyLayerSystem;

namespace mainMenu
{
    public class SelfFightLayer : UILayer
    {
        [Header("选中框")]
        [SerializeField] GameObject selectedFrame;

        [Header("删除")]
        [SerializeField] Button removeBtn;
        
        [Header("角色属性框")]
        [SerializeField] HeroIcon noMagic;
        
        [SerializeField] HeroCell team11_R, team12_R, team13_R;
        [SerializeField] HeroCell team21_R, team22_R, team23_R;
        
        [Header("选中角色的技能显示")]
        [SerializeField] NineForShow nineForShow;
        
        [Header("技能编辑")]
        [SerializeField] BOButton skillEditButton;

        [Header("Start")]
        [SerializeField] FightModeSwitch _fightModeSwitch;
        [SerializeField] FightBeginBtn fightStartBtn;

        [Header("战场选择")]
        [SerializeField] BattleGroundSwitch battleGroundSwitch;

        readonly MultiDic<Team, int, HeroCell> _teamButtonDicR = new MultiDic<Team, int, HeroCell>();
        readonly IDictionary<HeroCell, int> _iconNumCheck = new Dictionary<HeroCell, int>();
        private readonly FightMembers _selfFight = new FightMembers();
        FightInfo _stage;
        
        PosKeySet _team1PosKeySetR = new PosKeySet();
        PosKeySet _team2PosKeySetR = new PosKeySet();
        
        public async UniTask INI()
        {
            _stage = ScriptableObject.CreateInstance<FightInfo>();
            _stage.EventType = FightEventType.Self;
            
            IniCells(new List<HeroCell> { team11_R, team12_R, team13_R }, Team.player1);
            IniCells(new List<HeroCell> { team21_R, team22_R, team23_R }, Team.player2);
            
            _fightModeSwitch.Setup(0,PlayerPrefs.GetInt("preferAdventureMode",  PlayerPrefs.GetInt("preferAdventureMode", 2)));
            fightStartBtn.SetAction(FightStart);
            
            void SkillEdit()
            {
                if (nineForShow.InstanceID != null)
                {
                    PreScene.target.SetFocusingUnit(nineForShow.InstanceID);;
                    PreScene.target.trySwitchToStep(MainSceneStep.UnitSkillEdit);
                }
            }
            skillEditButton.SetListener(SkillEdit);
            await battleGroundSwitch.INI();
        }
        
        void FightStart()
        {
            _stage.battleGroundID = battleGroundSwitch.BattleFieldIndex;
            _stage.team1Mode = _fightModeSwitch.TeamMode;
            _stage.team2Mode = _fightModeSwitch.TeamMode;
            FightLoad.Go(_stage);
        }
        
        public void Clear()
        {
            foreach (var icon in _teamButtonDicR.GetValues())
            {
                icon.Clear();
            }
            
            _team1PosKeySetR = new PosKeySet();
            _team2PosKeySetR = new PosKeySet();
        }
        
        void CancelSelect()
        {
            var unitsLayer = UILayerLoader.Get<UnitsLayer>();
            unitsLayer.Selected.Value = null;
            focusTeam = Team.none;
            focusPos = -1;
            HeroIcon.SelectedFeature(null, selectedFrame, 1.1f);
        }
        
        #region Icon Feature 必须在unit box生成所有角色头像之后执行
        public void AddUnitIconFeaturesToBox()
        {
            var unitsLayer = UILayerLoader.Get<UnitsLayer>();
            unitsLayer.SetUnitsIconOnClick(ClickOnLowerIcons);
        }
        
        // 点击下面那一排icon的动作
        void ClickOnLowerIcons(string instanceID)
        {
            var unitsLayer = UILayerLoader.Get<UnitsLayer>();
            unitsLayer.Selected.Value = instanceID;
            nineForShow.ShowStones_Acc(instanceID);

            if (focusTeam != Team.none && focusPos != -1)
            {
                switch (focusTeam)
                {
                    case Team.player1:
                        ChangeIconOnPos(focusPos, focusTeam, instanceID, _teamButtonDicR, _team1PosKeySetR);
                        break;
                    case Team.player2:
                        ChangeIconOnPos(focusPos, focusTeam, instanceID, _teamButtonDicR, _team2PosKeySetR);
                        break;
                }
            }
        }

        private Team focusTeam = Team.none;
        private int focusPos = -1;
        // 点击cell或cell里面icon的动作
        void OnTeamPosBtn(Team team, int pos)
        {
            focusTeam = team;
            focusPos = pos;
            
            var unitsLayer = UILayerLoader.Get<UnitsLayer>();
            var unitsBoxSelect = unitsLayer.Selected.Value;
            var oneSet = CheckIfHaveUnitOnTeamSlot(pos, team);
            nineForShow.ShowStones_Acc(oneSet?.instanceID);
            removeBtn.gameObject.SetActive(oneSet != null && oneSet.instanceID != null);
            removeBtn.onClick.RemoveAllListeners();
            removeBtn.onClick.AddListener(() =>
            {
                RemoveSelect(pos, team);
                removeBtn.gameObject.SetActive(false);
            });

            if (unitsBoxSelect != null)
            {
                switch (team)
                {
                    case Team.player1:
                        ChangeIconOnPos(pos, team, unitsBoxSelect, _teamButtonDicR, _team1PosKeySetR);
                        break;
                    case Team.player2:
                        ChangeIconOnPos(pos, team, unitsBoxSelect, _teamButtonDicR, _team2PosKeySetR);
                        break;
                }
            }
        }
        #endregion
        
        void IniCells(List<HeroCell> cells, Team team)
        {
            for (var i = 0; i < cells.Count; i++)
            {
                var heroCell = cells[i];
                _teamButtonDicR.Set(team, i, heroCell);
                DicAdd<HeroCell, int>.Add(_iconNumCheck, heroCell, i);
                heroCell.Clear();
                heroCell.iconButton.SetListener(() =>
                {
                    OnTeamPosBtn(team, _iconNumCheck[heroCell]);
                    HeroIcon.SelectedFeature(heroCell.transform, selectedFrame, 1.1f);
                });
                void CellTrigger(string x)
                {
                    PosKeySet targetPosKeySet = null;
                    switch (team)
                    {
                        case Team.player1:
                            targetPosKeySet = _team1PosKeySetR;
                            break;
                        case Team.player2:
                            targetPosKeySet = _team2PosKeySetR;
                            break;
                    }
                    ChangeIconOnPos(_iconNumCheck[heroCell], team, x, _teamButtonDicR, targetPosKeySet);
                }
                heroCell.TeamEdit = CellTrigger;
                heroCell.sourceCellSwapAction = CellTrigger;
            }
        }
        
        void ChangeIconOnPos(int posNum, Team team, string instanceID, MultiDic<Team, int, HeroCell> teamButtonDic, PosKeySet posKeySet)
        {
            var unitInfo = dataAccess.Units.Get(instanceID);
            if (unitInfo != null && Stones.GetEquippingStones(instanceID).Count != 9)
            {
                return;
            }
            
            posKeySet.SetPosMemInfoByInstanceID(posNum, instanceID);
            var cell = teamButtonDic.Get(team, posNum);
            var posInstanceId = posKeySet.GetInstanceIdOnPos(posNum);
            if (posInstanceId != null)
            {
                var info = dataAccess.Units.Get(posInstanceId);
                if (info != null)
                {
                    var targetingIcon = Instantiate(noMagic);
                    targetingIcon.name = info.id + "_icon";
                    targetingIcon.InstanceID = instanceID;
                    targetingIcon.ChangeIcon(info);
                    targetingIcon.iconButton.SetListener(() =>
                    {
                        OnTeamPosBtn(team, posNum);
                        if (targetingIcon != null)
                        {
                            var cell = targetingIcon.GetCell();
                            if (cell != null)
                                HeroIcon.SelectedFeature(cell.transform, selectedFrame, 1.1f);
                        }
                    });
                    cell.AddItem(targetingIcon);
                }
                else
                {
                    cell.Clear();
                }
            }
            else
            {
                cell.Clear();
            }
            CheckFightLegal();
            CancelSelect();
        }
        
        PosKeySet.OneSet CheckIfHaveUnitOnTeamSlot(int pos, Team team)
        {
            switch (team)
            {
                case Team.player1:
                    return _team1PosKeySetR.GetPosMemInfo(pos);
                case Team.player2:
                    return _team2PosKeySetR.GetPosMemInfo(pos);
            }
            return null;
        }
        
        void ArrangeStageInfo()
        {
            _selfFight.HeroSets = _team1PosKeySetR.LoadTeamDic();
            _selfFight.EnemySets = _team2PosKeySetR.LoadTeamDic();
            _stage.FightMembers = _selfFight;
            _stage.Team1ID = PlayerAccountInfo.Me.PlayFabId;
            _stage.Team2ID = PlayerAccountInfo.Me.PlayFabId + "_2";
        }

        void CheckFightLegal()
        {
            ArrangeStageInfo();
            fightStartBtn.Enable(_stage.FightMembers.CheckStonesLegal(FightEventType.Self));
        }

        void RemoveSelect(int pos, Team team)
        {
            if (pos != -1)
            {
                switch (team)
                {
                    case Team.player1:
                        ChangeIconOnPos(pos, team,null, _teamButtonDicR, _team1PosKeySetR);
                        break;
                    case Team.player2:
                        ChangeIconOnPos(pos, team,null, _teamButtonDicR, _team2PosKeySetR);
                        break;
                }
            }
        }
    }
}