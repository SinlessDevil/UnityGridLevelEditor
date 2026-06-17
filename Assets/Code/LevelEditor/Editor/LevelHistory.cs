using System.Collections.Generic;
using UnityEditor;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Per-level undo/redo stack for the Level Editor window. Holds deep snapshots of
    /// the grid and lives entirely inside the window — it is independent of Unity's
    /// global Undo. The history is bound to one level via <see cref="Begin"/> and is
    /// dropped when the level changes or the window closes (see <see cref="Clear"/>).
    /// </summary>
    public sealed class LevelHistory
    {
        private const int Capacity = 100;

        private sealed class Snapshot
        {
            public LevelCell[] Cells;
            public int Width;
            public int Height;
            public int NextInstanceId;
        }

        // _undo always holds [baseline, ..., current]; its top mirrors the live level.
        private readonly List<Snapshot> _undo = new();
        private readonly List<Snapshot> _redo = new();
        private LevelMatrixEditor _level;

        public bool CanUndo => _undo.Count > 1;
        public bool CanRedo => _redo.Count > 0;

        /// <summary>Binds to a level and records its current state as the baseline.</summary>
        public void Begin(LevelMatrixEditor level)
        {
            _level = level;
            _undo.Clear();
            _redo.Clear();

            if (_level != null)
                _undo.Add(Capture());
        }

        /// <summary>Drops the whole history (on window close or before binding a new level).</summary>
        public void Clear()
        {
            _level = null;
            _undo.Clear();
            _redo.Clear();
        }

        /// <summary>Records the level's current state as a new undo step. Call after a committed change.</summary>
        public void Record()
        {
            if (_level == null || _undo.Count == 0)
                return;

            _undo.Add(Capture());
            _redo.Clear();

            while (_undo.Count > Capacity)
                _undo.RemoveAt(0);
        }

        public bool Undo()
        {
            if (!CanUndo)
                return false;

            var current = _undo[_undo.Count - 1];
            _undo.RemoveAt(_undo.Count - 1);
            _redo.Add(current);

            Restore(_undo[_undo.Count - 1]);
            return true;
        }

        public bool Redo()
        {
            if (!CanRedo)
                return false;

            var next = _redo[_redo.Count - 1];
            _redo.RemoveAt(_redo.Count - 1);
            _undo.Add(next);

            Restore(next);
            return true;
        }

        private Snapshot Capture() => new()
        {
            Cells = _level.CloneCells(),
            Width = _level.Width,
            Height = _level.Height,
            NextInstanceId = _level.NextInstanceId
        };

        private void Restore(Snapshot snapshot)
        {
            _level.RestoreCells(snapshot.Cells, snapshot.Width, snapshot.Height, snapshot.NextInstanceId);
            EditorUtility.SetDirty(_level);
        }
    }
}
