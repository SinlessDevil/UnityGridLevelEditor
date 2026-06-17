using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Renders a <see cref="LevelMatrixEditor"/> grid as a matrix of clickable cells.
    /// Multi-cell blocks (cells sharing an InstanceId) are drawn as one merged plate
    /// overlay and behave as a single object for dragging and clearing.
    /// </summary>
    public class LevelGridView : VisualElement
    {
        private const float DragThreshold = 4f;

        private LevelMatrixEditor _level;
        private VisualElement[,] _cells;

        private VisualElement _plateLayer;
        private readonly List<PlateInfo> _plates = new();

        private readonly List<Vector2Int> _selection = new();
        private Vector2Int? _selectionStart;
        private readonly List<Vector2Int> _hoverCells = new();

        // Drag-to-move state.
        private Vector2Int? _moveSource;
        private Vector2 _pointerDownPos;
        private bool _moving;
        private int _movePointerId = -1;
        private readonly List<Vector2Int> _dragOffsets = new();
        private readonly List<StampIcon> _stampIcons = new();   // per-cell preview (single / multi-selection)
        private VisualElement _stampPlate;                      // one merged preview for a whole object
        private bool _stampMerged;
        private BlockDataEditor _externalBlock;

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

        private readonly struct PlateInfo
        {
            public readonly List<Vector2Int> Cells;
            public readonly VisualElement Element;

            public PlateInfo(List<Vector2Int> cells, VisualElement element)
            {
                Cells = cells;
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
        /// </summary>
        public VisualElement GhostHost { get; set; }

        /// <summary>Optional sink for user-facing messages (set by the window).</summary>
        public Action<string, LogLevel> Log { get; set; }

        public LevelGridView()
        {
            AddToClassList("le-grid");
            focusable = true; // so the grid can receive Ctrl+C / Ctrl+V

            // Drag is driven through pointer capture on the grid itself so it keeps
            // working while cells/plates underneath are rebuilt.
            RegisterCallback<PointerMoveEvent>(OnDragMove);
            RegisterCallback<PointerUpEvent>(OnDragUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => CancelMove());
            RegisterCallback<GeometryChangedEvent>(_ => PositionPlates());
            RegisterCallback<KeyDownEvent>(OnKeyDown);
        }

        public void SetLevel(LevelMatrixEditor level)
        {
            _level = level;
            _selection.Clear();
            _selectionStart = null;
            Rebuild();
        }

        // ---- Build / refresh ----

        public void Rebuild()
        {
            ResetDragState();

            Clear();
            _cells = null;
            _plateLayer = null;
            _plates.Clear();
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

            _plateLayer = new VisualElement { name = "plates", pickingMode = PickingMode.Ignore };
            _plateLayer.AddToClassList("le-plate-layer");
            Add(_plateLayer);

            RebuildPlates();
        }

        /// <summary>Re-reads cell data into the visuals and rebuilds the merged plates.</summary>
        public void RefreshCells()
        {
            if (_cells == null || _level == null)
                return;

            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                UpdateCell(x, y);

            RebuildPlates();
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
            cell.RegisterCallback<PointerDownEvent>(evt => OnCellPointerDown(evt, pos));

            return cell;
        }

        private void UpdateCell(int x, int y)
        {
            var cell = _cells[x, y];
            var data = _level.GetCell(x, y);

            var icon = cell.Q<VisualElement>("icon");
            var arrow = cell.Q<Label>("arrow");
            var id = cell.Q<Label>("id");

            bool hasBlock = data?.Block != null;
            bool isInstance = hasBlock && data.InstanceId != 0;
            // Per-cell icon is only drawn for standalone blocks; instances draw a plate.
            bool showIcon = hasBlock && data.Block.Icon != null && !isInstance;

            if (showIcon)
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
            cell.EnableInClassList("le-cell--instance", isInstance);
            cell.EnableInClassList("le-cell--selected", _selection.Contains(new Vector2Int(x, y)));
        }

        // ---- Merged plates (multi-cell blocks) ----

        private void RebuildPlates()
        {
            if (_plateLayer == null)
                return;

            _plateLayer.Clear();
            _plates.Clear();

            var groups = new Dictionary<int, List<Vector2Int>>();
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
            {
                var c = _level.GetCell(x, y);
                if (c.InstanceId == 0 || c.Block == null)
                    continue;

                if (!groups.TryGetValue(c.InstanceId, out var list))
                    groups[c.InstanceId] = list = new List<Vector2Int>();
                list.Add(new Vector2Int(x, y));
            }

            foreach (var pair in groups)
            {
                var cells = pair.Value;
                var block = _level.GetCell(cells[0]).Block;

                var plate = BuildPlate(cells, block);
                _plateLayer.Add(plate);
                _plates.Add(new PlateInfo(cells, plate));
            }

            PositionPlates();
        }

        private VisualElement BuildPlate(List<Vector2Int> cells, BlockDataEditor block)
        {
            var plate = new VisualElement { tooltip = block.ID };
            plate.AddToClassList("le-plate");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("le-plate__icon");
            if (block.Icon != null)
                icon.style.backgroundImage = Background.FromSprite(block.Icon);
            plate.Add(icon);

            var arrow = new Label("↑") { pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("le-plate__arrow");
            float angle = _level.GetCell(cells[0]).Rotation.eulerAngles.y;
            arrow.style.translate = new Translate(Length.Percent(-50), 0);
            arrow.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            plate.Add(arrow);

            var label = new Label(block.ID) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("le-plate__id");
            plate.Add(label);

            plate.RegisterCallback<PointerDownEvent>(evt => OnPlatePointerDown(evt, cells));

            return plate;
        }

        private void PositionPlates()
        {
            if (_plateLayer == null || _cells == null || _level == null)
                return;

            foreach (var info in _plates)
            {
                float minX = float.MaxValue, minY = float.MaxValue;
                float maxX = float.MinValue, maxY = float.MinValue;

                foreach (var c in info.Cells)
                {
                    var wb = _cells[c.x, c.y].worldBound;
                    minX = Mathf.Min(minX, wb.xMin);
                    minY = Mathf.Min(minY, wb.yMin);
                    maxX = Mathf.Max(maxX, wb.xMax);
                    maxY = Mathf.Max(maxY, wb.yMax);
                }

                var topLeft = _plateLayer.WorldToLocal(new Vector2(minX, minY));
                info.Element.style.left = topLeft.x;
                info.Element.style.top = topLeft.y;
                info.Element.style.width = maxX - minX;
                info.Element.style.height = maxY - minY;
            }
        }

        // ---- Hit testing / highlight ----

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

        public void SetHoverCell(Vector2Int? cell)
        {
            if (cell.HasValue)
                SetHoverCells(new[] { cell.Value }, true);
            else
                SetHoverCells(Array.Empty<Vector2Int>(), true);
        }

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

        // ---- Pointer: cells ----

        private void OnCellPointerDown(PointerDownEvent evt, Vector2Int pos)
        {
            Focus(); // route keyboard shortcuts here
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

            BeginPointerDrag(pos, evt.pointerId, evt.position);
            evt.StopPropagation();
        }

        // ---- Pointer: plates (whole instance) ----

        private void OnPlatePointerDown(PointerDownEvent evt, List<Vector2Int> cells)
        {
            Focus(); // route keyboard shortcuts here

            if (evt.button == 1)
            {
                _selection.Clear();
                _selection.AddRange(cells);
                _selectionStart = null;
                RefreshCells();

                CellRightClicked?.Invoke(cells[0], evt.position);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            var anchor = TryGetCellAt(evt.position, out var hit) ? hit : cells[0];
            BeginPointerDrag(anchor, evt.pointerId, evt.position);
            evt.StopPropagation();
        }

        // ---- Drag (shared) ----

        private void BeginPointerDrag(Vector2Int anchor, int pointerId, Vector2 pos)
        {
            _moveSource = anchor;
            _pointerDownPos = pos;
            _moving = false;
            _movePointerId = pointerId;
            this.CapturePointer(pointerId);
        }

        private void OnDragMove(PointerMoveEvent evt)
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

        /// <summary>Decides which cells move and builds the preview stamp.</summary>
        private bool BeginDrag()
        {
            var anchor = _moveSource.Value;
            var anchorCell = _level.GetCell(anchor);

            // A whole multi-cell object drags as one merged plate.
            if (anchorCell.InstanceId != 0)
            {
                _moving = true;
                BuildStamp(anchor, GetInstanceCells(anchorCell.InstanceId), merged: true, mergedIcon: anchorCell.Block?.Icon);
                return true;
            }

            List<Vector2Int> group;
            if (_selection.Count > 1 && _selection.Contains(anchor))
                group = new List<Vector2Int>(_selection);
            else
            {
                if (anchorCell.Block == null)
                    return false;

                group = new List<Vector2Int> { anchor };
            }

            _moving = true;
            BuildStamp(anchor, group, merged: false, mergedIcon: null);
            return true;
        }

        private void OnDragUp(PointerUpEvent evt)
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
                var anchorCell = _level.GetCell(_moveSource.Value);
                _selection.Clear();
                if (anchorCell.InstanceId != 0)
                    _selection.AddRange(GetInstanceCells(anchorCell.InstanceId));
                _selectionStart = null;
                RefreshCells();
            }

            int pointerId = _movePointerId;
            EndMove();
            if (this.HasPointerCapture(pointerId))
                this.ReleasePointer(pointerId);

            evt.StopPropagation();
        }

        /// <summary>Every dragged cell must land inside the grid.</summary>
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

            var sources = new List<Vector2Int>(_dragOffsets.Count);
            var blocks = new List<BlockDataEditor>(_dragOffsets.Count);
            var rotations = new List<Quaternion>(_dragOffsets.Count);
            var instanceIds = new List<int>(_dragOffsets.Count);

            foreach (var offset in _dragOffsets)
            {
                var src = anchor + offset;
                var c = _level.GetCell(src);
                sources.Add(src);
                blocks.Add(c.Block);
                rotations.Add(c.Rotation);
                instanceIds.Add(c.InstanceId);
            }

            var movingInstances = new HashSet<int>();
            foreach (var id in instanceIds)
                if (id != 0)
                    movingInstances.Add(id);

            var dests = new List<Vector2Int>(sources.Count);
            foreach (var src in sources)
                dests.Add(src + delta);

            // Anything occupying the destination (other than what we are moving) is cleared.
            var clearInstances = new HashSet<int>();
            foreach (var d in dests)
            {
                var c = _level.GetCell(d);
                if (c.InstanceId != 0 && !movingInstances.Contains(c.InstanceId))
                    clearInstances.Add(c.InstanceId);
            }

            foreach (var id in clearInstances)
                ClearInstance(id);

            var sourceSet = new HashSet<Vector2Int>(sources);

            foreach (var src in sources)
                ClearCell(src);

            foreach (var d in dests)
                if (!sourceSet.Contains(d))
                    ClearCell(d);

            for (int i = 0; i < sources.Count; i++)
            {
                var c = _level.GetCell(dests[i]);
                c.Block = blocks[i];
                c.Rotation = rotations[i];
                c.InstanceId = instanceIds[i];
            }

            EditorUtility.SetDirty(_level);

            _selection.Clear();
            _selection.AddRange(dests);
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

        private void ResetDragState()
        {
            ClearStamp();
            _externalBlock = null;
            _moveSource = null;
            _moving = false;
            _movePointerId = -1;
        }

        // ---- Instance helpers ----

        private List<Vector2Int> GetInstanceCells(int id)
        {
            var list = new List<Vector2Int>();
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                if (_level.GetCell(x, y).InstanceId == id)
                    list.Add(new Vector2Int(x, y));
            return list;
        }

        private void ClearInstance(int id)
        {
            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                if (_level.GetCell(x, y).InstanceId == id)
                    ClearCell(new Vector2Int(x, y));
        }

        private void ClearCell(Vector2Int pos)
        {
            var c = _level.GetCell(pos);
            c.Block = null;
            c.Rotation = Quaternion.identity;
            c.InstanceId = 0;
        }

        private void ClearArea(IEnumerable<Vector2Int> cells)
        {
            var instanceIds = new HashSet<int>();
            var standalone = new List<Vector2Int>();

            foreach (var p in cells)
            {
                var c = _level.GetCell(p);
                if (c.InstanceId != 0)
                    instanceIds.Add(c.InstanceId);
                else if (c.Block != null)
                    standalone.Add(p);
            }

            foreach (var id in instanceIds)
                ClearInstance(id);

            foreach (var p in standalone)
                ClearCell(p);
        }

        // ---- Preview stamp ----

        /// <param name="merged">When true the whole group is previewed as one plate (a single object).</param>
        private void BuildStamp(Vector2Int anchor, List<Vector2Int> group, bool merged, Sprite mergedIcon)
        {
            ClearStamp();
            _stampMerged = merged;

            foreach (var cellPos in group)
                _dragOffsets.Add(cellPos - anchor);

            var host = GhostHost ?? panel?.visualTree;
            if (host == null)
                return;

            if (merged)
            {
                if (mergedIcon != null)
                    _stampPlate = CreateStampPlate(host, mergedIcon);
                return;
            }

            foreach (var cellPos in group)
            {
                var data = _level.GetCell(cellPos);
                if (data?.Block?.Icon == null)
                    continue;

                _stampIcons.Add(new StampIcon(cellPos - anchor, CreateStampIcon(host, data.Block.Icon)));
            }
        }

        private VisualElement CreateStampIcon(VisualElement host, Sprite sprite)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("le-stamp-icon");
            icon.style.backgroundImage = Background.FromSprite(sprite);
            icon.style.display = DisplayStyle.None;
            host.Add(icon);
            return icon;
        }

        private VisualElement CreateStampPlate(VisualElement host, Sprite sprite)
        {
            var plate = new VisualElement { pickingMode = PickingMode.Ignore };
            plate.AddToClassList("le-stamp-plate");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("le-stamp-plate__icon");
            icon.style.backgroundImage = Background.FromSprite(sprite);
            plate.Add(icon);

            plate.style.display = DisplayStyle.None;
            host.Add(plate);
            return plate;
        }

        private void UpdateStamp(Vector2 panelPosition)
        {
            var host = GhostHost ?? panel?.visualTree;
            if (host == null)
                return;

            if (!TryGetCellAt(panelPosition, out var hovered))
            {
                SetHoverCells(Array.Empty<Vector2Int>(), true);
                HideStampVisuals();
                return;
            }

            bool valid = IsDropValid(hovered);

            var dests = new List<Vector2Int>(_dragOffsets.Count);
            foreach (var offset in _dragOffsets)
                dests.Add(hovered + offset);
            SetHoverCells(dests, valid);

            if (_stampMerged)
                PositionStampPlate(host, hovered);
            else
                PositionStampIcons(host, hovered);
        }

        private void PositionStampIcons(VisualElement host, Vector2Int hovered)
        {
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

        private void PositionStampPlate(VisualElement host, Vector2Int hovered)
        {
            if (_stampPlate == null)
                return;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;
            bool any = false;

            foreach (var offset in _dragOffsets)
            {
                var dst = hovered + offset;
                if (!_level.InBounds(dst))
                    continue;

                any = true;
                var wb = _cells[dst.x, dst.y].worldBound;
                minX = Mathf.Min(minX, wb.xMin);
                minY = Mathf.Min(minY, wb.yMin);
                maxX = Mathf.Max(maxX, wb.xMax);
                maxY = Mathf.Max(maxY, wb.yMax);
            }

            if (!any)
            {
                _stampPlate.style.display = DisplayStyle.None;
                return;
            }

            var topLeft = host.WorldToLocal(new Vector2(minX, minY));
            _stampPlate.style.display = DisplayStyle.Flex;
            _stampPlate.style.left = topLeft.x;
            _stampPlate.style.top = topLeft.y;
            _stampPlate.style.width = maxX - minX;
            _stampPlate.style.height = maxY - minY;
        }

        private void HideStampVisuals()
        {
            if (_stampPlate != null)
                _stampPlate.style.display = DisplayStyle.None;

            foreach (var stamp in _stampIcons)
                stamp.Element.style.display = DisplayStyle.None;
        }

        private void ClearStamp()
        {
            foreach (var stamp in _stampIcons)
                stamp.Element.RemoveFromHierarchy();
            _stampIcons.Clear();

            if (_stampPlate != null)
            {
                _stampPlate.RemoveFromHierarchy();
                _stampPlate = null;
            }

            _dragOffsets.Clear();
            _stampMerged = false;
        }

        // ---- External (palette) footprint placement ----

        public void BeginExternalStamp(BlockDataEditor block)
        {
            ClearStamp();
            _externalBlock = block;

            if (block == null)
                return;

            foreach (var offset in block.Footprint)
                _dragOffsets.Add(offset);

            var host = GhostHost ?? panel?.visualTree;
            if (host == null || block.Icon == null)
                return;

            if (block.IsMultiCell)
            {
                _stampMerged = true;
                _stampPlate = CreateStampPlate(host, block.Icon);
            }
            else
            {
                _stampIcons.Add(new StampIcon(Vector2Int.zero, CreateStampIcon(host, block.Icon)));
            }
        }

        public void UpdateExternalStamp(Vector2 panelPosition) => UpdateStamp(panelPosition);

        /// <summary>Fills the footprint cells if the drop is in bounds, clearing anything underneath.</summary>
        public bool TryPlaceExternalStamp(Vector2 panelPosition)
        {
            if (_externalBlock == null || _level == null)
                return false;

            if (!TryGetCellAt(panelPosition, out var hovered))
                return false; // dropped outside the grid

            if (!IsDropValid(hovered))
            {
                Log?.Invoke($"Can't place '{_externalBlock.ID}': out of bounds", LogLevel.Error);
                return false;
            }

            var targets = new List<Vector2Int>(_dragOffsets.Count);
            foreach (var offset in _dragOffsets)
                targets.Add(hovered + offset);

            ClearArea(targets);

            int instanceId = _externalBlock.IsMultiCell ? _level.NewInstanceId() : 0;
            foreach (var t in targets)
            {
                var c = _level.GetCell(t);
                c.Block = _externalBlock;
                c.Rotation = Quaternion.identity;
                c.InstanceId = instanceId;
            }

            EditorUtility.SetDirty(_level);
            RefreshCells();
            Log?.Invoke($"Placed '{_externalBlock.ID}'", LogLevel.Success);
            return true;
        }

        public void ClearExternalStamp()
        {
            _externalBlock = null;
            SetHoverCells(Array.Empty<Vector2Int>(), true);
            ClearStamp();
        }

        // ---- Selection ----

        // ---- Keyboard copy / paste ----

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (_level == null)
                return;

            if (evt.keyCode == KeyCode.Backspace || evt.keyCode == KeyCode.Delete)
            {
                DeleteSelection();
                evt.StopPropagation();
                return;
            }

            bool ctrl = evt.ctrlKey || evt.commandKey;
            if (!ctrl)
                return;

            if (evt.keyCode == KeyCode.C)
            {
                CopySelection();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.V)
            {
                PasteSelection();
                evt.StopPropagation();
            }
        }

        private void DeleteSelection()
        {
            if (_selection.Count == 0)
            {
                Log?.Invoke("Nothing selected to delete", LogLevel.Info);
                return;
            }

            int count = _selection.Count;

            // Clears standalone selected cells and any whole object they touch.
            ClearArea(_selection);

            EditorUtility.SetDirty(_level);
            _selection.Clear();
            _selectionStart = null;
            RefreshCells();

            Log?.Invoke($"Deleted {count} selected cell(s)", LogLevel.Info);
        }

        private void CopySelection()
        {
            if (_selection.Count == 0)
            {
                Log?.Invoke("Nothing selected to copy", LogLevel.Info);
                return;
            }

            LevelClipboard.Copy(_level, _selection);
            Log?.Invoke($"Copied {LevelClipboard.Count} cell(s)", LogLevel.Success);
        }

        private void PasteSelection()
        {
            if (!LevelClipboard.HasData)
            {
                Log?.Invoke("Clipboard is empty", LogLevel.Info);
                return;
            }

            if (_selection.Count == 0)
            {
                Log?.Invoke("Select a target cell to paste", LogLevel.Error);
                return;
            }

            var (pasted, blocked) = LevelClipboard.Paste(_level, _selection[0]);

            if (pasted > 0)
            {
                EditorUtility.SetDirty(_level);
                RefreshCells();
            }

            if (blocked > 0 && pasted == 0)
                Log?.Invoke("Can't paste into a solid object", LogLevel.Error);
            else if (blocked > 0)
                Log?.Invoke($"Pasted {pasted}, {blocked} blocked by solid objects", LogLevel.Info);
            else
                Log?.Invoke($"Pasted {pasted} cell(s)", LogLevel.Success);
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
    }
}
