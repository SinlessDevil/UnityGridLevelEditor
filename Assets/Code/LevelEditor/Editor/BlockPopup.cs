using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Context popup for the selected cell(s): pick a block, rotate, copy/paste, clear.
    /// UI Toolkit replacement for the old IMGUI BlockPopupWindow.
    /// </summary>
    public class BlockPopup : EditorWindow
    {
        private static BlockPopup _current;
        private static readonly Dictionary<Vector2Int, LevelCell> _clipboard = new();

        private LevelMatrixEditor _level;
        private BlockLibrary _library;
        private List<Vector2Int> _selection;
        private Action _onChanged;

        private string _blockFilter = "";
        private VisualElement _blockListContainer;
        private VisualElement _selectedListContainer;

        public static void Show(
            Rect activatorRect,
            LevelMatrixEditor level,
            BlockLibrary library,
            IEnumerable<Vector2Int> selection,
            Action onChanged)
        {
            if (_current != null)
                _current.Close();

            _current = CreateInstance<BlockPopup>();
            _current._level = level;
            _current._library = library;
            _current._selection = selection.ToList();
            _current._onChanged = onChanged;

            _current.ShowPopup();
            _current.position = new Rect(activatorRect.position, new Vector2(360, 560));
            _current.Focus();
        }

        private void OnLostFocus() => Close();

        private void OnDestroy()
        {
            if (_current == this)
                _current = null;
        }

        private void CreateGUI()
        {
            var root = rootVisualElement;
            LevelEditorStyles.Apply(root);
            root.style.paddingLeft = 8;
            root.style.paddingRight = 8;
            root.style.paddingTop = 8;
            root.style.paddingBottom = 8;

            root.Add(BuildBlockSection());
            root.Add(BuildSelectedSection());
            root.Add(BuildRotationSection());
            root.Add(BuildCopyPasteSection());
            root.Add(BuildClearButton());

            RebuildBlockList();
            RebuildSelectedList();
        }

        private VisualElement Section(string title)
        {
            var box = new VisualElement();
            box.AddToClassList("le-section");

            var label = new Label(title);
            label.AddToClassList("le-section__title");
            box.Add(label);

            return box;
        }

        private VisualElement BuildBlockSection()
        {
            var box = Section("🧱 Select Block");

            var search = new TextField("Search");
            search.RegisterValueChangedCallback(evt =>
            {
                _blockFilter = evt.newValue;
                RebuildBlockList();
            });
            box.Add(search);

            var scroll = new ScrollView { style = { maxHeight = 200 } };
            _blockListContainer = scroll.contentContainer;
            box.Add(scroll);

            return box;
        }

        private void RebuildBlockList()
        {
            _blockListContainer.Clear();

            if (_library?.AllBlocks == null)
                return;

            foreach (var block in _library.AllBlocks)
            {
                if (block == null)
                    continue;

                if (!string.IsNullOrEmpty(_blockFilter) &&
                    !block.ID.ToLower().Contains(_blockFilter.ToLower()))
                    continue;

                _blockListContainer.Add(BuildBlockRow(block));
            }
        }

        private VisualElement BuildBlockRow(BlockDataEditor block)
        {
            var row = new VisualElement();
            row.AddToClassList("le-block-row");

            var icon = new VisualElement();
            icon.AddToClassList("le-block-row__icon");
            if (block.Icon != null)
                icon.style.backgroundImage = Background.FromSprite(block.Icon);
            row.Add(icon);

            var label = new Label(block.ID);
            label.AddToClassList("le-block-row__label");
            row.Add(label);

            var setButton = new Button(() => ApplyBlock(block)) { text = "Set" };
            setButton.style.width = 60;
            row.Add(setButton);

            return row;
        }

        private VisualElement BuildSelectedSection()
        {
            var box = Section("📦 Selected Cells");

            var scroll = new ScrollView { style = { maxHeight = 140 } };
            _selectedListContainer = scroll.contentContainer;
            box.Add(scroll);

            return box;
        }

        private void RebuildSelectedList()
        {
            _selectedListContainer.Clear();

            if (_selection == null)
                return;

            foreach (var pos in _selection)
            {
                if (!_level.InBounds(pos))
                    continue;

                var cell = _level.GetCell(pos);

                var row = new VisualElement();
                row.AddToClassList("le-block-row");

                var icon = new VisualElement();
                icon.AddToClassList("le-block-row__icon");
                if (cell?.Block?.Icon != null)
                    icon.style.backgroundImage = Background.FromSprite(cell.Block.Icon);
                row.Add(icon);

                string blockId = cell?.Block != null ? cell.Block.ID : "Empty";
                var label = new Label($"[{pos.x},{pos.y}] - {blockId}");
                label.AddToClassList("le-block-row__label");
                row.Add(label);

                _selectedListContainer.Add(row);
            }
        }

        private VisualElement BuildRotationSection()
        {
            var box = Section("↻ Rotation");

            var row = new VisualElement();
            row.AddToClassList("le-rotation-row");

            row.Add(RotationButton("↑\n0°", 0));
            row.Add(RotationButton("→\n90°", 90));
            row.Add(RotationButton("↓\n180°", 180));
            row.Add(RotationButton("←\n270°", 270));

            box.Add(row);
            return box;
        }

        private Button RotationButton(string text, float angle)
        {
            var button = new Button(() => ApplyRotation(angle)) { text = text };
            button.AddToClassList("le-rotation-button");
            return button;
        }

        private VisualElement BuildCopyPasteSection()
        {
            var box = Section("⧉ Copy / Paste");

            var row = new VisualElement();
            row.AddToClassList("le-rotation-row");

            var copy = new Button(Copy) { text = "Copy" };
            copy.AddToClassList("le-rotation-button");
            row.Add(copy);

            var paste = new Button(Paste) { text = "Paste" };
            paste.AddToClassList("le-rotation-button");
            row.Add(paste);

            box.Add(row);
            return box;
        }

        private VisualElement BuildClearButton()
        {
            var button = new Button(ClearBlocks) { text = "🗑 Clear Block(s)" };
            button.AddToClassList("le-clear-button");
            return button;
        }

        // ---- Operations ----

        private void ApplyBlock(BlockDataEditor block)
        {
            foreach (var pos in _selection)
            {
                if (!_level.InBounds(pos))
                    continue;

                var cell = _level.GetCell(pos);
                cell.Block = block;
                cell.Rotation = Quaternion.identity;
                cell.InstanceId = 0;
            }

            Changed();
        }

        private void ApplyRotation(float angle)
        {
            var rotation = Quaternion.Euler(0, angle, 0);

            foreach (var pos in _selection)
            {
                if (_level.InBounds(pos))
                    _level.GetCell(pos).Rotation = rotation;
            }

            Changed();
        }

        private void ClearBlocks()
        {
            foreach (var pos in _selection)
            {
                if (!_level.InBounds(pos))
                    continue;

                var cell = _level.GetCell(pos);
                cell.Block = null;
                cell.Rotation = Quaternion.identity;
                cell.InstanceId = 0;
            }

            Changed();
        }

        private void Copy()
        {
            _clipboard.Clear();

            if (_selection == null || _selection.Count == 0)
            {
                Debug.LogWarning("No cells selected to copy.");
                return;
            }

            foreach (var pos in _selection)
            {
                if (!_level.InBounds(pos))
                    continue;

                var cell = _level.GetCell(pos);
                _clipboard[pos] = new LevelCell { Block = cell.Block, Rotation = cell.Rotation };
            }

            Debug.Log($"Copied {_clipboard.Count} cells");
        }

        private void Paste()
        {
            if (_clipboard.Count == 0)
            {
                Debug.LogWarning("Nothing copied to paste.");
                return;
            }

            if (_selection == null || _selection.Count == 0)
            {
                Debug.LogWarning("No cells selected to paste into.");
                return;
            }

            var firstTarget = _selection[0];
            var firstSource = _clipboard.Keys.First();
            int pasteCount = 0;

            foreach (var pair in _clipboard)
            {
                var targetPos = firstTarget + (pair.Key - firstSource);
                if (!_level.InBounds(targetPos))
                    continue;

                var targetCell = _level.GetCell(targetPos);
                targetCell.Block = pair.Value.Block;
                targetCell.Rotation = pair.Value.Rotation;
                targetCell.InstanceId = 0;
                pasteCount++;
            }

            Debug.Log($"Pasted {pasteCount} cells starting from [{firstTarget.x},{firstTarget.y}]");
            Changed();
        }

        private void Changed()
        {
            EditorUtility.SetDirty(_level);
            _onChanged?.Invoke();
            RebuildSelectedList();
        }
    }
}
