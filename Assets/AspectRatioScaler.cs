using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasScaler))]
public class AspectRatioScaler : MonoBehaviour
{
    [SerializeField] float referenceWidth = 1980f;
    [SerializeField] float referenceHeight = 1080f;

    private CanvasScaler _canvasScaler;
    
    private void Awake()
    {
        _canvasScaler = GetComponent<CanvasScaler>();
        Set();
    }

    private void Set()
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;
        float targetAspectRatio = referenceWidth / referenceHeight;
        float currentAspectRatio = screenWidth / screenHeight;

        if (currentAspectRatio >= targetAspectRatio)
        {
            // Screen width is greater than the target aspect ratio
            _canvasScaler.matchWidthOrHeight = 1f;
        }
        else
        {
            // Screen height is greater than the target aspect ratio
            _canvasScaler.matchWidthOrHeight = 0f;
        }
    }
}
