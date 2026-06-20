using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Drives drag-and-drop of a block from the palette onto the grid. While the
    /// cursor is over the grid the placement is previewed through the grid's
    /// footprint stamp; a floating ghost is shown only while outside the grid.
    /// </summary>
    public class BlockDragController
    {
        private readonly VisualElement _ghostHost;
        private readonly Func<LevelGridView> _getGrid;

        private VisualElement _ghost;
        private BlockDataEditor _block;
        private int _pointerId = -1;

        public BlockDragController(VisualElement ghostHost, Func<LevelGridView> getGrid)
        {
            _ghostHost = ghostHost;
            _getGrid = getGrid;
        }

        /// <summary>Makes a palette tile a drag source for the given block.</summary>
        public void AttachTile(VisualElement tile, BlockDataEditor block)
        {
            tile.RegisterCallback<PointerDownEvent>(evt => OnPointerDown(tile, block, evt));
            tile.RegisterCallback<PointerMoveEvent>(OnPointerMove);
            tile.RegisterCallback<PointerUpEvent>(evt => OnPointerUp(tile, evt));
            tile.RegisterCallback<PointerCaptureOutEvent>(_ => Cancel());
        }

        private void OnPointerDown(VisualElement tile, BlockDataEditor block, PointerDownEvent evt)
        {
            if (evt.button != 0 || block == null)
                return;

            _block = block;
            _pointerId = evt.pointerId;

            _getGrid?.Invoke()?.BeginExternalStamp(block);

            CreateGhost(block);
            UpdateGhost(evt.position);

            tile.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_block == null || evt.pointerId != _pointerId)
                return;

            var grid = _getGrid?.Invoke();
            bool overGrid = grid != null && grid.TryGetCellAt(evt.position, out _);

            // The snapped footprint stamp takes over once we are over the grid;
            // the floating ghost is just a carry indicator outside of it.
            _ghost.style.display = overGrid ? DisplayStyle.None : DisplayStyle.Flex;
            UpdateGhost(evt.position);

            grid?.UpdateExternalStamp(evt.position);
        }

        private void OnPointerUp(VisualElement tile, PointerUpEvent evt)
        {
            if (_block == null || evt.pointerId != _pointerId)
                return;

            var grid = _getGrid?.Invoke();
            grid?.TryPlaceExternalStamp(evt.position);
            grid?.ClearExternalStamp();

            if (tile.HasPointerCapture(evt.pointerId))
                tile.ReleasePointer(evt.pointerId);

            Cleanup();
            evt.StopPropagation();
        }

        private void Cancel()
        {
            if (_block == null)
                return;

            _getGrid?.Invoke()?.ClearExternalStamp();
            Cleanup();
        }

        private void Cleanup()
        {
            _block = null;
            _pointerId = -1;

            if (_ghost != null)
            {
                _ghost.RemoveFromHierarchy();
                _ghost = null;
            }
        }

        private void CreateGhost(BlockDataEditor block)
        {
            _ghost?.RemoveFromHierarchy();

            _ghost = new VisualElement { pickingMode = PickingMode.Ignore };
            _ghost.AddToClassList("le-drag-ghost");

            BlockIconResolver.Apply(_ghost, block);

            _ghostHost.Add(_ghost);
        }

        private void UpdateGhost(Vector2 panelPosition)
        {
            if (_ghost == null)
                return;

            float half = _ghost.resolvedStyle.width > 0 ? _ghost.resolvedStyle.width / 2f : 28f;
            _ghost.style.left = panelPosition.x - half;
            _ghost.style.top = panelPosition.y - half;
        }
    }
}
