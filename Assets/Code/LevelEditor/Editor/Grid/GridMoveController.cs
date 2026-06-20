using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Drag-to-move of existing cells / objects. A short drag past the threshold turns a
    /// pointer-down into a move: the carried cells (a standalone block, a multi-selection,
    /// or a whole multi-cell object) are previewed via <see cref="DragPreview"/> and, on a
    /// valid drop, relocated — clearing whatever they land on. Pointer capture lives on the
    /// grid element so the drag survives cell/plate rebuilds underneath.
    /// </summary>
    internal sealed class GridMoveController
    {
        private const float DragThreshold = 4f;

        private readonly GridContext _ctx;
        private readonly GridSelection _selection;
        private readonly GridHitTester _hitTester;
        private readonly GridHighlighter _highlighter;
        private readonly DragPreview _preview;
        private readonly VisualElement _captureTarget;
        private readonly Action _refresh;
        private readonly Action _changed;

        private Vector2Int? _moveSource;
        private Vector2 _pointerDownPos;
        private bool _moving;
        private int _movePointerId = -1;

        public GridMoveController(
            GridContext ctx,
            GridSelection selection,
            GridHitTester hitTester,
            GridHighlighter highlighter,
            DragPreview preview,
            VisualElement captureTarget,
            Action refresh,
            Action changed)
        {
            _ctx = ctx;
            _selection = selection;
            _hitTester = hitTester;
            _highlighter = highlighter;
            _preview = preview;
            _captureTarget = captureTarget;
            _refresh = refresh;
            _changed = changed;
        }

        public void BeginDrag(Vector2Int anchor, int pointerId, Vector2 pos)
        {
            _moveSource = anchor;
            _pointerDownPos = pos;
            _moving = false;
            _movePointerId = pointerId;
            _captureTarget.CapturePointer(pointerId);
        }

        public void OnPointerMove(PointerMoveEvent evt)
        {
            if (!_moveSource.HasValue || evt.pointerId != _movePointerId)
                return;

            if (!_moving)
            {
                if (Vector2.Distance(evt.position, _pointerDownPos) < DragThreshold)
                    return;

                if (!StartMoving())
                    return; // nothing to carry; stays a click
            }

            _preview.Update(evt.position);
        }

        /// <summary>Decides which cells move and builds the preview stamp.</summary>
        private bool StartMoving()
        {
            var anchor = _moveSource.Value;
            var anchorCell = _ctx.Level.GetCell(anchor);

            // A whole multi-cell object drags as one merged plate.
            if (anchorCell.InstanceId != 0)
            {
                _moving = true;
                _preview.BuildMoveStamp(anchor, LevelGridOps.GetInstanceCells(_ctx.Level, anchorCell.InstanceId), merged: true);
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
            _preview.BuildMoveStamp(anchor, group, merged: false);
            return true;
        }

        public void OnPointerUp(PointerUpEvent evt)
        {
            if (!_moveSource.HasValue || evt.pointerId != _movePointerId)
                return;

            if (_moving)
            {
                if (_hitTester.TryGetCellAt(evt.position, out var hovered))
                {
                    var delta = hovered - _moveSource.Value;
                    if (delta != Vector2Int.zero && _preview.IsDropValid(hovered))
                        MoveGroup(delta);
                }
            }
            else
            {
                // Plain click (no drag): clear selection, same for cells and objects.
                _selection.Start = null;
                _selection.Clear();
                _refresh();
            }

            int pointerId = _movePointerId;
            EndMove();
            if (_captureTarget.HasPointerCapture(pointerId))
                _captureTarget.ReleasePointer(pointerId);

            evt.StopPropagation();
        }

        public void Cancel()
        {
            if (_moveSource.HasValue)
                EndMove();
        }

        /// <summary>Resets only the move state (the preview is cleared separately on rebuild).</summary>
        public void Reset()
        {
            _moveSource = null;
            _moving = false;
            _movePointerId = -1;
        }

        private void EndMove()
        {
            _moveSource = null;
            _moving = false;
            _movePointerId = -1;
            _highlighter.SetHoverCells(Array.Empty<Vector2Int>(), true);
            _preview.Clear();
        }

        private void MoveGroup(Vector2Int delta)
        {
            var level = _ctx.Level;
            var anchor = _moveSource.Value;
            var offsets = _preview.Offsets;

            var sources = new List<Vector2Int>(offsets.Count);
            var blocks = new List<BlockDataEditor>(offsets.Count);
            var rotations = new List<Quaternion>(offsets.Count);
            var instanceIds = new List<int>(offsets.Count);

            foreach (var offset in offsets)
            {
                var src = anchor + offset;
                var c = level.GetCell(src);
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
                var c = level.GetCell(d);
                if (c.InstanceId != 0 && !movingInstances.Contains(c.InstanceId))
                    clearInstances.Add(c.InstanceId);
            }

            foreach (var id in clearInstances)
                LevelGridOps.ClearInstance(level, id);

            var sourceSet = new HashSet<Vector2Int>(sources);

            foreach (var src in sources)
                LevelGridOps.ClearCell(level, src);

            foreach (var d in dests)
                if (!sourceSet.Contains(d))
                    LevelGridOps.ClearCell(level, d);

            for (int i = 0; i < sources.Count; i++)
            {
                var c = level.GetCell(dests[i]);
                c.Block = blocks[i];
                c.Rotation = rotations[i];
                c.InstanceId = instanceIds[i];
            }

            EditorUtility.SetDirty(level);

            _selection.Clear();
            _selection.AddRange(dests);
            _selection.Start = null;

            _refresh();
            _changed();
        }
    }
}
