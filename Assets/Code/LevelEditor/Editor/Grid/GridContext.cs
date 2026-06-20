using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Shared, mutable references the grid sub-components read live. The owning
    /// <see cref="LevelGridView"/> keeps <see cref="Level"/> and <see cref="Cells"/>
    /// in sync (set on level switch and on rebuild); the helpers never reassign them.
    /// </summary>
    internal sealed class GridContext
    {
        public LevelMatrixEditor Level;
        public VisualElement[,] Cells;

        public int Width => Level != null ? Level.Width : 0;
        public int Height => Level != null ? Level.Height : 0;

        public bool IsValid(Vector2Int p) =>
            Cells != null && Level != null &&
            p.x >= 0 && p.x < Width && p.y >= 0 && p.y < Height;
    }
}
