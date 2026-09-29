using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class ClickNextTutorial : MonoBehaviour
{
    [SerializeField] private Button Btn;
    [SerializeField] private GameObject[] TutorialLayers;
    [SerializeField] private float clickDelay = 1f;

    public Button Button => Btn;
    
    private int pageIndex = 0;
    private int pageVersion;
    void Awake()
    {
        Btn.onClick.AddListener(NextPage);
    }

    public void Open()
    {
        // The transparent full-screen button owns all clicks, including those
        // over tutorial labels, masks and the battle controls underneath.
        Btn.transform.SetAsLastSibling();
        gameObject.SetActive(true);
        pageIndex = -1;
        NextPage();
    }
    
    async void NextPage()
    {
        var version = ++pageVersion;
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
            return;
        }

        var cancelled = await UniTask.Delay(TimeSpan.FromSeconds(clickDelay), ignoreTimeScale: true,
            cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (!cancelled && version == pageVersion && isActiveAndEnabled)
            Btn.interactable = true;
    }

    void OnDisable()
    {
        ++pageVersion;
        Btn.interactable = false;
    }
}
