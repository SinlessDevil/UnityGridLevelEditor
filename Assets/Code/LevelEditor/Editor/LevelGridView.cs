using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Renders a <see cref="LevelMatrixEditor"/> grid as a matrix of clickable cells.
    /// Replaces the Odin TableMatrix + IMGUI cell drawing.
    /// </summary>
    public class LevelGridView : VisualElement
    {
        private LevelMatrixEditor _level;
        private VisualElement[,] _cells;

        private readonly List<Vector2Int> _selection = new();
        private Vector2Int? _selectionStart;
        private Vector2Int? _hoverCell;

        // Drag-to-move state (left button drag of a filled cell onto another cell).
        private const float DragThreshold = 4f;
        private Vector2Int? _moveSource;
        private Vector2 _pointerDownPos;
        private bool _moving;
        private int _movePointerId = -1;
        private VisualElement _moveGhost;

        /// <summary>
        /// Raised on right click. Arguments are the clicked cell position and the
        /// mouse position in panel coordinates (for placing the popup).
        /// </summary>
        public event Action<Vector2Int, Vector2> CellRightClicked;

        public IReadOnlyList<Vector2Int> Selection => _selection;

        /// <summary>
        /// Element the drag ghost is parented to. Should be the window root
        /// (overlays in <c>panel.visualTree</c> render unreliably in an EditorWindow).
        /// Falls back to the panel root if not set.
        /// </summary>
        public VisualElement GhostHost { get; set; }

        public LevelGridView()
        {
            AddToClassList("le-grid");
        }

        public void SetLevel(LevelMatrixEditor level)
        {
            _level = level;
            _selection.Clear();
            _selectionStart = null;
            Rebuild();
        }

        /// <summary>Finds the grid cell under a panel-space position (e.g. a pointer event position).</summary>
        public bool TryGetCellAt(Vector2 panelPosition, out Vector2Int cellPos)
        {
            cellPos = default;

            if (_cells == null || _level == null)
                return false;

            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                if (_cells[x, y].worldBound.Contains(panelPosition))
                {
                    cellPos = new Vector2Int(x, y);
                    return true;
                }
            }

            return false;
        }

        /// <summary>Highlights a single cell as a drop target (pass null to clear).</summary>
        public void SetHoverCell(Vector2Int? cell)
        {
            if (_hoverCell.HasValue && IsValid(_hoverCell.Value))
                _cells[_hoverCell.Value.x, _hoverCell.Value.y].RemoveFromClassList("le-cell--drop-target");

            _hoverCell = cell;

            if (cell.HasValue && IsValid(cell.Value))
                _cells[cell.Value.x, cell.Value.y].AddToClassList("le-cell--drop-target");
        }

        private bool IsValid(Vector2Int p) =>
            _cells != null && _level != null &&
            p.x >= 0 && p.x < _level.Width && p.y >= 0 && p.y < _level.Height;

        public void Rebuild()
        {
            _moveGhost?.RemoveFromHierarchy();
            _moveGhost = null;
            _moveSource = null;
            _moving = false;
            _movePointerId = -1;

            Clear();
            _cells = null;
            _hoverCell = null;

            if (_level == null)
                return;

            _level.EnsureInitialized();
            _cells = new VisualElement[_level.Width, _level.Height];

            for (int y = 0; y < _level.Height; y++)
            {
                var row = new VisualElement();
                row.AddToClassList("le-grid__row");

                for (int x = 0; x < _level.Width; x++)
                {
                    var cell = BuildCell(x, y);
                    _cells[x, y] = cell;
                    row.Add(cell);
                    UpdateCell(x, y);
                }

                Add(row);
            }
        }

        /// <summary>Re-reads cell data and selection state into the existing visuals.</summary>
        public void RefreshCells()
        {
            if (_cells == null || _level == null)
                return;

            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                UpdateCell(x, y);
        }

        private VisualElement BuildCell(int x, int y)
        {
            var cell = new VisualElement { name = $"cell_{x}_{y}" };
            cell.AddToClassList("le-cell");

            var icon = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore };
            icon.AddToClassList("le-cell__icon");
            cell.Add(icon);

            var arrow = new Label("↑") { name = "arrow", pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("le-cell__arrow");
            cell.Add(arrow);

            var id = new Label { name = "id", pickingMode = PickingMode.Ignore };
            id.AddToClassList("le-cell__id");
            cell.Add(id);

            var pos = new Vector2Int(x, y);
            cell.RegisterCallback<PointerDownEvent>(evt => OnCellPointerDown(evt, cell, pos));
            cell.RegisterCallback<PointerMoveEvent>(OnCellPointerMove);
            cell.RegisterCallback<PointerUpEvent>(evt => OnCellPointerUp(evt, cell));
            cell.RegisterCallback<PointerCaptureOutEvent>(_ => CancelMove());

            return cell;
        }

        private void OnCellPointerDown(PointerDownEvent evt, VisualElement cell, Vector2Int pos)
        {
            bool ctrl = evt.ctrlKey || evt.commandKey;

            if (evt.button == 1)
            {
                if (!_selection.Contains(pos))
                {
                    _selection.Clear();
                    _selection.Add(pos);
                    RefreshCells();
                }

                CellRightClicked?.Invoke(pos, evt.position);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            if (ctrl)
            {
                if (!_selectionStart.HasValue)
                {
                    _selectionStart = pos;
                    _selection.Clear();
                    _selection.Add(pos);
                }
                else
                {
                    SelectRange(_selectionStart.Value, pos);
                    _selectionStart = null;
                }

                RefreshCells();
                evt.StopPropagation();
                return;
            }

            // Plain left button: pending click-or-drag. We decide on move/up:
            // moved past the threshold over a filled cell -> move; otherwise -> click.
            _moveSource = pos;
            _pointerDownPos = evt.position;
            _moving = false;
            _movePointerId = evt.pointerId;
            cell.CapturePointer(evt.pointerId);
            evt.StopPropagation();
        }

        private void OnCellPointerMove(PointerMoveEvent evt)
        {
            if (!_moveSource.HasValue || evt.pointerId != _movePointerId)
                return;

            if (!_moving)
            {
                if (Vector2.Distance(evt.position, _pointerDownPos) < DragThreshold)
                    return;

                var source = _level.GetCell(_moveSource.Value);
                if (source?.Block == null)
                    return; // nothing to carry; stays a click

                _moving = true;
                CreateMoveGhost(source.Block);
            }

            UpdateMoveGhost(evt.position);
            SetHoverCell(TryGetCellAt(evt.position, out var cell) ? cell : (Vector2Int?)null);
        }

        private void OnCellPointerUp(PointerUpEvent evt, VisualElement cell)
        {
            if (!_moveSource.HasValue || evt.pointerId != _movePointerId)
                return;

            var source = _moveSource.Value;

            if (_moving)
            {
                if (TryGetCellAt(evt.position, out var target) && target != source && _level.InBounds(target))
                    MoveBlock(source, target);
            }
            else
            {
                // Treated as a plain click: clear selection (matches old behaviour).
                _selectionStart = null;
                _selection.Clear();
                RefreshCells();
            }

            if (cell.HasPointerCapture(evt.pointerId))
                cell.ReleasePointer(evt.pointerId);

            EndMove();
            evt.StopPropagation();
        }

        private void MoveBlock(Vector2Int source, Vector2Int target)
        {
            var from = _level.GetCell(source);
            var to = _level.GetCell(target);

            to.Block = from.Block;
            to.Rotation = from.Rotation;

            from.Block = null;
            from.Rotation = Quaternion.identity;

            EditorUtility.SetDirty(_level);
            RefreshCells();
        }

        private void CancelMove()
        {
            if (_moveSource.HasValue)
                EndMove();
        }

        private void EndMove()
        {
            _moveSource = null;
            _moving = false;
            _movePointerId = -1;
            SetHoverCell(null);

            if (_moveGhost != null)
            {
                _moveGhost.RemoveFromHierarchy();
                _moveGhost = null;
            }
        }

        private void CreateMoveGhost(BlockDataEditor block)
        {
            _moveGhost?.RemoveFromHierarchy();

            _moveGhost = new VisualElement { pickingMode = PickingMode.Ignore };
            _moveGhost.AddToClassList("le-drag-ghost");

            if (block.Icon != null)
                _moveGhost.style.backgroundImage = Background.FromSprite(block.Icon);

            var host = GhostHost ?? panel?.visualTree;
            host?.Add(_moveGhost);
        }

        private void UpdateMoveGhost(Vector2 panelPosition)
        {
            if (_moveGhost == null)
                return;

            float half = _moveGhost.resolvedStyle.width > 0 ? _moveGhost.resolvedStyle.width / 2f : 28f;
            _moveGhost.style.left = panelPosition.x - half;
            _moveGhost.style.top = panelPosition.y - half;
        }

        private void SelectRange(Vector2Int start, Vector2Int end)
        {
            _selection.Clear();

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                _selection.Add(new Vector2Int(x, y));
        }

        private void UpdateCell(int x, int y)
        {
            var cell = _cells[x, y];
            var data = _level.GetCell(x, y);

            var icon = cell.Q<VisualElement>("icon");
            var arrow = cell.Q<Label>("arrow");
            var id = cell.Q<Label>("id");

            bool hasBlock = data?.Block != null && data.Block.Icon != null;

            if (hasBlock)
            {
                icon.style.display = DisplayStyle.Flex;
                icon.style.backgroundImage = Background.FromSprite(data.Block.Icon);

                id.style.display = DisplayStyle.Flex;
                id.text = data.Block.ID;

                arrow.style.display = DisplayStyle.Flex;
                float angle = data.Rotation.eulerAngles.y;
                arrow.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            }
            else
            {
                icon.style.display = DisplayStyle.None;
                id.style.display = DisplayStyle.None;
                arrow.style.display = DisplayStyle.None;
            }

            cell.EnableInClassList("le-cell--empty", !hasBlock);
            cell.EnableInClassList("le-cell--selected", _selection.Contains(new Vector2Int(x, y)));
        }
    }
}
