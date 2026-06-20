using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// The floating drag "stamp": a snapped preview of where cells will land. Tracks the
    /// dragged footprint offsets, draws per-cell ghost icons (single / multi-selection) or
    /// one merged ghost plate (a whole object), and highlights the target cells. Shared by
    /// the move drag and the palette placement so the preview rules live in one place.
    /// </summary>
    internal sealed class DragPreview
    {
        // Cells are drawn with a 1px margin; expand each rect by this so a stamp's
        // cells merge with no internal gaps (matches PlateLayer.CellGapPad).
        private const float CellGapPad = 1f;

        private readonly GridContext _ctx;
        private readonly GridHitTester _hitTester;
        private readonly GridHighlighter _highlighter;
        private readonly Func<VisualElement> _hostProvider;

        private readonly List<Vector2Int> _dragOffsets = new();
        private readonly List<StampIcon> _stampIcons = new();   // per-cell preview (single / multi-selection)
        private ObjectPlate _stampPlate;                        // one merged preview for a whole object
        private bool _stampMerged;

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

        public DragPreview(
            GridContext ctx,
            GridHitTester hitTester,
            GridHighlighter highlighter,
            Func<VisualElement> hostProvider)
        {
            _ctx = ctx;
            _hitTester = hitTester;
            _highlighter = highlighter;
            _hostProvider = hostProvider;
        }

        /// <summary>The footprint offsets (relative to the drag anchor) currently being carried.</summary>
        public IReadOnlyList<Vector2Int> Offsets => _dragOffsets;

        /// <summary>Every dragged cell must land inside the grid.</summary>
        public bool IsDropValid(Vector2Int hovered)
        {
            foreach (var offset in _dragOffsets)
                if (!_ctx.Level.InBounds(hovered + offset))
                    return false;

            return true;
        }

        /// <summary>Builds the preview for a move of existing cells.</summary>
        /// <param name="merged">When true the whole group is previewed as one plate (a single object).</param>
        public void BuildMoveStamp(Vector2Int anchor, List<Vector2Int> group, bool merged)
        {
            Clear();
            _stampMerged = merged;

            foreach (var cellPos in group)
                _dragOffsets.Add(cellPos - anchor);

            var host = _hostProvider();
            if (host == null)
                return;

            if (merged)
            {
                _stampPlate = CreateStampPlate(host);
                return;
            }

            foreach (var cellPos in group)
            {
                var data = _ctx.Level.GetCell(cellPos);
                if (data?.Block?.Icon == null)
                    continue;

                _stampIcons.Add(new StampIcon(cellPos - anchor, CreateStampIcon(host, data.Block.Icon)));
            }
        }

        /// <summary>Builds the preview for placing a block's footprint from the palette.</summary>
        public void BuildFootprintStamp(BlockDataEditor block)
        {
            Clear();

            if (block == null)
                return;

            foreach (var offset in block.Footprint)
                _dragOffsets.Add(offset);

            var host = _hostProvider();
            if (host == null)
                return;

            if (block.IsMultiCell)
            {
                _stampMerged = true;
                _stampPlate = CreateStampPlate(host);
            }
            else if (block.Icon != null)
            {
                _stampIcons.Add(new StampIcon(Vector2Int.zero, CreateStampIcon(host, block.Icon)));
            }
        }

        public void Update(Vector2 panelPosition)
        {
            var host = _hostProvider();
            if (host == null)
                return;

            if (!_hitTester.TryGetCellAt(panelPosition, out var hovered))
            {
                _highlighter.SetHoverCells(Array.Empty<Vector2Int>(), true);
                HideStampVisuals();
                return;
            }

            bool valid = IsDropValid(hovered);

            var dests = new List<Vector2Int>(_dragOffsets.Count);
            foreach (var offset in _dragOffsets)
                dests.Add(hovered + offset);
            _highlighter.SetHoverCells(dests, valid);

            if (_stampMerged)
                PositionStampPlate(host, hovered);
            else
                PositionStampIcons(host, hovered);
        }

        public void Clear()
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

        // ---- Internal helpers ----

        private VisualElement CreateStampIcon(VisualElement host, Sprite sprite)
        {
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("le-stamp-icon");
            icon.style.backgroundImage = Background.FromSprite(sprite);
            icon.style.display = DisplayStyle.None;
            host.Add(icon);
            return icon;
        }

        private ObjectPlate CreateStampPlate(VisualElement host)
        {
            var plate = new ObjectPlate(ghost: true);
            plate.style.display = DisplayStyle.None;
            host.Add(plate);
            return plate;
        }

        private void PositionStampIcons(VisualElement host, Vector2Int hovered)
        {
            foreach (var stamp in _stampIcons)
            {
                var dst = hovered + stamp.Offset;
                if (!_ctx.Level.InBounds(dst))
                {
                    stamp.Element.style.display = DisplayStyle.None;
                    continue;
                }

                var wb = _ctx.Cells[dst.x, dst.y].worldBound;
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

            var inBounds = new List<Vector2Int>(_dragOffsets.Count);
            foreach (var offset in _dragOffsets)
            {
                var dst = hovered + offset;
                if (!_ctx.Level.InBounds(dst))
                    continue;

                inBounds.Add(dst);
                var wb = _ctx.Cells[dst.x, dst.y].worldBound;
                minX = Mathf.Min(minX, wb.xMin);
                minY = Mathf.Min(minY, wb.yMin);
                maxX = Mathf.Max(maxX, wb.xMax);
                maxY = Mathf.Max(maxY, wb.yMax);
            }

            if (inBounds.Count == 0)
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

            var rects = new Rect[inBounds.Count];
            var coords = new Vector2Int[inBounds.Count];
            for (int i = 0; i < inBounds.Count; i++)
            {
                var wb = _ctx.Cells[inBounds[i].x, inBounds[i].y].worldBound;
                rects[i] = new Rect(
                    wb.xMin - minX - CellGapPad,
                    wb.yMin - minY - CellGapPad,
                    wb.width + 2 * CellGapPad,
                    wb.height + 2 * CellGapPad);
                coords[i] = inBounds[i];
            }

            _stampPlate.SetShape(rects, coords);
        }

        private void HideStampVisuals()
        {
            if (_stampPlate != null)
                _stampPlate.style.display = DisplayStyle.None;

            foreach (var stamp in _stampIcons)
                stamp.Element.style.display = DisplayStyle.None;
        }
    }
}
