using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Drives drag-and-drop of a block from the palette onto a grid cell:
    /// a ghost follows the cursor, the target cell is highlighted, and on
    /// release the block is written into that cell.
    /// </summary>
    public class BlockDragController
    {
        private readonly VisualElement _ghostHost;
        private readonly Func<LevelGridView> _getGrid;
        private readonly Func<LevelMatrixEditor> _getLevel;
        private readonly Action _onPlaced;

        private VisualElement _ghost;
        private BlockDataEditor _block;
        private int _pointerId = -1;

        public BlockDragController(
            VisualElement ghostHost,
            Func<LevelGridView> getGrid,
            Func<LevelMatrixEditor> getLevel,
            Action onPlaced)
        {
            _ghostHost = ghostHost;
            _getGrid = getGrid;
            _getLevel = getLevel;
            _onPlaced = onPlaced;
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

            CreateGhost(block);
            UpdateGhost(evt.position);

            tile.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (_block == null || evt.pointerId != _pointerId)
                return;

            UpdateGhost(evt.position);

            var grid = _getGrid?.Invoke();
            grid?.SetHoverCell(grid.TryGetCellAt(evt.position, out var cell) ? cell : (Vector2Int?)null);
        }

        private void OnPointerUp(VisualElement tile, PointerUpEvent evt)
        {
            if (_block == null || evt.pointerId != _pointerId)
                return;

            var grid = _getGrid?.Invoke();
            if (grid != null && grid.TryGetCellAt(evt.position, out var cell))
                PlaceBlock(cell);

            if (tile.HasPointerCapture(evt.pointerId))
                tile.ReleasePointer(evt.pointerId);

            Cleanup();
            evt.StopPropagation();
        }

        private void PlaceBlock(Vector2Int cellPos)
        {
            var level = _getLevel?.Invoke();
            if (level == null || !level.InBounds(cellPos))
                return;

            var cell = level.GetCell(cellPos);
            cell.Block = _block;
            cell.Rotation = Quaternion.identity;

            EditorUtility.SetDirty(level);
            _onPlaced?.Invoke();
        }

        private void Cancel()
        {
            if (_block == null)
                return;

            Cleanup();
        }

        private void Cleanup()
        {
            _block = null;
            _pointerId = -1;
            _getGrid?.Invoke()?.SetHoverCell(null);

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

            if (block.Icon != null)
                _ghost.style.backgroundImage = Background.FromSprite(block.Icon);

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
