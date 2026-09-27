using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace mainMenu
{
    public class BattleGroundSwitch : MonoBehaviour
    {
        [Header("战场选择")] [SerializeField] private Text battleFieldName;
        [SerializeField] private BOButton touchBtn;
        [SerializeField] private BOButton leftSwitchBattleField;
        [SerializeField] private BOButton rightSwitchBattleField;

        private readonly List<string> _battleFieldKeys = new List<string>();
        private int _choosingBattleFieldId = 0;

        public int BattleFieldIndex
        {
            get => _choosingBattleFieldId;
            set
            {
                if (value > _battleFieldKeys.Count - 1)
                {
                    _choosingBattleFieldId = 0;
                }
                else if (value < 0)
                {
                    _choosingBattleFieldId = _battleFieldKeys.Count - 1;
                }
                else
                {
                    _choosingBattleFieldId = value;
                }
            }
        }

        int SwitchBattleField(bool plusIndex)
        {
            if (plusIndex)
                BattleFieldIndex += 1;
            else
                BattleFieldIndex -= 1;
            
            battleFieldName.text = this.BattleFieldName();
            return BattleFieldIndex;
        }

        string BattleFieldName()
        {
            return Translate.Get("battleGround/" + BattleFieldIndex);
        }

        async UniTask BattleFieldInitialize()
        {
            _battleFieldKeys.Clear();
            var locationHandle = Addressables.LoadResourceLocationsAsync("battle_ground");
            await locationHandle.Task;
            if (locationHandle.Status == AsyncOperationStatus.Succeeded)
            {
                foreach (var stageLocation in locationHandle.Result)
                {
                    _battleFieldKeys.Add(stageLocation.PrimaryKey);
                }
            }

            Addressables.Release(locationHandle);
        }
        
        public async UniTask INI()
        {
            await BattleFieldInitialize();
            leftSwitchBattleField.SetListener(() => this.SwitchBattleField(false));
            rightSwitchBattleField.SetListener(() => this.SwitchBattleField(true));
            touchBtn.SetListener(() => this.SwitchBattleField(true));

            this.SwitchBattleField(true);
        }
    }
}