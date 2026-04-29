using System.Collections.Generic;
using UnityEngine;

namespace MCombat.Shared.Camera
{
    public abstract class CameraManagerCore : MonoBehaviour
    {
        public static UnityEngine.Camera _camera;
        public static UnityEngine.Camera _subCamera;
        public static UnityEngine.Camera _centerCamera;

        [SerializeField] protected UnityEngine.Camera mainCamera;
        [SerializeField] protected UnityEngine.Camera subCamera;
        [SerializeField] protected UnityEngine.Camera centerCamera;
        [SerializeField] protected Transform StartPosRef;
        [SerializeField] protected Transform topDownModeEndRef;
        [SerializeField] protected VisibilityControl visibilityControl;

        protected CameraModeCore CurrentMode;
        protected IDictionary<C_Mode, CameraModeCore> CModeDic;

        public Transform TopDownModeEndRef => topDownModeEndRef;
        public VisibilityControl VisibilityControl => visibilityControl;

        protected virtual bool ReenterSameModeOnAssign => false;

        protected abstract IDictionary<C_Mode, CameraModeCore> CreateModeDictionary();

        protected virtual void Awake()
        {
            _camera = mainCamera;
            _subCamera = subCamera;
            _centerCamera = centerCamera;

            if (_camera != null)
            {
                _camera.depthTextureMode = DepthTextureMode.Depth;
            }

            EnsureModeDictionary();
        }

        protected virtual void LateUpdate()
        {
            CurrentMode?.LocalUpdate(_camera);
        }

        public CameraModeCore GetMode(C_Mode mode)
        {
            EnsureModeDictionary().TryGetValue(mode, out var cameraMode);
            return cameraMode;
        }

        public void SetPosToStart()
        {
            if (_camera == null || StartPosRef == null)
            {
                return;
            }

            _camera.transform.position = StartPosRef.position;
            _camera.transform.rotation = StartPosRef.rotation;
        }

        public void Assign_Camera(C_Mode num, Transform me, List<Transform> targets, List<Transform> mes = null)
        {
            EnsureModeDictionary().TryGetValue(num, out var nextMode);
            var modeChanged = CurrentMode != nextMode;
            if (modeChanged || ReenterSameModeOnAssign)
            {
                CurrentMode?.Exit(_camera);
            }

            CurrentMode = nextMode;
            if (CurrentMode == null)
            {
                return;
            }

            CurrentMode.SetMeCenter(me);
            CurrentMode.targets = targets;
            CurrentMode.myTeamTargets = mes;
            if (modeChanged || ReenterSameModeOnAssign)
            {
                CurrentMode.Enter(_camera);
            }
        }

        IDictionary<C_Mode, CameraModeCore> EnsureModeDictionary()
        {
            if (CModeDic != null)
            {
                return CModeDic;
            }

            CModeDic = CreateModeDictionary() ?? new Dictionary<C_Mode, CameraModeCore>();
            foreach (var kv in CModeDic)
            {
                if (kv.Value is ICameraManagerLinkedMode linkedMode)
                {
                    linkedMode.SetCameraManager(this);
                }
            }

            return CModeDic;
        }
    }
}
