using System.Collections.Generic;
using UnityEngine;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Shared copy/paste buffer for grid cells. Used by both the keyboard shortcuts
    /// (Ctrl+C / Ctrl+V) and the block popup.
    /// </summary>
    public static class LevelClipboard
    {
        private static readonly Dictionary<Vector2Int, LevelCell> Cells = new();

        public static int Count => Cells.Count;
        public static bool HasData => Cells.Count > 0;

        public static void Copy(LevelMatrixEditor level, IEnumerable<Vector2Int> selection)
        {
            Cells.Clear();

            foreach (var pos in selection)
            {
                if (!level.InBounds(pos))
                    continue;

                var c = level.GetCell(pos);
                Cells[pos] = new LevelCell { Block = c.Block, Rotation = c.Rotation };
            }
        }

        /// <summary>
        /// Pastes the buffer with its first cell aligned to <paramref name="anchor"/>.
        /// Cells belonging to a solid object are skipped. Returns how many cells were
        /// written and how many were blocked.
        /// </summary>
        public static (int pasted, int blocked) Paste(LevelMatrixEditor level, Vector2Int anchor)
        {
            if (Cells.Count == 0)
                return (0, 0);

            Vector2Int firstSource = default;
            bool found = false;
            foreach (var key in Cells.Keys)
            {
                firstSource = key;
                found = true;
                break;
            }

            if (!found)
                return (0, 0);

            int pasted = 0, blocked = 0;
            foreach (var pair in Cells)
            {
                var target = anchor + (pair.Key - firstSource);
                if (!level.InBounds(target))
                    continue;

                var cell = level.GetCell(target);
                if (cell.InstanceId != 0)
                {
                    blocked++;
                    continue;
                }

                cell.Block = pair.Value.Block;
                cell.Rotation = pair.Value.Rotation;
                cell.InstanceId = 0;
                pasted++;
            }

            return (pasted, blocked);
        }
    }
}
