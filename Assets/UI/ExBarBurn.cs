using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

public class ExBarBurn : MonoBehaviour
{
    [SerializeField] ParticleSystem explosionFigure;
    private CancellationTokenSource cancelTokenSource;
    
    private void OnDestroy()
    {
        cancelTokenSource.Cancel();
        Destroy(explosionFigure.gameObject);
    }

    void OnEnable()
    {
        cancelTokenSource = new CancellationTokenSource();
    }
    
    void OnDisable()
    {
        Burn(cancelTokenSource.Token).Forget();
    }

    async UniTask Burn(CancellationToken c)
    {
        await UniTask.DelayFrame(1, cancellationToken: c);
        if (c.IsCancellationRequested)
            return;
        explosionFigure.transform.SetParent(null);
        explosionFigure.transform.position = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, transform.GetComponent<RectTransform>(), 10);
        explosionFigure.Play();
    }
}
