using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class ImageGliter : MonoBehaviour
{
    [SerializeField] Color set1, set2;
    [SerializeField] float interval = 1f;
    [SerializeField] Image[] toChange;
    [SerializeField] Text textTarget;
    
    private readonly List<Tweener> _tweeners = new List<Tweener>();

    void ColorChange(Image target, Color color1, Color color2)
    {
        Tweener tweener = target.DOColor(color1, interval).OnComplete(() => { ColorChange(target, color2, color1); });
        _tweeners.Add(tweener);
    }

    void ColorChange(Text textTarget, Color color1, Color color2)
    {
        Tweener tweener = textTarget.DOColor(color1, interval).OnComplete(() => { ColorChange(textTarget, color2, color1); });
        _tweeners.Add(tweener);
    }

    void OnEnable()
    {
        for (int i = 0; i < toChange.Length; i++)
        {
            ColorChange(toChange[i], set1, set2);
        }

        if (textTarget != null)
        {
            ColorChange(textTarget, set1, set2);
        }
    }

    void OnDisable()
    {
        foreach (Tweener tweener in _tweeners)
        {
            tweener.Kill();
        }
        _tweeners.Clear();
    }
}
