using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using dataAccess;
using MCombat.Shared.Behaviour;
using Skill;

namespace mainMenu
{
    public class SkillStoneDetail : MonoBehaviour
    {
#if UNITY_EDITOR
        // Per-view fault injection for Editor regression; omitted from player builds.
        System.Func<string, Cysharp.Threading.Tasks.UniTask<SKStoneItem>> iconLoaderForValidation;
#endif
        Cysharp.Threading.Tasks.UniTask<SKStoneItem> LoadIcon(string id)
        {
#if UNITY_EDITOR
            if (iconLoaderForValidation != null) return iconLoaderForValidation(id);
#endif
            return Stones.GenerateStoneModel(id, false);
        }

        [Header("图标")]
        [SerializeField] RectTransform iconShowT;

        [Header("技能名字")]
        [SerializeField] Text keyName;
        [SerializeField] Text showName;

        [Header("技能类型图标")]
        [SerializeField] GameObject atIcon;
        [SerializeField] GameObject defenceIcon;

        [Header("EXTypes")]
        [SerializeField] GameObject ex1Icon, ex2Icon, ex3Icon;

        [Header("Range")]
        [SerializeField] GameObject close, near, far;

        [Header("property titles")] [SerializeField]
        private Text ATTitle, HPTitle, LevelTitle;

        [Header("AT")]
        [SerializeField] Text AT;

        [Header("HP")]
        [SerializeField] Text HP;

        [Header("当前技能等级")]
        [SerializeField] Text stoneTargetLevel;

        [Header("Intro")]
        [SerializeField] Text skillIntro;

        [Header("tempT")]
        [SerializeField] Transform tempT;

        public Text SkillIntro => skillIntro;

        int iconRequestVersion;
        SKStoneItem renderedIcon;

        // An old selection must never replace the text's current skill icon.
        public void IconForShow(string skillID, float size)
        {
            int version = ++iconRequestVersion;
            ClearIcon();
            RenderIcon(skillID, size, version).Forget();
        }

        async UniTask RenderIcon(string skillID, float size, int version)
        {
            var item = await LoadIcon(skillID);
            if (item == null) return;
            if (this == null || version != iconRequestVersion)
            {
                DiscardIcon(item);
                return;
            }
            var target = iconShowT != null ? iconShowT : tempT;
            if (target == null)
            {
                DiscardIcon(item);
                return;
            }
            renderedIcon = item;
            item.transform.SetParent(target, false);
            if (iconShowT != null)
            {
                item.gameObject.SetActive(true);
                item.transform.localPosition = Vector3.zero;
                item.transform.localScale = Vector3.one;
                item.GetComponent<RectTransform>().sizeDelta = new Vector2(size, size);
            }
        }

        void ClearIcon()
        {
            DiscardIcon(renderedIcon);
            renderedIcon = null;
            if (iconShowT == null) return;
            foreach (Transform child in iconShowT)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }

        static void DiscardIcon(SKStoneItem item)
        {
            if (item == null) return;
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
        }

        void OnDestroy()
        {
            iconRequestVersion++;
            DiscardIcon(renderedIcon);
        }

        public void Clear()
        {
            iconRequestVersion++;
            ClearIcon();
            keyName.text = string.Empty;
            showName.text = string.Empty;
            skillIntro.text = string.Empty;

            ATTitle.text = string.Empty;
            HPTitle.text = string.Empty;
            LevelTitle.text = string.Empty;

            stoneTargetLevel.text = string.Empty;
            AT.text = string.Empty;
            HP.text = string.Empty;
            ShowSkillStoneExType(ex1Icon, ex2Icon, ex3Icon,-1);
            ShowSKillRanges(close, near, far, -10, -10); //即清空
            atIcon.SetActive(false);
            defenceIcon.SetActive(false);
        }

