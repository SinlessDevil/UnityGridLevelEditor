using System.Collections.Generic;
using UnityEngine;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Shared copy/paste buffer for grid cells. Used by both the keyboard shortcuts
    /// (Ctrl+C / Ctrl+V) and the block popup. Multi-cell objects are preserved:
    /// a copied object is pasted as one fresh object instance.
    /// </summary>
    public static class LevelClipboard
    {
        private readonly struct Copied
        {
            public readonly BlockDataEditor Block;
            public readonly Quaternion Rotation;
            public readonly int InstanceId;

            public Copied(BlockDataEditor block, Quaternion rotation, int instanceId)
            {
                Block = block;
                Rotation = rotation;
                InstanceId = instanceId;
            }
        }

        private static readonly Dictionary<Vector2Int, Copied> Cells = new();

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
                Cells[pos] = new Copied(c.Block, c.Rotation, c.InstanceId);
            }
        }

        /// <summary>
        /// Pastes the buffer with its first cell aligned to <paramref name="anchor"/>.
        /// Copied objects are placed only on free cells (all-or-nothing, with a new
        /// instance id); standalone cells overwrite empty/standalone targets but never
        /// objects. Returns how many cells were written and the destination cells that
        /// were blocked (for highlighting "no space here").
        /// </summary>
        public static (int pasted, List<Vector2Int> blocked) Paste(LevelMatrixEditor level, Vector2Int anchor)
        {
            var blocked = new List<Vector2Int>();

            if (Cells.Count == 0)
                return (0, blocked);

            Vector2Int firstSource = default;
            bool found = false;
            foreach (var key in Cells.Keys)
            {
                firstSource = key;
                found = true;
                break;
            }

            if (!found)
                return (0, blocked);

            var delta = anchor - firstSource;

            var objectGroups = new Dictionary<int, List<KeyValuePair<Vector2Int, Copied>>>();
            var standalone = new List<KeyValuePair<Vector2Int, Copied>>();

            foreach (var pair in Cells)
            {
                if (pair.Value.InstanceId != 0)
                {
                    if (!objectGroups.TryGetValue(pair.Value.InstanceId, out var list))
                        objectGroups[pair.Value.InstanceId] = list = new List<KeyValuePair<Vector2Int, Copied>>();
                    list.Add(pair);
                }
                else
                {
                    standalone.Add(pair);
                }
            }

            int pasted = 0;

            // Each copied object becomes one new instance, only if all its cells are free.
            foreach (var group in objectGroups.Values)
            {
                bool free = true;
                foreach (var pair in group)
                {
                    var t = pair.Key + delta;
                    if (!level.InBounds(t) || level.GetCell(t).Block != null)
                    {
                        free = false;
                        break;
                    }
                }

                if (!free)
                {
                    foreach (var pair in group)
                        blocked.Add(pair.Key + delta);
                    continue;
                }

                int newId = level.NewInstanceId();
                foreach (var pair in group)
                {
                    var c = level.GetCell(pair.Key + delta);
                    c.Block = pair.Value.Block;
                    c.Rotation = pair.Value.Rotation;
                    c.InstanceId = newId;
                    pasted++;
                }
            }

            // Standalone cells: overwrite empty/standalone targets, never break objects.
            foreach (var pair in standalone)
            {
                var t = pair.Key + delta;
                if (!level.InBounds(t))
                    continue;

                var c = level.GetCell(t);
                if (c.InstanceId != 0)
                {
                    blocked.Add(t);
                    continue;
                }

                c.Block = pair.Value.Block;
                c.Rotation = pair.Value.Rotation;
                c.InstanceId = 0;
                pasted++;
            }

            return (pasted, blocked);
        }
    }
}
