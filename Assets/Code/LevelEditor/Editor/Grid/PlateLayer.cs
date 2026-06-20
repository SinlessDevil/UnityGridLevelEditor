using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Owns the overlay layer that draws every placed multi-cell instance as one merged
    /// <see cref="ObjectPlate"/>. Cells sharing an InstanceId are grouped into a single
    /// plate that follows their exact footprint and behaves as one object on click.
    /// </summary>
    internal sealed class PlateLayer
    {
        // Cells are drawn with a 1px margin; expand each rect by this so a plate's
        // cells merge with no internal gaps.
        private const float CellGapPad = 1f;

        private readonly GridContext _ctx;
        private readonly GridZoom _zoom;
        private readonly GridSelection _selection;
        private readonly Action<PointerDownEvent, List<Vector2Int>> _onPlatePointerDown;

        private readonly List<PlateInfo> _plates = new();
        private VisualElement _layer;

        private readonly struct PlateInfo
        {
            public readonly List<Vector2Int> Cells;
            public readonly ObjectPlate Element;

            public PlateInfo(List<Vector2Int> cells, ObjectPlate element)
            {
                Cells = cells;
                Element = element;
            }
        }

        public PlateLayer(
            GridContext ctx,
            GridZoom zoom,
            GridSelection selection,
            Action<PointerDownEvent, List<Vector2Int>> onPlatePointerDown)
        {
            _ctx = ctx;
            _zoom = zoom;
            _selection = selection;
            _onPlatePointerDown = onPlatePointerDown;
        }

        /// <summary>Drops the current layer reference and plates (called at the start of a rebuild).</summary>
        public void Reset()
        {
            _layer = null;
            _plates.Clear();
        }

        /// <summary>Creates a fresh layer element; the caller adds it to the grid hierarchy.</summary>
        public VisualElement CreateLayer()
        {
            _layer = new VisualElement { name = "plates", pickingMode = PickingMode.Ignore };
            _layer.AddToClassList("le-plate-layer");
            return _layer;
        }

        public void Rebuild()
        {
            if (_layer == null)
                return;

            _layer.Clear();
            _plates.Clear();

            var level = _ctx.Level;

            var groups = new Dictionary<int, List<Vector2Int>>();
            for (int y = 0; y < _ctx.Height; y++)
            for (int x = 0; x < _ctx.Width; x++)
            {
                var c = level.GetCell(x, y);
                if (c.InstanceId == 0 || c.Block == null)
                    continue;

                if (!groups.TryGetValue(c.InstanceId, out var list))
                    groups[c.InstanceId] = list = new List<Vector2Int>();
                list.Add(new Vector2Int(x, y));
            }

            foreach (var pair in groups)
            {
                var cells = pair.Value;
                var block = level.GetCell(cells[0]).Block;

                var plate = BuildPlate(cells, block);

                bool selected = false;
                foreach (var c in cells)
                    if (_selection.Contains(c))
                    {
                        selected = true;
                        break;
                    }
                plate.SetSelected(selected);

                _layer.Add(plate);
                _plates.Add(new PlateInfo(cells, plate));
            }

            PositionAll();
        }

        private ObjectPlate BuildPlate(List<Vector2Int> cells, BlockDataEditor block)
        {
            var plate = new ObjectPlate { tooltip = block.ID };
            plate.SetZoom(_zoom.Zoom);
            float angle = _ctx.Level.GetCell(cells[0]).Rotation.eulerAngles.y;
            plate.SetContent(block.Icon, block.ID, angle);
            plate.RegisterCallback<PointerDownEvent>(evt => _onPlatePointerDown(evt, cells));

            return plate;
        }

        /// <summary>
        /// Briefly blinks every plate that covers one of the given cells red. Cells under a
        /// plate can't show the per-cell flash (the plate paints over them), so the blocking
        /// object itself flashes instead.
        /// </summary>
        public void Flash(IEnumerable<Vector2Int> cells)
        {
            if (_layer == null)
                return;

            var set = new HashSet<Vector2Int>(cells);
            if (set.Count == 0)
                return;

            var targets = new List<ObjectPlate>();
            foreach (var info in _plates)
                foreach (var c in info.Cells)
                    if (set.Contains(c))
                    {
                        targets.Add(info.Element);
                        break;
                    }

            if (targets.Count == 0)
                return;

            const int toggles = 6;
            int count = 0;

            IVisualElementScheduledItem item = null;
            item = _layer.schedule.Execute(() =>
            {
                bool on = count % 2 == 0;
                foreach (var t in targets)
                    t.SetFlash(on);

                if (++count >= toggles)
                {
                    foreach (var t in targets)
                        t.SetFlash(false);
                    item?.Pause();
                }
            }).Every(110);
        }

        public void PositionAll()
        {
            if (_layer == null || _ctx.Cells == null || _ctx.Level == null)
                return;

            foreach (var info in _plates)
            {
                info.Element.SetZoom(_zoom.Zoom);
                PositionPlate(info.Element, info.Cells);
            }
        }

        /// <summary>Places a plate over its cells and feeds the exact footprint to it.</summary>
        private void PositionPlate(ObjectPlate plate, List<Vector2Int> cells)
        {
            var grid = _ctx.Cells;

            float minX = float.MaxValue, minY = float.MaxValue;
            float maxX = float.MinValue, maxY = float.MinValue;

            foreach (var c in cells)
            {
                var wb = grid[c.x, c.y].worldBound;
                minX = Mathf.Min(minX, wb.xMin);
                minY = Mathf.Min(minY, wb.yMin);
                maxX = Mathf.Max(maxX, wb.xMax);
                maxY = Mathf.Max(maxY, wb.yMax);
            }

            var topLeft = _layer.WorldToLocal(new Vector2(minX, minY));
            plate.style.left = topLeft.x;
            plate.style.top = topLeft.y;
            plate.style.width = maxX - minX;
            plate.style.height = maxY - minY;

            // Per-cell rects in the plate's local space (origin = bbox top-left).
            var rects = new Rect[cells.Count];
            var coords = new Vector2Int[cells.Count];
            for (int i = 0; i < cells.Count; i++)
            {
                var wb = grid[cells[i].x, cells[i].y].worldBound;
                rects[i] = new Rect(
                    wb.xMin - minX - CellGapPad,
                    wb.yMin - minY - CellGapPad,
                    wb.width + 2 * CellGapPad,
                    wb.height + 2 * CellGapPad);
                coords[i] = cells[i];
            }

            plate.SetShape(rects, coords);
        }
    }
}
