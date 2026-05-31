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
    private bool _isRunning;

    void ColorChange(Image target, Color color1, Color color2)
    {
        if (!_isRunning || target == null)
        {
            return;
        }

        Tweener tweener = null;
        tweener = target.DOColor(color1, interval)
            .SetLink(target.gameObject)
            .OnComplete(() =>
            {
                _tweeners.Remove(tweener);
                ColorChange(target, color2, color1);
            });
        _tweeners.Add(tweener);
    }

    void ColorChange(Text textTarget, Color color1, Color color2)
    {
        if (!_isRunning || textTarget == null)
        {
            return;
        }

        Tweener tweener = null;
        tweener = textTarget.DOColor(color1, interval)
            .SetLink(textTarget.gameObject)
            .OnComplete(() =>
            {
                _tweeners.Remove(tweener);
                ColorChange(textTarget, color2, color1);
            });
        _tweeners.Add(tweener);
    }

    void OnEnable()
    {
        _isRunning = true;
        if (toChange != null)
        {
            for (int i = 0; i < toChange.Length; i++)
            {
                ColorChange(toChange[i], set1, set2);
            }
        }

        if (textTarget != null)
        {
            ColorChange(textTarget, set1, set2);
        }
    }

    void OnDisable()
    {
        _isRunning = false;
        foreach (Tweener tweener in _tweeners)
        {
            tweener.Kill();
        }
        _tweeners.Clear();
    }
}
