using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using UniRx;

namespace mainMenu
{
    public partial class SkillStonesBox : MonoBehaviour
    {
        private int focusingExType;
        int FocusingExType
        {
            get => focusingExType;
            set
            {
                focusingExType = value;
                _tabEffects.SetSelectedTabPos(focusingExType);
            }
        }
        
        public void IniExTabs()
        {
            void Temp(Button btn, int exLevel)
            {
                btn.onClick.AddListener(() =>
                {
                    FocusingExType = exLevel;
                    RestFilter();
                });
            }
            Temp(NormalTab,0);
            Temp(EX1Tab,1);
            Temp(EX2Tab,2);
            Temp(EX3Tab,3);
        }
        
        public async UniTask IniExTabsEffects(Camera fxCamera, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            
            void IniExTab(Button btn, int exLevel)
            {
                var worldPos = PosCal.GetWorldPos(fxCamera, btn.GetComponent<RectTransform>(), 5f);
                _tabEffects.RefreshTagEffect(worldPos, exLevel);
                btn.onClick.AddListener(() =>
                {
                    //NormalTabFeature(PosCal.GetWorldPos(fxCamera, btn.GetComponent<RectTransform>(), 3));
                    _tabEffects.SkillButtonExplosion(exLevel, worldPos, _tabEffects.transform);
                });
            }
            
            await Observable.TimerFrame(5);
            
            IniExTab(NormalTab,0);
            IniExTab(EX1Tab,1);
            IniExTab(EX2Tab,2);
            IniExTab(EX3Tab,3);
            
            _tabEffects.SetSelectedTabPos(focusingExType);
        }

        public void PressTab(int exLevel)
        {
            switch (exLevel)
            {
                case 0:
                    NormalTab.onClick.Invoke();
                    break;
                case 1:
                    EX1Tab.onClick.Invoke();
                    break;
                case 2:
                    EX2Tab.onClick.Invoke();
                    break;
                case 3:
                    EX3Tab.onClick.Invoke();
                    break;
            }
        }
    }
}