        public void RefreshInfo(string instanceID)
        {
            var currentStone = Stones.Get(instanceID);
            if (currentStone == null)
            {
                Clear();
                return;
            }
            var skillConfig = SkillConfigTable.GetSkillConfigByRecordId(currentStone.SkillId);
            RefreshInfo(skillConfig);
            var row = PowerEstimateTable.Find_RECORD_ID(skillConfig.RECORD_ID);
            float.TryParse(row.HP, out float hp);
            float.TryParse(row.EstimateDamage, out float at);

            ATTitle.text = Translate.Get("at_title");
            HPTitle.text = Translate.Get("hp_title");
            LevelTitle.text = Translate.Get("level_title");

            var passiveSkill = UnitPassiveTable.GetPassiveSKillRecordIds();
            if (passiveSkill.Contains(currentStone.SkillId))
            {
                LevelTitle.text = Translate.Get("BornSkill");
                stoneTargetLevel.text = String.Empty;
                AT.text = FightGlobalSetting.ATCal(at, currentStone.Level) + "?";
                HP.text = FightGlobalSetting.StoneHpCal(hp, currentStone.Level) + "?";
            }
            else
            {
                stoneTargetLevel.text = (currentStone.Level == PlayFabSetting._VersionMaxStoneLevel ? "MAX" : currentStone.Level.ToString());
                AT.text = FightGlobalSetting.ATCal(at, currentStone.Level).ToString();
                HP.text = FightGlobalSetting.StoneHpCal(hp, currentStone.Level).ToString();
            }
        }

        public void RefreshInfo(SkillConfig config)
        {
            if (iconShowT != null)
                IconForShow(config.RECORD_ID, iconShowT.transform.GetComponent<RectTransform>().sizeDelta.x);
            keyName.text = config.REAL_NAME;

            if (PlayerAccountInfo.Me.TitleDisplayName != null && PlayerAccountInfo.Me.TitleDisplayName.Contains("IconDev"))
            {
                showName.text = config.RECORD_ID +"."+ SkillDescription.GetName(config);
            }
            else
            {
                showName.text = SkillDescription.GetName(config);
            }

            ATTitle.text = Translate.Get("at_title");
            HPTitle.text = Translate.Get("hp_title");

            ShowSkillStoneExType(ex1Icon, ex2Icon, ex3Icon, config.SP_LEVEL);
            ShowSKillRanges(close, near, far, config.AIAttrs.AI_MIN_DIS, config.AIAttrs.AI_MAX_DIS);
            atIcon.SetActive(BehaviorTypeUtility.IsAttackIconState(config.STATE_TYPE));
            defenceIcon.SetActive(BehaviorTypeUtility.IsDefenceIconState(config.STATE_TYPE));

            skillIntro.text = SkillDescription.GetIntro(config);

            PowerShow(config.RECORD_ID, 1);
        }

        void PowerShow(string RECORD_ID, int level)
        {
            var row = PowerEstimateTable.Find_RECORD_ID(RECORD_ID);
            float.TryParse(row.HP, out float hp);
            float.TryParse(row.EstimateDamage, out float at);
            AT.text = FightGlobalSetting.ATCal(at, level).ToString();
            HP.text = FightGlobalSetting.StoneHpCal(hp, level).ToString();
        }

        public static void ShowSKillRanges(GameObject close, GameObject near, GameObject far, float disMIN, float disMAX)
        {
            close.SetActive(SkillConfig.RangeLimit(disMIN, disMAX, true, false, false));
            near.SetActive(SkillConfig.RangeLimit(disMIN, disMAX, false, true, false));
            far.SetActive(SkillConfig.RangeLimit(disMIN, disMAX, false, false, true));
        }

        public static void ShowSkillStoneExType(GameObject ex1Icon, GameObject ex2Icon, GameObject ex3Icon, int eX)
        {
            switch (eX)
            {
                case 0:
                    ex1Icon.SetActive(false);
                    ex2Icon.SetActive(false);
                    ex3Icon.SetActive(false);
                break;
                case 1:
                    ex1Icon.SetActive(true);
                    ex2Icon.SetActive(false);
                    ex3Icon.SetActive(false);
                break;
                case 2:
                    ex1Icon.SetActive(true);
                    ex2Icon.SetActive(true);
                    ex3Icon.SetActive(false);
                break;
                case 3:
                    ex1Icon.SetActive(true);
                    ex2Icon.SetActive(true);
                    ex3Icon.SetActive(true);
                break;
                case -1:
                    ex1Icon.SetActive(false);
                    ex2Icon.SetActive(false);
                    ex3Icon.SetActive(false);
                break;
            }
        }
    }
}
