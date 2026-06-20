using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Free hand-drag panning of the grid inside a clipped viewport: hold the middle mouse
    /// button and drag to move the map anywhere — left, right, up, down — with no scroll and
    /// no clamping. The grid is moved with a translate transform, which worldBound-based
    /// hit-testing accounts for, so cell picking and dragging keep working. The left mouse
    /// button is untouched (the grid ignores button 2, so the press bubbles up to here).
    /// </summary>
    internal sealed class GridPanController
    {
        private const int MiddleButton = 2;

        private readonly VisualElement _viewport;
        private readonly VisualElement _content;

        private Vector2 _offset;
        private bool _panning;
        private int _pointerId = -1;
        private Vector2 _startMouse;
        private Vector2 _startOffset;

        public GridPanController(VisualElement viewport, VisualElement content)
        {
            _viewport = viewport;
            _content = content;

            // TrickleDown so the middle-button press is intercepted before the cells underneath.
            _viewport.RegisterCallback<PointerDownEvent>(OnPointerDown, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerMoveEvent>(OnPointerMove, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerUpEvent>(OnPointerUp, TrickleDown.TrickleDown);
            _viewport.RegisterCallback<PointerCaptureOutEvent>(_ => _panning = false);

            // Center the map once both the viewport and grid have a resolved size.
            EventCallback<GeometryChangedEvent> initCenter = null;
            initCenter = _ =>
            {
                if (_viewport.contentRect.width <= 0f || _content.layout.width <= 0f)
                    return;

                CenterContent();
                _content.UnregisterCallback(initCenter);
            };
            _content.RegisterCallback(initCenter);

            Apply();
        }

        /// <summary>Recenters the map in the viewport.</summary>
        public void ResetPosition() => CenterContent();

        private void CenterContent()
        {
            Vector2 view = _viewport.contentRect.size;
            Vector2 content = _content.layout.size;
            if (view.x <= 0f || content.x <= 0f)
                return; // not laid out yet

            _offset = new Vector2((view.x - content.x) * 0.5f, (view.y - content.y) * 0.5f);
            Apply();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != MiddleButton)
                return;

            _panning = true;
            _pointerId = evt.pointerId;
            _startMouse = evt.position;
            _startOffset = _offset;
            _viewport.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_panning || evt.pointerId != _pointerId)
                return;

            // Grab-and-drag: the map follows the cursor 1:1, freely in any direction.
            Vector2 delta = (Vector2)evt.position - _startMouse;
            _offset = _startOffset + delta;
            Apply();
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!_panning || evt.pointerId != _pointerId)
                return;

            if (_viewport.HasPointerCapture(_pointerId))
                _viewport.ReleasePointer(_pointerId);

            _panning = false;
            _pointerId = -1;
            evt.StopPropagation();
        }

        private void Apply() => _content.style.translate = new Translate(_offset.x, _offset.y);
    }
}
