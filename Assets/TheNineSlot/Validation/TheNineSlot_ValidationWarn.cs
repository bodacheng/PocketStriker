using UnityEngine;

namespace mainMenu
{
    public partial class TheNineSlot : MonoBehaviour
    {
        [SerializeField] RectTransform normalSkillIndicator;
        
        public SkillSet.SkillEditError ValidateWarn()
        {
            // 第一列技能必须有普通技能
            var valR = CheckEditBasedOnCurrent(PlayerAccountInfo.Me.tutorialProgress != "Finished");
            ValidationWarn(valR);
            comboShowBtn.gameObject.SetActive(valR == SkillSet.SkillEditError.Perfect && PlayerAccountInfo.Me.tutorialProgress == "Finished");
            dreamComboShowBtn.gameObject.SetActive(valR == SkillSet.SkillEditError.Perfect && PlayerAccountInfo.Me.tutorialProgress == "Finished");
            return valR;
        }
        
        public void ValidationWarn(SkillSet.SkillEditError skillEditError)
        {
            confirmBtnColorSwapper.ChangeColor(skillEditError == SkillSet.SkillEditError.Perfect ? Color.green : Color.white);
            validationWarn.gameObject.SetActive(true);
            normalSkillIndicator.gameObject.SetActive(false);
            overHeatIndicator.gameObject.SetActive(skillEditError == SkillSet.SkillEditError.UnBalanced);
            switch(skillEditError)
            {
                case SkillSet.SkillEditError.RepeatedSkill:
                    validationWarn.text = Translate.Get("CantEquipSameSkill");
                break;
                case SkillSet.SkillEditError.UnBalanced:
                    validationWarn.text = Translate.Get("UnBalanced");
                    break;
                case SkillSet.SkillEditError.NoNormalStart:
                    normalSkillIndicator.gameObject.SetActive(true);
                    validationWarn.text = Translate.Get("AColumnNeedNormal");
                    break;
                case SkillSet.SkillEditError.NotFull:
                    validationWarn.text = Translate.Get("NotFull");//"全てのスロットを満たしましょう！";
                    break;
                case SkillSet.SkillEditError.NoAtLeastTwoEx:
                    validationWarn.text = Translate.Get("AtLeastTwoEx");
                    break;
                case SkillSet.SkillEditError.Perfect:
                    validationWarn.gameObject.SetActive(false);
                    break;
            }
        }
        
        public void IntroAboutCombo(bool on, bool DreamCombo = false)
        {
            validationWarnSide.gameObject.SetActive(on);
            validationWarnSide.text = Translate.Get(DreamCombo ? "IntroOfDreamCombo" : "IntroOfCombo");
            
            randomBtn.gameObject.SetActive(!on);
            removeAllBtn.gameObject.SetActive(!on);
            ConfirmSkillChangeButton.gameObject.SetActive(!on);
            ResetButton.gameObject.SetActive(!on);
        }
    }
}