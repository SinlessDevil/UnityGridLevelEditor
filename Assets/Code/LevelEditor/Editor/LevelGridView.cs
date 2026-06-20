using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Renders a <see cref="LevelMatrixEditor"/> grid as a matrix of clickable cells and
    /// composes the grid's behaviours (zoom, selection, hit-testing, highlighting,
    /// multi-cell plates, drag-to-move, palette placement and keyboard shortcuts). It owns
    /// the live cell array and routes pointer / keyboard input to the focused components.
    /// </summary>
    public class LevelGridView : VisualElement
    {
        private readonly GridContext _ctx = new();
        private readonly GridSelection _selection = new();
        private readonly GridZoom _zoom;
        private readonly GridHitTester _hitTester;
        private readonly GridHighlighter _highlighter;
        private readonly CellRenderer _cellRenderer;
        private readonly PlateLayer _plateLayer;
        private readonly DragPreview _preview;
        private readonly GridMoveController _move;
        private readonly PalettePlacement _palette;
        private readonly GridShortcuts _shortcuts;

        private LevelMatrixEditor _level;
        private VisualElement[,] _cells;

        /// <summary>
        /// Raised on right click. Arguments are the clicked cell position and the
        /// mouse position in panel coordinates (for placing the popup).
        /// </summary>
        public event Action<Vector2Int, Vector2> CellRightClicked;

        /// <summary>Raised after a committed grid mutation, so the window can push an undo step.</summary>
        public event Action Changed;

        /// <summary>Raised on Ctrl+Z / Ctrl+Y (Ctrl+Shift+Z); handled by the window's history.</summary>
        public event Action UndoRequested;
        public event Action RedoRequested;

        /// <summary>Raised after the zoom level changes, so the window can update its label.</summary>
        public event Action ZoomChanged;

        /// <summary>Current zoom factor (1 = 100%).</summary>
        public float Zoom => _zoom.Zoom;

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

            _zoom = new GridZoom(_ctx);
            _hitTester = new GridHitTester(_ctx);
            _highlighter = new GridHighlighter(_ctx, this);
            _cellRenderer = new CellRenderer(_ctx, _zoom, _selection, OnCellPointerDown);
            _plateLayer = new PlateLayer(_ctx, _zoom, _selection, OnPlatePointerDown);
            _preview = new DragPreview(_ctx, _hitTester, _highlighter, () => GhostHost ?? panel?.visualTree);
            _move = new GridMoveController(_ctx, _selection, _hitTester, _highlighter, _preview, this,
                RefreshCells, () => Changed?.Invoke());
            _palette = new PalettePlacement(_ctx, _hitTester, _highlighter, _preview,
                RefreshCells, () => Changed?.Invoke(), (m, l) => Log?.Invoke(m, l));
            _shortcuts = new GridShortcuts(_ctx, _selection,
                RefreshCells, () => Changed?.Invoke(), () => UndoRequested?.Invoke(), () => RedoRequested?.Invoke(),
                (m, l) => Log?.Invoke(m, l), FlashCells);

            _zoom.ZoomChanged += () => ZoomChanged?.Invoke();

            // Drag is driven through pointer capture on the grid itself so it keeps
            // working while cells/plates underneath are rebuilt.
            RegisterCallback<PointerMoveEvent>(_move.OnPointerMove);
            RegisterCallback<PointerUpEvent>(_move.OnPointerUp);
            RegisterCallback<PointerCaptureOutEvent>(_ => _move.Cancel());
            RegisterCallback<GeometryChangedEvent>(_ => _plateLayer.PositionAll());
            RegisterCallback<KeyDownEvent>(_shortcuts.OnKeyDown);
            RegisterCallback<WheelEvent>(_zoom.OnWheel);
        }

        // ---- Zoom (delegated to GridZoom) ----

        public void ZoomIn() => _zoom.ZoomIn();
        public void ZoomOut() => _zoom.ZoomOut();
        public void ResetZoom() => _zoom.ResetZoom();

        public void SetLevel(LevelMatrixEditor level)
        {
            _level = level;
            _ctx.Level = level;
            _selection.Clear();
            _selection.Start = null;
            Rebuild();
        }

        // ---- Build / refresh ----

        public void Rebuild()
        {
            ResetInteractions();

            Clear();
            _cells = null;
            _ctx.Cells = null;
            _plateLayer.Reset();
            _highlighter.ResetState();

            if (_level == null)
                return;

            _level.EnsureInitialized();
            _cells = new VisualElement[_level.Width, _level.Height];
            _ctx.Cells = _cells;

            for (int y = 0; y < _level.Height; y++)
            {
                var row = new VisualElement();
                row.AddToClassList("le-grid__row");

                for (int x = 0; x < _level.Width; x++)
                {
                    var cell = _cellRenderer.Build(x, y);
                    _cells[x, y] = cell;
                    row.Add(cell);
                    _cellRenderer.Update(x, y);
                }

                Add(row);
            }

            Add(_plateLayer.CreateLayer());
            _plateLayer.Rebuild();
        }

        /// <summary>Re-reads cell data into the visuals and rebuilds the merged plates.</summary>
        public void RefreshCells()
        {
            if (_cells == null || _level == null)
                return;

            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                _cellRenderer.Update(x, y);

            _plateLayer.Rebuild();
        }

        /// <summary>Cancels any in-progress drag / palette placement (used on rebuild).</summary>
        private void ResetInteractions()
        {
            _preview.Clear();
            _palette.ResetExternal();
            _move.Reset();
        }

        // ---- Hit testing / highlight (delegated) ----

        public bool TryGetCellAt(Vector2 panelPosition, out Vector2Int cellPos) =>
            _hitTester.TryGetCellAt(panelPosition, out cellPos);

        public void FlashCells(IEnumerable<Vector2Int> cells)
        {
            // Materialize once: highlighter flashes standalone/empty cells, the plate layer
            // flashes any multi-cell object covering them (the per-cell flash hides under it).
            var list = cells as IReadOnlyList<Vector2Int> ?? new List<Vector2Int>(cells);
            _highlighter.FlashCells(list);
            _plateLayer.Flash(list);
        }

        // ---- Pointer input routing ----

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
                if (!_selection.Start.HasValue)
                {
                    _selection.Start = pos;
                    _selection.Clear();
                    _selection.Add(pos);
                }
                else
                {
                    _selection.SelectRange(_selection.Start.Value, pos);
                    _selection.Start = null;
                }

                RefreshCells();
                evt.StopPropagation();
                return;
            }

            _move.BeginDrag(pos, evt.pointerId, evt.position);
            evt.StopPropagation();
        }

        private void OnPlatePointerDown(PointerDownEvent evt, List<Vector2Int> cells)
        {
            Focus(); // route keyboard shortcuts here

            if (evt.button == 1)
            {
                _selection.Clear();
                _selection.AddRange(cells);
                _selection.Start = null;
                RefreshCells();

                CellRightClicked?.Invoke(cells[0], evt.position);
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0)
                return;

            // Ctrl+LMB selects the whole object (consistent with cell selection).
            if (evt.ctrlKey || evt.commandKey)
            {
                _selection.Clear();
                _selection.AddRange(cells);
                _selection.Start = null;
                RefreshCells();
                evt.StopPropagation();
                return;
            }

            var anchor = _hitTester.TryGetCellAt(evt.position, out var hit) ? hit : cells[0];
            _move.BeginDrag(anchor, evt.pointerId, evt.position);
            evt.StopPropagation();
        }

        // ---- External (palette) placement (delegated) ----

        public void BeginExternalStamp(BlockDataEditor block) => _palette.Begin(block);
        public void UpdateExternalStamp(Vector2 panelPosition) => _palette.Update(panelPosition);
        public bool TryPlaceExternalStamp(Vector2 panelPosition) => _palette.TryPlace(panelPosition);
        public void ClearExternalStamp() => _palette.Clear();
    }
}
