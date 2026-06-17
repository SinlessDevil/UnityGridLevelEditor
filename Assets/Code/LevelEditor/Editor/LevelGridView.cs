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
        private readonly List<Vector2Int> _hoverCells = new();

        // Drag-to-move state (left button drag of the selected cells onto another location).
        private const float DragThreshold = 4f;
        private Vector2Int? _moveSource;
        private Vector2 _pointerDownPos;
        private bool _moving;
        private int _movePointerId = -1;
        private readonly List<Vector2Int> _dragOffsets = new();  // cell offsets relative to the grabbed anchor
        private readonly List<StampIcon> _stampIcons = new();    // translucent preview, one per moved block

        private readonly struct StampIcon
        {
            public readonly Vector2Int Offset;
            public readonly VisualElement Element;

            public StampIcon(Vector2Int offset, VisualElement element)
            {
                Offset = offset;
                Element = element;
            }
        }

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
            if (cell.HasValue)
                SetHoverCells(new[] { cell.Value }, true);
            else
                SetHoverCells(Array.Empty<Vector2Int>(), true);
        }

        /// <summary>Highlights a set of cells as drop targets, tinted by validity.</summary>
        public void SetHoverCells(IEnumerable<Vector2Int> cells, bool valid)
        {
            foreach (var c in _hoverCells)
            {
                if (!IsValid(c))
                    continue;

                _cells[c.x, c.y].RemoveFromClassList("le-cell--drop-target");
                _cells[c.x, c.y].RemoveFromClassList("le-cell--drop-invalid");
            }

            _hoverCells.Clear();

            string cssClass = valid ? "le-cell--drop-target" : "le-cell--drop-invalid";
            foreach (var c in cells)
            {
                if (!IsValid(c))
                    continue;

                _cells[c.x, c.y].AddToClassList(cssClass);
                _hoverCells.Add(c);
            }
        }

        private bool IsValid(Vector2Int p) =>
            _cells != null && _level != null &&
            p.x >= 0 && p.x < _level.Width && p.y >= 0 && p.y < _level.Height;

        public void Rebuild()
        {
            ClearStamp();
            _moveSource = null;
            _moving = false;
            _movePointerId = -1;

            Clear();
            _cells = null;
            _hoverCells.Clear();

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

                if (!BeginDrag())
                    return; // nothing to carry; stays a click
            }

            UpdateStamp(evt.position);
        }

        /// <summary>Decides which cells are being dragged and builds the preview stamp.</summary>
        private bool BeginDrag()
        {
            var anchor = _moveSource.Value;

            // Dragging a cell that belongs to a multi-selection moves the whole selection.
            bool isGroup = _selection.Count > 1 && _selection.Contains(anchor);

            List<Vector2Int> group;
            if (isGroup)
            {
                group = new List<Vector2Int>(_selection);
            }
            else
            {
                if (_level.GetCell(anchor).Block == null)
                    return false; // empty single cell -> treat as a click

                group = new List<Vector2Int> { anchor };
            }

            _moving = true;
            BuildStamp(anchor, group);
            return true;
        }

        private void OnCellPointerUp(PointerUpEvent evt, VisualElement cell)
        {
            if (!_moveSource.HasValue || evt.pointerId != _movePointerId)
                return;

            if (_moving)
            {
                if (TryGetCellAt(evt.position, out var hovered))
                {
                    var delta = hovered - _moveSource.Value;
                    if (delta != Vector2Int.zero && IsDropValid(hovered))
                        MoveGroup(delta);
                }
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

        /// <summary>True when every dragged cell lands inside the grid for the hovered anchor.</summary>
        private bool IsDropValid(Vector2Int hovered)
        {
            foreach (var offset in _dragOffsets)
                if (!_level.InBounds(hovered + offset))
                    return false;

            return true;
        }

        private void MoveGroup(Vector2Int delta)
        {
            var anchor = _moveSource.Value;

            // Snapshot every source cell first: source and destination ranges can overlap.
            var sources = new List<Vector2Int>(_dragOffsets.Count);
            var blocks = new List<BlockDataEditor>(_dragOffsets.Count);
            var rotations = new List<Quaternion>(_dragOffsets.Count);

            foreach (var offset in _dragOffsets)
            {
                var src = anchor + offset;
                var c = _level.GetCell(src);
                sources.Add(src);
                blocks.Add(c.Block);
                rotations.Add(c.Rotation);
            }

            foreach (var src in sources)
            {
                var c = _level.GetCell(src);
                c.Block = null;
                c.Rotation = Quaternion.identity;
            }

            var moved = new List<Vector2Int>(sources.Count);
            for (int i = 0; i < sources.Count; i++)
            {
                var dst = sources[i] + delta;
                var c = _level.GetCell(dst);
                c.Block = blocks[i];
                c.Rotation = rotations[i];
                moved.Add(dst);
            }

            EditorUtility.SetDirty(_level);

            // Selection follows the moved cells.
            _selection.Clear();
            _selection.AddRange(moved);
            _selectionStart = null;

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
            SetHoverCells(Array.Empty<Vector2Int>(), true);
            ClearStamp();
        }

        // ---- Preview stamp ----

        private void BuildStamp(Vector2Int anchor, List<Vector2Int> group)
        {
            ClearStamp();

            var host = GhostHost ?? panel?.visualTree;

            foreach (var cellPos in group)
            {
                var offset = cellPos - anchor;
                _dragOffsets.Add(offset);

                var data = _level.GetCell(cellPos);
                if (host == null || data?.Block?.Icon == null)
                    continue;

                var icon = new VisualElement { pickingMode = PickingMode.Ignore };
                icon.AddToClassList("le-stamp-icon");
                icon.style.backgroundImage = Background.FromSprite(data.Block.Icon);
                icon.style.display = DisplayStyle.None;

                host.Add(icon);
                _stampIcons.Add(new StampIcon(offset, icon));
            }
        }

        private void UpdateStamp(Vector2 panelPosition)
        {
            var host = GhostHost ?? panel?.visualTree;
            if (host == null)
                return;

            if (!TryGetCellAt(panelPosition, out var hovered))
            {
                SetHoverCells(Array.Empty<Vector2Int>(), true);
                foreach (var stamp in _stampIcons)
                    stamp.Element.style.display = DisplayStyle.None;
                return;
            }

            bool valid = IsDropValid(hovered);

            var dests = new List<Vector2Int>(_dragOffsets.Count);
            foreach (var offset in _dragOffsets)
                dests.Add(hovered + offset);
            SetHoverCells(dests, valid);

            foreach (var stamp in _stampIcons)
            {
                var dst = hovered + stamp.Offset;
                if (!_level.InBounds(dst))
                {
                    stamp.Element.style.display = DisplayStyle.None;
                    continue;
                }

                var wb = _cells[dst.x, dst.y].worldBound;
                var local = host.WorldToLocal(new Vector2(wb.x, wb.y));

                stamp.Element.style.display = DisplayStyle.Flex;
                stamp.Element.style.left = local.x;
                stamp.Element.style.top = local.y;
                stamp.Element.style.width = wb.width;
                stamp.Element.style.height = wb.height;
            }
        }

        private void ClearStamp()
        {
            foreach (var stamp in _stampIcons)
                stamp.Element.RemoveFromHierarchy();

            _stampIcons.Clear();
            _dragOffsets.Clear();
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
