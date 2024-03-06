using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UI;
#endif

    public enum SeType
    {
        Tap,
        Confirm,
        ExTab
    }

/// <summary>
/// 这个按钮扩展的bug：如果你设置Time.timeScale = 0，这个按钮会失效
/// </summary>
    public class BOButton : Button
    {
        static System.Action<SeType> playSe;
        public static void SetPlaySeMethod(System.Action<SeType> playSe)
        {
            if (playSe == null)
            {
                return;
            }
            BOButton.playSe = playSe;
        }

        private static readonly Color DisableColor = new Color32(0xC8, 0xC8, 0xC8, 0xFF);

        private const float DoubleClickInterval = 0.22f;
        private const float HoldTime = 0.8f;
        private const float RepeatHoldInterval = 0.1f;

        public static bool AnyProcess
        {
            get;
            private set;
        }

        static float pointerDownTime = 0;

        [System.Serializable] public class ButtonHoldEvent : UnityEvent { }
        [System.Serializable] public class ButtonDoubleClickEvent : UnityEvent { }


        [SerializeField, FormerlySerializedAs("onDoubleClick")] ButtonDoubleClickEvent doubleClick = new ButtonDoubleClickEvent();
        [SerializeField, FormerlySerializedAs("onHold")] ButtonHoldEvent hold = new ButtonHoldEvent();


        [SerializeField]
        private bool disableTransitionOverride = false;
        [SerializeField]
        private bool activateDoubleClick = false;
        [SerializeField]
        private bool activateHold = false;
        [SerializeField]
        private bool repeatHold = false;
        
        [SerializeField]
        private SeType sound = default;

        [SerializeField]
        private Sprite disableSprite = default;

       　[SerializeField]
        private Text text = default;

        private Sprite defaultSprite = default;
        private Image targetImage = default;

        private bool outOfRange = false;

        private bool executedDoubleClick = false;
        private readonly float[] clickTimeQueue = new float[2] { 0, 0 };

        private bool executedHold = false;
        private Coroutine singleClickCoroutine;
        private Coroutine holdWaitCoroutine;

        public ButtonDoubleClickEvent onDoubleClick => doubleClick;
        public ButtonHoldEvent onHold => hold;

        public bool ActivateDoubleClick
        {
            get => activateDoubleClick;
            set => activateDoubleClick = value;
        }

        public bool ActivateHold
        {
            get => activateHold;
            set => activateHold = value;
        }

        public new bool interactable
        {
            get => base.interactable;
            set
            {
                base.interactable = value;
                if (targetImage == null || disableSprite == null)
                {
                    return;
                }
                targetImage.sprite = value ? defaultSprite : disableSprite;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            if (disableSprite != null && targetGraphic != null && targetGraphic is Image image)
            {
                targetImage = image;
                defaultSprite = targetImage.sprite;
            }

            if (!disableTransitionOverride)
            {
                var c = colors;
                c.disabledColor = DisableColor;
                colors = c;
            }
        }
        
        public void ClearAllEvents()
        {
            onClick.RemoveAllListeners();
            onHold.RemoveAllListeners();
            doubleClick.RemoveAllListeners();
        }
        
        public static async void AnyButtonAsync()
        {
            AnyProcess = true;
            await UniTask.Delay(300);
            AnyProcess = false;
        }

        public void SetText(string txt)
        {
            if (text != null)
            {
                text.text = txt;
            }
        }

        public void SetActive(bool active)
        {
            transform.gameObject.SetActive(active);
        }

        public override void OnPointerClick(PointerEventData eventData)
        {
            if (activateDoubleClick)
            {
                clickTimeQueue[0] = clickTimeQueue[1];
                clickTimeQueue[1] = Time.time;

                if (clickTimeQueue[1] - clickTimeQueue[0] <= DoubleClickInterval)
                {
                    clickTimeQueue[0] = 0;
                    clickTimeQueue[1] = 0;
                    executedDoubleClick = true;
                }
            }

            if (!executedDoubleClick)
            {
                if (AnyProcess)
                {
                    return;
                }
                AnyButtonAsync();
            }

            if (!executedHold)
            {
                if (executedDoubleClick)
                {
                    //singleClickCoroutine = StartCoroutine(Wait(DoubleClickInterval, () => base.OnPointerClick(eventData)));
                    //StopCoroutine(singleClickCoroutine);
                    UISystemProfilerApi.AddMarker("Button.onDoubleClick", this);
                    onDoubleClick.Invoke();
                }
                // else if (activateDoubleClick)
                // {
                //     singleClickCoroutine = StartCoroutine(Wait(DoubleClickInterval, () => base.OnPointerClick(eventData)));
                //     Debug.Log("here:"+eventData );
                // }
                else
                {
                    if (interactable)
                    {
                        playSe?.Invoke(sound);
                    }
                    base.OnPointerClick(eventData);
                }
            }

            executedDoubleClick = false;
            executedHold = false;
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            base.OnPointerDown(eventData);
            if (!interactable)
            {
                return;
            }

            outOfRange = false;
            if (activateHold)
            {
                holdWaitCoroutine = StartCoroutine(Wait(HoldTime, ExecuteHold));
            }

            pointerDownTime = Time.time;
        }

        public override void OnPointerUp(PointerEventData eventData)
        {
            if (holdWaitCoroutine != null)
            {
                StopCoroutine(holdWaitCoroutine);
                holdWaitCoroutine = null;
            }

            base.OnPointerUp(eventData);
        }

        private void ExecuteHold()
        {
            if (!activateHold || outOfRange)
            {
                executedHold = false;
                return;
            }
            executedHold = true;

            UISystemProfilerApi.AddMarker("Button.onHold", this);
            onHold.Invoke();
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            outOfRange = true;
            base.OnPointerExit(eventData);
        }

        IEnumerator Wait(float time, System.Action complete)
        {
            yield return new WaitForSeconds(time);
            complete?.Invoke();
            while (repeatHold)
            {
                yield return new WaitForSeconds(RepeatHoldInterval);
                complete?.Invoke();
            }
        }
        
        public void SetListener(System.Action action)
        {
            onClick.RemoveAllListeners();
            onClick.AddListener(() => action?.Invoke());
        }

        /// <summary>
        /// onClick にタスクを設定する
        /// </summary>
        /// <param name="action">イベント</param>
        /// <param name="waitTask">タスクの終了待ちの間非活性にするか</param>
        /// <param name="endInteractable">タスクの終了後のinteractableに設定する値</param>
        public void SetListener(System.Func<UniTask> action, bool waitTask = true, bool endInteractable = true)
        {
            onClick.RemoveAllListeners();
            onClick.AddListener(() => UniTask.Void(async () =>
            {
                try
                {
                    if (action != null)
                    {
                        if (waitTask) interactable = false;
                        await action.Invoke();
                    }
                }
                finally
                {
                    if (waitTask) interactable = endInteractable;
                }
            }));
        }

        public static bool AllowClickTime()
        {
            return (Time.time - pointerDownTime) >= 0.2f;
        }

        public static float LastClickTime()
        {
            return pointerDownTime;
        }

        public static void SetClickTime()
        {
            pointerDownTime = Time.time;
        }
    }


#if UNITY_EDITOR

    [CustomEditor(typeof(BOButton), true)]
    [CanEditMultipleObjects]
    public class P2ButtonEditor : ButtonEditor
    {
        SerializedProperty disableSpriteProperty;

        SerializedProperty disableTransitionOverrideProperty;
        SerializedProperty activateDoubleClickProperty;
        SerializedProperty activateHoldProperty;

        SerializedProperty onDoubleClickProperty;
        SerializedProperty onHoldProperty;
        SerializedProperty onHoldRepeatProperty;
        
        SerializedProperty textProperty;
        SerializedProperty soundProperty;

        protected override void OnEnable()
        {
            base.OnEnable();
            disableSpriteProperty = serializedObject.FindProperty("disableSprite");

            disableTransitionOverrideProperty = serializedObject.FindProperty("disableTransitionOverride");
            activateDoubleClickProperty = serializedObject.FindProperty("activateDoubleClick");
            activateHoldProperty = serializedObject.FindProperty("activateHold");

            onDoubleClickProperty = serializedObject.FindProperty("doubleClick");
            onHoldProperty = serializedObject.FindProperty("hold");
            onHoldRepeatProperty = serializedObject.FindProperty("repeatHold");
            textProperty = serializedObject.FindProperty("text");
            
            soundProperty = serializedObject.FindProperty("sound");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(disableTransitionOverrideProperty);
            EditorGUILayout.PropertyField(disableSpriteProperty);
            EditorGUILayout.PropertyField(disableSpriteProperty);
            EditorGUILayout.PropertyField(textProperty);
            serializedObject.ApplyModifiedProperties();

            base.OnInspectorGUI();

            serializedObject.Update();
            EditorGUILayout.PropertyField(activateDoubleClickProperty);
            if (activateDoubleClickProperty.boolValue)
            {
                EditorGUILayout.PropertyField(onDoubleClickProperty);
            }

            EditorGUILayout.PropertyField(activateHoldProperty);
            EditorGUILayout.PropertyField(soundProperty);
            if (activateHoldProperty.boolValue)
            {
                EditorGUILayout.PropertyField(onHoldProperty);
                EditorGUILayout.PropertyField(onHoldRepeatProperty);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
#endif