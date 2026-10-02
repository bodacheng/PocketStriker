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
        public int SelectedSpLevel => focusingExType;
        bool exTabsInitialized;
        Camera tabEffectCamera;
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
            if (exTabsInitialized) return;
            exTabsInitialized = true;
            void Temp(Button btn, int exLevel)
            {
                btn.onClick.AddListener(() =>
                {
                    ExTabPressed?.Invoke();
                    FocusingExType = exLevel;
                    RestFilter();
                    if (tabEffectCamera != null)
                        _tabEffects.SkillButtonExplosion(exLevel,
                            PosCal.GetWorldPos(tabEffectCamera, btn.GetComponent<RectTransform>(), 5f), _tabEffects.transform);
                });
            }
            Temp(NormalTab,0);
            Temp(EX1Tab,1);
            Temp(EX2Tab,2);
            Temp(EX3Tab,3);
        }

        public async UniTask IniExTabsEffects(Camera fxCamera, CancellationToken token = default)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            void IniExTab(Button btn, int exLevel)
            {
                _tabEffects.RefreshTagEffect(btn.GetComponent<RectTransform>(), fxCamera, exLevel);
            }

            await Observable.TimerFrame(5);
            if (token.IsCancellationRequested)
            {
                return;
            }

            tabEffectCamera = fxCamera;
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
