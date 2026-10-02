using System;
using UnityEngine;
using UnityEngine.UI;

namespace mainMenu
{
    public partial class SkillStonesBox : MonoBehaviour
    {
        public event Action ExTabPressed;

        [Header("画面主模块parent")]
        [SerializeField] RectTransform BoxT;

        [Header("type按钮")]
        [SerializeField] Dropdown types;
        [SerializeField] BOButton NormalTab;
        [SerializeField] BOButton EX1Tab;
        [SerializeField] BOButton EX2Tab;
        [SerializeField] BOButton EX3Tab;

        [Header("order")]
        [SerializeField] BOButton orderBtn;
        [Header("Order Button")]
        [SerializeField] Text orderButtonText;

        [Header("type特效管理")]
        public SkillStoneBoxTabEffectsManager _tabEffects;

        [Header("攻击范围限定")]
        [SerializeField] Toggle closeCheckBox;
        [SerializeField] Toggle nearCheckBox;
        [SerializeField] Toggle farCheckBox;

        [SerializeField] ShowAllMyStoneLevel showAllMyStoneLevel;

        public void TutorialSimpleMode()
        {
            closeCheckBox.gameObject.SetActive(false);
            nearCheckBox.gameObject.SetActive(false);
            farCheckBox.gameObject.SetActive(false);
            showAllMyStoneLevel.gameObject.SetActive(false);
        }

        void Awake()
        {
            Selected = selectedFrame;
            FocusingType = "human";
            orderBtn.onClick.AddListener(SwitchOrder);
        }

        public string FocusingType
        {
            get;
            set;
        }

        RectTransform BoxRoot
        {
            get
            {
                if (BoxT != null)
                    return BoxT;
                if (grid != null)
                    return grid.GetComponent<RectTransform>();
                return transform as RectTransform;
            }
        }

        // 功能系。刷新技能石陈列界面。这里应该包括一个特殊功能，就是展示Tutorial模式下临时可用的那些石头
        public void FilterFeatureRefresh(bool viewingMode)
        {
            if (viewingMode)
            {
                types.ClearOptions();
                foreach (var s in Units.GetTypeList())
                {
                    // Legacy unit types with no configured skills are not selectable categories.
                    if (SkillConfigTable.GetSkillConfigsOfType(s).Count == 0) continue;
                    var m_NewData = new Dropdown.OptionData
                    {
                        text = s
                    };
                    types.options.Add(m_NewData);
                }
                var selectedType = types.options.FindIndex(option => option.text == FocusingType);
                types.SetValueWithoutNotify(Mathf.Max(0, selectedType));
                types.RefreshShownValue();
                types.gameObject.SetActive(types.options.Count > 1);
            }
            else
            {
                types.gameObject.SetActive(false);
            }
            types.onValueChanged.RemoveListener(OnTypeSelected);
            types.onValueChanged.AddListener(OnTypeSelected);
            closeCheckBox.onValueChanged.RemoveAllListeners();
            closeCheckBox.onValueChanged.AddListener(delegate { RestFilter(); });
            nearCheckBox.onValueChanged.RemoveAllListeners();
            nearCheckBox.onValueChanged.AddListener(delegate { RestFilter(); });
            farCheckBox.onValueChanged.RemoveAllListeners();
            farCheckBox.onValueChanged.AddListener(delegate { RestFilter(); });
        }

        // 直接放在type下拉按钮上的功能
        public void TypeDropDownBehaviour()
        {
            if (types.options.Count == 0) return;
            FocusingType = types.options[types.value].text;
            RestFilter();
        }

        void OnTypeSelected(int value) => TypeDropDownBehaviour();
    }
}
