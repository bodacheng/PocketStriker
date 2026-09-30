using System.Collections.Generic;
using mainMenu;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace ModelView
{
    public partial class DedicatedCameraConnector
    {
        bool _uiPresentationEnabled;
        RawImage _uiPresentationImage;
        RenderTexture _uiPresentationTexture;
        Vector3 _originalModelPosition;
        bool _modelPositionReserved;
        int _presentationSlot;
        static readonly HashSet<int> PresentationSlots = new HashSet<int>();

        /// <summary>
        /// Composes this model with its UI panel instead of behind the overlay canvas.
        /// The connector's RectTransform remains the display bounds and touch target.
        /// Other model viewers retain their original camera-stack presentation.
        /// </summary>
        public void EnableUIPresentation(RectTransform presentationParent = null)
        {
            rect = (RectTransform)transform;
            if (presentationParent != null && transform.parent != presentationParent)
                transform.SetParent(presentationParent, false);
            if (_uiPresentationEnabled) return;
            _uiPresentationEnabled = true;
            if (!_modelPositionReserved)
            {
                _originalModelPosition = modelPos;
                // Camera masks are shared by model viewers. Keep simultaneous previews
                // in separate world spaces so each camera sees only its own character.
                _presentationSlot = 1;
                while (PresentationSlots.Contains(_presentationSlot)) _presentationSlot++;
                PresentationSlots.Add(_presentationSlot);
                var offset = Vector3.right * (4096f * _presentationSlot);
                modelPos += offset;
                MovePreviewModels(offset);
                _modelPositionReserved = true;
            }
            if (_uiPresentationImage == null)
            {
                var surface = new GameObject("Model UI Presentation", typeof(RectTransform), typeof(RawImage));
                surface.layer = gameObject.layer;
                surface.transform.SetParent(transform, false);
                _uiPresentationImage = surface.GetComponent<RawImage>();
                _uiPresentationImage.raycastTarget = false;
                _uiPresentationImage.color = Color.white;
                var imageRect = _uiPresentationImage.rectTransform;
                imageRect.anchorMin = Vector2.zero;
                imageRect.anchorMax = Vector2.one;
                imageRect.offsetMin = imageRect.offsetMax = Vector2.zero;
            }
            _uiPresentationImage.gameObject.SetActive(true);
            _uiPresentationImage.transform.SetAsFirstSibling();
            ConfigureUIPresentationCamera();
            if (target != null) CameraPositionCal();
        }

        public void DisableUIPresentation()
        {
            if (!_uiPresentationEnabled) return;
            _uiPresentationEnabled = false;
            ReleaseUIPresentationTexture();
            ReleaseUIPresentationSlot(true);
            if (_uiPresentationImage != null) _uiPresentationImage.gameObject.SetActive(false);
            if (camera == null) return;
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.ResetAspect();
            if (PreScene.target != null) PreScene.target.CameraStackToPostProcess(camera);
            if (target != null)
            {
                CameraPositionCal();
                CameraTreat(camera, true);
            }
        }

        void ConfigureUIPresentationCamera()
        {
            if (camera == null) return;
            DetachFromStack(PreScene.target != null ? PreScene.target.postProcessCamera : null, true);
            DetachFromStack(PreScene.target != null ? PreScene.target.noPostProcessCamera : null, false);
            var data = camera.GetComponent<UniversalAdditionalCameraData>();
            if (data != null)
            {
                data.renderType = CameraRenderType.Base;
                // URP post processing writes opaque alpha. Preserve the transparent
                // background here; the UI canvas performs the final composition.
                data.renderPostProcessing = false;
            }
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.clear;
            camera.allowHDR = false;
            camera.allowMSAA = false;
            camera.rect = new Rect(0, 0, 1, 1);
            EnsureUIPresentationTexture();
        }

        void DetachFromStack(Camera baseCamera, bool postProcess)
        {
            if (baseCamera == null || baseCamera == camera) return;
            var data = baseCamera.GetComponent<UniversalAdditionalCameraData>();
            if (data == null || data.renderType != CameraRenderType.Base || !data.cameraStack.Remove(camera)) return;
            if (!postProcess) return;
            data.renderPostProcessing = data.cameraStack.Count == 0;
            for (int index = 0; index < data.cameraStack.Count; index++)
            {
                var stacked = data.cameraStack[index];
                if (stacked == null) continue;
                var stackedData = stacked.GetComponent<UniversalAdditionalCameraData>();
                if (stackedData != null) stackedData.renderPostProcessing = index == data.cameraStack.Count - 1;
            }
        }

        void EnsureUIPresentationTexture()
        {
            if (rect == null || camera == null) return;
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            int width = Mathf.Clamp(Mathf.CeilToInt(rect.rect.width * scale), 64, 1536);
            int height = Mathf.Clamp(Mathf.CeilToInt(rect.rect.height * scale), 64, 1536);
            if (_uiPresentationTexture != null && _uiPresentationTexture.width == width && _uiPresentationTexture.height == height) return;
            ReleaseUIPresentationTexture();
            _uiPresentationTexture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
            {
                name = "Preparation Model " + GetEntityId(),
                antiAliasing = 1,
                useMipMap = false,
                filterMode = FilterMode.Bilinear
            };
            _uiPresentationTexture.Create();
            camera.targetTexture = _uiPresentationTexture;
            camera.aspect = (float)width / height;
            if (_uiPresentationImage != null) _uiPresentationImage.texture = _uiPresentationTexture;
        }

        void UpdateUIPresentationCamera(bool resetPos)
        {
            EnsureUIPresentationTexture();
            if (camera == null || rect == null) return;
            wid = Mathf.Max(1, rect.rect.width);
            hei = Mathf.Max(1, rect.rect.height);
            camera.orthographicSize = Mathf.Max(0.01f,
                Mathf.Max(_basicOrthographicSize, _targetBounds.extents.x / camera.aspect) * 1.04f);
            var position = _targetBounds.center + Vector3.forward * (_targetBounds.extents.z + extraZDis);
            // UI relocation is immediate; easing through the large isolation offset
            // would leave the surface empty during layout and route changes.
            camera.transform.position = position;
            camera.farClipPlane = Mathf.Max(camera.nearClipPlane + 1f, _targetBounds.extents.z * 2 + extraZDis + extraZCameraDepth);
        }

        void ReleaseUIPresentationTexture()
        {
            if (_uiPresentationTexture == null) return;
            if (camera != null && camera.targetTexture == _uiPresentationTexture) camera.targetTexture = null;
            if (_uiPresentationImage != null) _uiPresentationImage.texture = null;
            _uiPresentationTexture.Release();
            if (Application.isPlaying) Destroy(_uiPresentationTexture);
            else DestroyImmediate(_uiPresentationTexture);
            _uiPresentationTexture = null;
        }

        void MovePreviewModels(Vector3 offset)
        {
            bool targetMoved = false;
            foreach (var saved in _saves.Values)
            {
                if (saved == null || saved.WholeT == null) continue;
                saved.WholeT.position += offset;
                targetMoved |= saved.WholeT == target;
            }
            if (target != null && !targetMoved) target.position += offset;
        }

        void ReleaseUIPresentationSlot(bool restorePosition)
        {
            if (!_modelPositionReserved) return;
            if (restorePosition) MovePreviewModels(_originalModelPosition - modelPos);
            modelPos = _originalModelPosition;
            PresentationSlots.Remove(_presentationSlot);
            _modelPositionReserved = false;
            _presentationSlot = 0;
        }
    }
}
