using System.Linq;
using DG.Tweening;
using mainMenu;
using UnityEngine;

namespace ModelView
{
    public partial class DedicatedCameraConnector : MonoBehaviour
    {
        public static float Unit2DViewYoKoSpaceWhenAtRight(string unit_id)
        {
            switch (unit_id)
            {
                case "1":
                    return 220;
                case "2":
                    return 210;
                case "3":
                    return 0;
                case "4":
                    return 0;
                case "5":
                    return 100;
                case "6":
                    return 365;
                case "7":
                    return 0;
                default:
                    return 0;
            }
        }
        
        public static float Unit2DViewYoKoSpaceWhenAtLeft(string unit_id)
        {
            float returnValue;
            switch (unit_id)
            {
                case "1":
                    returnValue = 30;
                    break;
                case "2":
                    returnValue = 140;
                    break;
                case "3":
                    returnValue = 145;
                    break;
                case "4":
                    returnValue = 175;
                    break;
                case "5":
                    returnValue = 100;
                    break;
                case "6":
                    returnValue = 0;
                    break;
                case "7":
                    returnValue = 190;
                    break;
                default:
                    returnValue = 0;
                    break;
            }

            returnValue += PosCal.TempToko() ;
            return returnValue;
        }
        
        
        [SerializeField] private Transform target;
        [SerializeField] private Camera camera;
        [SerializeField] private float upDownRotateRangeMin = -10;
        [SerializeField] private float upDownRotateRangeMax = 45;
        [SerializeField] private float rotateSpeed = 90;
        [SerializeField] private float extraZDis;
        [SerializeField] private float extraZCameraDepth = 30f;
        [SerializeField] private float followSpeed = 2;
        private readonly Bounds tempBoundary = new Bounds();
        private RectTransform rect;
        private bool fixMode;
        
        public void Clear()
        {
            resetModelPosTween?.Kill();
            DestroyImmediate(camera.gameObject);
            if (target != null)
                DestroyImmediate(target.gameObject);
            DestroyImmediate(gameObject);
        }
        
        void Initialize(bool fixMode, Transform focus, Transform camerasHolder = null)
        {
            this.fixMode = fixMode;
            rect = transform.GetComponent<RectTransform>();
            target = focus;
            target.SetParent(transform);
            _parentNodeRenderer = target.GetComponent<Renderer>();
            _renderers = target.GetComponentsInChildren<Renderer>()
                .Where(x => x.GetComponent<ParticleSystem>() == null)
                .ToArray();
            
            foreach (var mesh in _renderers)
            {
                if (mesh is SkinnedMeshRenderer skinnedMesh)
                {
                    skinnedMesh.updateWhenOffscreen = true; // to fit camera bound
                }
            }
            
            wid = rect.rect.width;
            hei = rect.rect.height;
            camera.transform.SetParent(camerasHolder);
            PreScene.target.CameraStackToPostProcess(camera);
            
            // 初始化工作全部完成后才启用相机，否则会在之前造成黑屏
            camera.gameObject.SetActive(true);
            if (!this.fixMode)
                _basicOrthographicSize = CalMaxOrthographicSize();
            
            CameraTreat(camera, true);
        }

        void Update()
        {
            if (target != null) CameraPositionCal();
            if (IfShowingSkill)
            {
                SkillsPrintOutLateUpdate();
            }
        }
        
        private Renderer _parentNodeRenderer;
        private Bounds _targetBounds;
        private Renderer[] _renderers;
        private float _basicOrthographicSize;
        
        void CameraPositionCal()
        {
            // 合成Bounds計算
            _targetBounds = tempBoundary;
            if (_parentNodeRenderer != null)
            {
                _targetBounds = _parentNodeRenderer.bounds;
            }

            foreach (var render in _renderers)
            {
                if (render == null)
                    continue; 

                if (_targetBounds == tempBoundary)
                {
                    _targetBounds = render.bounds;
                }
                else
                {
                    _targetBounds.Encapsulate(render.bounds);
                }
            }
            
            if (fixMode)
            {
                _basicOrthographicSize = Mathf.Max(_targetBounds.extents.x, _targetBounds.extents.y);
            }
            
            CameraTreat(camera, false);
        }
        
