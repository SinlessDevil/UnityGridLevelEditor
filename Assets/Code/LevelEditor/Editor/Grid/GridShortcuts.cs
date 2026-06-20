using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Keyboard shortcuts for the grid: Delete/Backspace clears the selection, Ctrl+C/V
    /// copy/paste through <see cref="LevelClipboard"/>, and Ctrl+Z / Ctrl+Y (Ctrl+Shift+Z)
    /// raise undo/redo, which the window's history handles.
    /// </summary>
    internal sealed class GridShortcuts
    {
        private readonly GridContext _ctx;
        private readonly GridSelection _selection;
        private readonly Action _refresh;
        private readonly Action _changed;
        private readonly Action _undo;
        private readonly Action _redo;
        private readonly Action<string, LogLevel> _log;
        private readonly Action<IEnumerable<Vector2Int>> _flash;

        public GridShortcuts(
            GridContext ctx,
            GridSelection selection,
            Action refresh,
            Action changed,
            Action undo,
            Action redo,
            Action<string, LogLevel> log,
            Action<IEnumerable<Vector2Int>> flash)
        {
            _ctx = ctx;
            _selection = selection;
            _refresh = refresh;
            _changed = changed;
            _undo = undo;
            _redo = redo;
            _log = log;
            _flash = flash;
        }

        public void OnKeyDown(KeyDownEvent evt)
        {
            if (_ctx.Level == null)
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
            else if (evt.keyCode == KeyCode.Z)
            {
                if (evt.shiftKey)
                    _redo();
                else
                    _undo();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Y)
            {
                _redo();
                evt.StopPropagation();
            }
        }

        private void DeleteSelection()
        {
            if (_selection.Count == 0)
            {
                _log("Nothing selected to delete", LogLevel.Info);
                return;
            }

            int count = _selection.Count;

            // Clears standalone selected cells and any whole object they touch.
            LevelGridOps.ClearArea(_ctx.Level, _selection);

            EditorUtility.SetDirty(_ctx.Level);
            _selection.Clear();
            _selection.Start = null;
            _refresh();
            _changed();

            _log($"Deleted {count} selected cell(s)", LogLevel.Info);
        }

        private void CopySelection()
        {
            if (_selection.Count == 0)
            {
                _log("Nothing selected to copy", LogLevel.Info);
                return;
            }

            LevelClipboard.Copy(_ctx.Level, _selection);
            _log($"Copied {LevelClipboard.Count} cell(s)", LogLevel.Success);
        }

        private void PasteSelection()
        {
            if (!LevelClipboard.HasData)
            {
                _log("Clipboard is empty", LogLevel.Info);
                return;
            }

            if (_selection.Count == 0)
            {
                _log("Select a target cell to paste", LogLevel.Error);
                return;
            }

            var (pasted, blocked) = LevelClipboard.Paste(_ctx.Level, _selection[0]);

            if (pasted > 0)
            {
                EditorUtility.SetDirty(_ctx.Level);
                _refresh();
                _changed();
            }

            if (blocked.Count > 0)
                _flash(blocked);

            if (blocked.Count > 0 && pasted == 0)
                _log("Can't paste here: no free space", LogLevel.Error);
            else if (blocked.Count > 0)
                _log($"Pasted {pasted}, {blocked.Count} cell(s) skipped (no space)", LogLevel.Info);
            else
                _log($"Pasted {pasted} cell(s)", LogLevel.Success);
        }
    }
}
