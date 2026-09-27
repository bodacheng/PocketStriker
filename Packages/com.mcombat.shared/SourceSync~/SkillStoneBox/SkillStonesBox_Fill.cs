using System.Collections.Generic;
using UnityEngine;
using dataAccess;
using Skill;

namespace mainMenu
{
    public partial class SkillStonesBox : MonoBehaviour
    {
        [SerializeField] private bool skillEditStep;
        private StoneFilterForm _form;
        readonly List<string> _currentStoneInstanceIds = new List<string>();

        public void RestFilter()
        {
            var filterForm = new StoneFilterForm
            {
                Type = FocusingType,
                ExType = new[] { FocusingExType },
                Close = closeCheckBox.isOn,
                Near = nearCheckBox.isOn,
                Far = farCheckBox.isOn
            };

            _form = filterForm;
            RefreshVisibleStones(true);
        }

        public class StoneFilterForm
        {
            public string Type;
            public BehaviorType BType = BehaviorType.NONE;
            public int[] ExType = { 0, 1, 2, 3 };
            public bool BossSkill = false;
            public bool Close;
            public bool Near;
            public bool Far;

            public StoneFilterForm()
            {
            }

            public StoneFilterForm(string type)
            {
                Type = type;
            }
        }

        void RefreshVisibleStones(bool refreshFromSource)
        {
            if (refreshFromSource)
            {
                string focusingUnitInstanceId = null;
                if (skillEditStep && PreScene.target.Focusing != null)
                    focusingUnitInstanceId = PreScene.target.Focusing.id;
                var targetSKs = Stones.TargetStonesFromAccount_except(focusingUnitInstanceId, _form, null, null, false);
                _currentStoneInstanceIds.Clear();
                if (targetSKs != null)
                    _currentStoneInstanceIds.AddRange(targetSKs);
            }

            UpdateVisibleCells(_currentStoneInstanceIds.Count);
            Order(_currentStoneInstanceIds);
            ApplyCurrentTargetsToCells();
        }

        void ApplyCurrentTargetsToCells()
        {
            foreach (var cell in _cells)
            {
                cell?.RemoveToTemp();
            }

            var key = 0;
            foreach (var instanceId in _currentStoneInstanceIds)
            {
                if (key >= _activeCellCount)
                {
                    Debug.Log("Stone box exceed："+ key);
                    Debug.Log("此时技能石头盒子的总容量：" + _activeCellCount);
                    break;
                }

                var renderModel = Stones.GetRenderModel(instanceId);
                var cell = _cells[key];
                if (renderModel == null || cell == null)
                    continue;

                if (!renderModel._using)
                {
                    cell.AddItem(renderModel);
                    key++;
                }
                else
                {
                    // 这个环节按理说不该出现
                    cell.UpdateMyItem();
                }
            }
        }
    }
}
