using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Placing a block dragged in from the palette. Mirrors the <see cref="DragPreview"/>
    /// footprint to the grid on drop, overwriting whatever lies underneath (unlike the
    /// popup, which requires empty cells). Driven externally by the palette's
    /// <c>BlockDragController</c> via the grid view's forwarding methods.
    /// </summary>
    internal sealed class PalettePlacement
    {
        private readonly GridContext _ctx;
        private readonly GridHitTester _hitTester;
        private readonly GridHighlighter _highlighter;
        private readonly DragPreview _preview;
        private readonly Action _refresh;
        private readonly Action _changed;
        private readonly Action<string, LogLevel> _log;

        private BlockDataEditor _externalBlock;

        public PalettePlacement(
            GridContext ctx,
            GridHitTester hitTester,
            GridHighlighter highlighter,
            DragPreview preview,
            Action refresh,
            Action changed,
            Action<string, LogLevel> log)
        {
            _ctx = ctx;
            _hitTester = hitTester;
            _highlighter = highlighter;
            _preview = preview;
            _refresh = refresh;
            _changed = changed;
            _log = log;
        }

        public void Begin(BlockDataEditor block)
        {
            _preview.BuildFootprintStamp(block);
            _externalBlock = block;
        }

        public void Update(Vector2 panelPosition) => _preview.Update(panelPosition);

        /// <summary>Fills the footprint cells if the drop is in bounds, clearing anything underneath.</summary>
        public bool TryPlace(Vector2 panelPosition)
        {
            if (_externalBlock == null || _ctx.Level == null)
                return false;

            if (!_hitTester.TryGetCellAt(panelPosition, out var hovered))
                return false; // dropped outside the grid

            if (!_preview.IsDropValid(hovered))
            {
                _log($"Can't place '{_externalBlock.ID}': out of bounds", LogLevel.Error);
                return false;
            }

            var offsets = _preview.Offsets;
            var targets = new List<Vector2Int>(offsets.Count);
            foreach (var offset in offsets)
                targets.Add(hovered + offset);

            LevelGridOps.ClearArea(_ctx.Level, targets);

            int instanceId = _externalBlock.IsMultiCell ? _ctx.Level.NewInstanceId() : 0;
            foreach (var t in targets)
            {
                var c = _ctx.Level.GetCell(t);
                c.Block = _externalBlock;
                c.Rotation = Quaternion.identity;
                c.InstanceId = instanceId;
            }

            EditorUtility.SetDirty(_ctx.Level);
            _refresh();
            _changed();
            _log($"Placed '{_externalBlock.ID}'", LogLevel.Success);
            return true;
        }

        public void Clear()
        {
            _externalBlock = null;
            _highlighter.SetHoverCells(Array.Empty<Vector2Int>(), true);
            _preview.Clear();
        }

        /// <summary>Drops the carried block without touching visuals (called on rebuild).</summary>
        public void ResetExternal() => _externalBlock = null;
    }
}
