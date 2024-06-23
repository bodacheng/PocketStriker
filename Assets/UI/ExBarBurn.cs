using UnityEngine;

public class ExBarBurn : MonoBehaviour
{
    [SerializeField] ParticleSystem explosionFigure;
    
    private void OnDestroy()
    {
        if (explosionFigure != null)
        {
            Destroy(explosionFigure.gameObject);
        }
    }

    void OnDisable()
    {
        Burn();
    }

    void Burn()
    {
        if (explosionFigure != null)
        {
            explosionFigure.transform.position = PosCal.GetWorldPos(FightScene.FightScene.target.fxCamera, transform.GetComponent<RectTransform>(), 10);
            explosionFigure.Play();
        }
    }
}
