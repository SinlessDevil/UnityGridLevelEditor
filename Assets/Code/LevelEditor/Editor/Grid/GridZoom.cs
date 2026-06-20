using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Owns the grid zoom factor and applies it to cell layout. Cells scale by changing
    /// real layout size (not a transform), so all worldBound-based hit-testing / dragging
    /// keeps working and the ScrollView resizes its scrollbars correctly. Plates re-layout
    /// separately via the view's GeometryChanged callback.
    /// </summary>
    internal sealed class GridZoom
    {
        // These match the base .le-cell USS values.
        private const float BaseCellSize = 64f;
        private const float BaseIconSize = 56f;
        private const float BaseArrowFont = 18f;
        private const float BaseIdFont = 10f;

        private const float MinZoom = 0.25f;
        private const float MaxZoom = 2f;
        private const float ZoomStep = 1.25f;

        private readonly GridContext _ctx;
        private float _zoom = 1f;

        /// <summary>Raised after the zoom level changes.</summary>
        public event Action ZoomChanged;

        /// <summary>Current zoom factor (1 = 100%).</summary>
        public float Zoom => _zoom;

        public GridZoom(GridContext ctx) => _ctx = ctx;

        public void ZoomIn() => SetZoom(_zoom * ZoomStep);
        public void ZoomOut() => SetZoom(_zoom / ZoomStep);
        public void ResetZoom() => SetZoom(1f);

        public void SetZoom(float zoom)
        {
            zoom = Mathf.Clamp(zoom, MinZoom, MaxZoom);
            if (Mathf.Approximately(zoom, _zoom))
                return;

            _zoom = zoom;
            ApplyZoom();
            ZoomChanged?.Invoke();
        }

        /// <summary>Ctrl + mouse wheel zooms; a plain wheel keeps scrolling the grid.</summary>
        public void OnWheel(WheelEvent evt)
        {
            if (!(evt.ctrlKey || evt.commandKey))
                return;

            if (evt.delta.y < 0f)
                ZoomIn();
            else
                ZoomOut();

            evt.StopPropagation();
        }

        /// <summary>Resizes every cell (and its decor) to the current zoom.</summary>
        private void ApplyZoom()
        {
            if (_ctx.Cells == null)
                return;

            for (int y = 0; y < _ctx.Height; y++)
            for (int x = 0; x < _ctx.Width; x++)
                ApplyCellMetrics(_ctx.Cells[x, y]);
        }

        /// <summary>Applies zoom-scaled size/fonts to a single cell and its children.</summary>
        public void ApplyCellMetrics(VisualElement cell)
        {
            float size = BaseCellSize * _zoom;
            cell.style.width = size;
            cell.style.height = size;

            var icon = cell.Q<VisualElement>("icon");
            if (icon != null)
            {
                icon.style.width = BaseIconSize * _zoom;
                icon.style.height = BaseIconSize * _zoom;
            }

            var arrow = cell.Q<Label>("arrow");
            if (arrow != null)
                arrow.style.fontSize = BaseArrowFont * _zoom;

            var id = cell.Q<Label>("id");
            if (id != null)
                id.style.fontSize = BaseIdFont * _zoom;
        }
    }
}
