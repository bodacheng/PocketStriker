using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ClickNextTutorial : MonoBehaviour
{
    [SerializeField] private Button Btn;
    [SerializeField] private GameObject[] TutorialLayers;
    [SerializeField] private float clickDelay = 1f;

    private int pageIndex = 0;
    void Awake()
    {
        Btn.onClick.AddListener(NextPage);
    }

    public void Open()
    {
        gameObject.SetActive(true);
        pageIndex = -1;
        NextPage();
    }
    
    async void NextPage()
    {
        Btn.interactable = false;
        void ClosePages()
        {
            foreach (var tutorialLayer in TutorialLayers)
            {
                tutorialLayer.SetActive(false);
            }
        }
        
        pageIndex += 1;
        if (pageIndex < TutorialLayers.Length)
        {
            ClosePages();
            TutorialLayers[pageIndex].SetActive(true);
            var specialEventPage = TutorialLayers[pageIndex].GetComponent<TutorialLayerSpecialEvent>();
            if (specialEventPage != null)
                specialEventPage.onClick.Invoke();
        }
        else
        {
            ClosePages();
            gameObject.SetActive(false);
        }

        await UniTask.Delay(TimeSpan.FromSeconds(clickDelay));
        Btn.interactable = true;
    }
}
