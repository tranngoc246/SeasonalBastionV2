using System;
using SeasonalBastion.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace SeasonalBastion
{
    public sealed class StrategyCameraController : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private Camera _cam;

        [Header("Pan")]
        [SerializeField] private float _keyboardPanSpeed = 20f;
        [SerializeField] private float _dragPanSpeed = 1.2f;
        [SerializeField] private bool _enableKeyboardPan = true;
        [SerializeField] private bool _enableMiddleMouseDrag = true;

        [Header("Zoom")]
        [SerializeField] private float _zoomSpeed = 8f;
        [SerializeField] private float _minZoom = 6f;
        [SerializeField] private float _maxZoom = 28f;
        [SerializeField] private bool _zoomToCursor = true;

        [Header("Bounds")]
        [SerializeField] private bool _clampToMap = true;
        [SerializeField] private float _boundsPadding = 2f;

        [Header("Focus")]
        [SerializeField] private Key _focusHqKey = Key.H;

        [Header("Smoothing")]
        [SerializeField] private bool _smoothMotion = true;
        [SerializeField] private float _moveSmoothTime = 0.08f;
        [SerializeField] private float _zoomSmoothTime = 0.08f;

        private GameServices _services;
        private IGridMap _grid;
        private Vector3 _targetPos;
        private Vector3 _moveVel;
        private float _targetZoom;
        private float _zoomVel;

        private bool _dragging;
        private Vector3 _dragOriginWorld;

        private void Awake()
        {
            if (_cam == null)
                _cam = GetComponent<Camera>();
            if (_cam == null)
                _cam = Camera.main;

            _targetPos = transform.position;
            _targetZoom = _cam != null ? _cam.orthographicSize : 10f;
        }

        private void Update()
        {
            TryBind();
            if (_cam == null)
                return;

            float dt = Time.unscaledDeltaTime;
            HandleFocusHotkeys();

            bool overUi = IsPointerOverUi();
            HandleKeyboardPan(dt);
            HandleZoom(dt);
            HandleDragPan();

            _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
            ClampTargetToMap();
            ApplyCamera();
        }

        public void FocusWorldPos(Vector3 worldPos)
        {
            _targetPos.x = worldPos.x;
            _targetPos.y = worldPos.y;
            ClampTargetToMap();
        }

        public void FocusCell(CellPos cell)
        {
            FocusWorldPos(new Vector3(cell.X + 0.5f, cell.Y + 0.5f, transform.position.z));
        }

        public void FocusHq()
        {
            if (_services?.WorldState?.Buildings != null)
            {
                foreach (var bid in _services.WorldState.Buildings.Ids)
                {
                    if (!_services.WorldState.Buildings.Exists(bid))
                        continue;

                    var bs = _services.WorldState.Buildings.Get(bid);
                    if (!bs.IsConstructed)
                        continue;

                    if (DefIdTierUtil.IsBase(bs.DefId, "bld_hq"))
                    {
                        FocusCell(bs.Anchor);
                        return;
                    }
                }
            }

            if (_grid != null)
            {
                float cx = (_grid.Width - 1) * 0.5f;
                float cy = (_grid.Height - 1) * 0.5f;
                FocusWorldPos(new Vector3(cx, cy, transform.position.z));
            }
        }

        private void HandleFocusHotkeys()
        {
            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb.hKey.wasPressedThisFrame)
            {
                TryBind();
                FocusHq();
            }
        }

        private void HandleKeyboardPan(float dt)
        {
            if (!_enableKeyboardPan)
                return;

            float x = 0f;
            float y = 0f;

            var kb = Keyboard.current;
            if (kb == null)
                return;

            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) y += 1f;

            var dir = new Vector3(x, y, 0f);
            if (dir.sqrMagnitude > 1f)
                dir.Normalize();

            _targetPos += dir * (_keyboardPanSpeed * dt);
        }

        private void HandleDragPan()
        {
            if (!_enableMiddleMouseDrag)
                return;

            var mouse = Mouse.current;
            if (mouse == null)
                return;

            if (mouse.middleButton.wasPressedThisFrame)
            {
                _dragging = true;
                _dragOriginWorld = ScreenToWorldOnCameraPlane(mouse.position.ReadValue());
            }
            else if (mouse.middleButton.wasReleasedThisFrame)
            {
                _dragging = false;
            }

            if (_dragging)
            {
                var now = ScreenToWorldOnCameraPlane(mouse.position.ReadValue());
                var delta = _dragOriginWorld - now;
                _targetPos += delta * _dragPanSpeed;
                _dragOriginWorld = now;
            }
        }

        private void HandleZoom(float dt)
        {
            var mouse = Mouse.current;
            if (mouse == null)
                return;

            float scroll = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.001f)
                return;

            var mousePos = mouse.position.ReadValue();
            if (_zoomToCursor)
            {
                var before = ScreenToWorldOnCameraPlane(mousePos);
                _targetZoom -= scroll * _zoomSpeed;
                _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);

                float oldZoom = _cam.orthographicSize;
                _cam.orthographicSize = _targetZoom;
                var after = ScreenToWorldOnCameraPlane(mousePos);
                _cam.orthographicSize = oldZoom;

                _targetPos += (before - after);
            }
            else
            {
                _targetZoom -= scroll * _zoomSpeed;
                _targetZoom = Mathf.Clamp(_targetZoom, _minZoom, _maxZoom);
            }
        }

        private void ClampTargetToMap()
        {
            if (!_clampToMap || _grid == null || _cam == null)
                return;

            float halfH = _targetZoom;
            float halfW = halfH * _cam.aspect;

            float minX = halfW - _boundsPadding;
            float maxX = (_grid.Width - 1) - halfW + _boundsPadding;
            float minY = halfH - _boundsPadding;
            float maxY = (_grid.Height - 1) - halfH + _boundsPadding;

            if (minX > maxX)
            {
                float cx = (_grid.Width - 1) * 0.5f;
                minX = maxX = cx;
            }

            if (minY > maxY)
            {
                float cy = (_grid.Height - 1) * 0.5f;
                minY = maxY = cy;
            }

            _targetPos.x = Mathf.Clamp(_targetPos.x, minX, maxX);
            _targetPos.y = Mathf.Clamp(_targetPos.y, minY, maxY);
        }

        private void ApplyCamera()
        {
            if (_smoothMotion)
            {
                transform.position = Vector3.SmoothDamp(
                    transform.position,
                    new Vector3(_targetPos.x, _targetPos.y, transform.position.z),
                    ref _moveVel,
                    _moveSmoothTime);

                float z = Mathf.SmoothDamp(
                    _cam.orthographicSize,
                    _targetZoom,
                    ref _zoomVel,
                    _zoomSmoothTime);

                _cam.orthographicSize = z;
            }
            else
            {
                transform.position = new Vector3(_targetPos.x, _targetPos.y, transform.position.z);
                _cam.orthographicSize = _targetZoom;
            }
        }

        private Vector3 ScreenToWorldOnCameraPlane(Vector3 screenPos)
        {
            float z = -_cam.transform.position.z;
            screenPos.z = z;
            return _cam.ScreenToWorldPoint(screenPos);
        }

        private bool IsPointerOverUi()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }

        private void TryBind()
        {
            if (_services != null)
                return;

            var behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < behaviours.Length; i++)
            {
                var mb = behaviours[i];
                if (mb == null)
                    continue;

                var services = ReadMember<GameServices>(mb, "_s", "Services", "services");
                if (services == null)
                    continue;

                _services = services;
                _grid = _services.GridMap;
                return;
            }
        }

        private static T ReadMember<T>(object obj, params string[] names) where T : class
        {
            if (obj == null || names == null)
                return null;

            var type = obj.GetType();
            const System.Reflection.BindingFlags flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic;

            for (int i = 0; i < names.Length; i++)
            {
                var n = names[i];
                if (string.IsNullOrEmpty(n))
                    continue;

                var f = type.GetField(n, flags);
                if (f != null && f.GetValue(obj) is T tvf)
                    return tvf;

                var p = type.GetProperty(n, flags);
                if (p != null && p.GetValue(obj) is T tvp)
                    return tvp;
            }

            return null;
        }
    }
}