        void CameraTreat(Camera _camera, bool resetPos)
        {
            var viewCenter = GetCenterPosition(rect);
            _camera.orthographicSize = _basicOrthographicSize * (PosCal.CanvasHeight / rect.rect.height);
            var cViewWidth = _camera.orthographicSize * 2 * _camera.aspect;
            var cViewHeight = _camera.orthographicSize * 2;
            var pos = _targetBounds.center + Vector3.forward * (_targetBounds.extents.z + extraZDis)
                                           + (0.5f -　(viewCenter.x / Screen.width)) * cViewWidth * _camera.transform.right 
                                           + (0.5f - (viewCenter.y / Screen.height)) * cViewHeight * Vector3.up;
            _camera.transform.position = resetPos ? pos : Vector3.Lerp(_camera.transform.position, pos, Time.deltaTime * followSpeed);
            _camera.farClipPlane = _targetBounds.extents.z * 2 + extraZDis + extraZCameraDepth;
        }
        
        static Vector2 GetCenterPosition(RectTransform rect)
        {
            var position = rect.transform.position;
            
            // 真ん中Pivotじゃなければ真ん中を計算する
            if (rect.pivot != new Vector2(0.5f, 0.5f))
            {
                var scaleX = rect.transform.lossyScale.x;
                var scaleY = rect.transform.lossyScale.y;
                var x = rect.rect.width / 2f * scaleX;
                var y = rect.rect.height / 2f * scaleY;
                position.x += Mathf.Lerp(x, -x, rect.pivot.x);
                position.y += Mathf.Lerp(y, -y, rect.pivot.y);
            }
            return position;
        }
        
        //回転用
        Vector2 sPos; //タッチした座標
        float wid = 100, hei = 100; //スクリーンサイズ
        float left_right, left_right_old,　up_down, up_down_old, _z; //変数
        bool canLeftRight = true, canUpDown = true;
        
        public void EnableRotateDirection(bool x, bool y)
        {
            canLeftRight = x;
            canUpDown = y;
        }
        
        public void RotateTarget(float left_right, float up_down, float Z = 0)
        {
            if (target == null) return;
            this.left_right = left_right;
            this.up_down = up_down;
            this._z = Z;
            target.localRotation = Quaternion.Euler(up_down, 0, Z);
            target.RotateAround(target.position, target.up, left_right);
        }
        
        public void OnPointerDown()
        {
            left_right_old = left_right;
            up_down_old = up_down;
            sPos = Input.mousePosition;
        }
        
        public void OnHold()
        {
            //回転
            left_right = left_right_old + (canLeftRight ? (sPos.x - Input.mousePosition.x) * rotateSpeed/ wid : 0); //横移動量(-1<tx<1)
            up_down = up_down_old + (canUpDown ? (sPos.y - Input.mousePosition.y) * rotateSpeed/ hei : 0); //縦移動量(-1<ty<1);
            up_down = Mathf.Clamp(up_down, upDownRotateRangeMin, upDownRotateRangeMax);
            RotateTarget(left_right, up_down, _z);
        }
        
        public void ItemDetailStartDirection(float x, float y, float z = 0)
        {
            up_down = y;
            if (y < upDownRotateRangeMin)
            {
                upDownRotateRangeMin = y;
            }
            if (y > upDownRotateRangeMax)
            {
                upDownRotateRangeMax = y;
            }
            
            up_down = Mathf.Clamp(up_down, upDownRotateRangeMin, upDownRotateRangeMax);
            RotateTarget(x, up_down, z);
            OnPointerDown();
        }
        
        /// <summary>
        /// For non fixMode
        /// </summary>
        /// <returns></returns>
        float CalMaxOrthographicSize()
        {
            float value = 0;
            RotateTarget(0,0);
            value = Mathf.Max(value, CalMaxExtend());
            RotateTarget(0,90);
            value = Mathf.Max(value, CalMaxExtend());
            RotateTarget(90,0);
            return Mathf.Max(value, CalMaxExtend());
        }
        
        float CalMaxExtend()
        {
            _targetBounds = tempBoundary;
            if (_parentNodeRenderer != null)
            {
                _targetBounds = _parentNodeRenderer.bounds;
            }

            foreach (Renderer render in _renderers)
            {
                if (_targetBounds == tempBoundary)
                {
                    _targetBounds = render.bounds;
                }
                else
                {
                    _targetBounds.Encapsulate(render.bounds);
                }
            }

            return Vector3.Distance(_targetBounds.max, _targetBounds.min) / 2f;
        }
    }
}
