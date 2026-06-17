using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Renders a <see cref="LevelMatrixEditor"/> grid as a matrix of clickable cells.
    /// Replaces the Odin TableMatrix + IMGUI cell drawing.
    /// </summary>
    public class LevelGridView : VisualElement
    {
        private LevelMatrixEditor _level;
        private VisualElement[,] _cells;

        private readonly List<Vector2Int> _selection = new();
        private Vector2Int? _selectionStart;

        /// <summary>
        /// Raised on right click. Arguments are the clicked cell position and the
        /// mouse position in panel coordinates (for placing the popup).
        /// </summary>
        public event Action<Vector2Int, Vector2> CellRightClicked;

        public IReadOnlyList<Vector2Int> Selection => _selection;

        public LevelGridView()
        {
            AddToClassList("le-grid");
        }

        public void SetLevel(LevelMatrixEditor level)
        {
            _level = level;
            _selection.Clear();
            _selectionStart = null;
            Rebuild();
        }

        public void Rebuild()
        {
            Clear();
            _cells = null;

            if (_level == null)
                return;

            _level.EnsureInitialized();
            _cells = new VisualElement[_level.Width, _level.Height];

            for (int y = 0; y < _level.Height; y++)
            {
                var row = new VisualElement();
                row.AddToClassList("le-grid__row");

                for (int x = 0; x < _level.Width; x++)
                {
                    var cell = BuildCell(x, y);
                    _cells[x, y] = cell;
                    row.Add(cell);
                    UpdateCell(x, y);
                }

                Add(row);
            }
        }

        /// <summary>Re-reads cell data and selection state into the existing visuals.</summary>
        public void RefreshCells()
        {
            if (_cells == null || _level == null)
                return;

            for (int y = 0; y < _level.Height; y++)
            for (int x = 0; x < _level.Width; x++)
                UpdateCell(x, y);
        }

        private VisualElement BuildCell(int x, int y)
        {
            var cell = new VisualElement { name = $"cell_{x}_{y}" };
            cell.AddToClassList("le-cell");

            var icon = new VisualElement { name = "icon", pickingMode = PickingMode.Ignore };
            icon.AddToClassList("le-cell__icon");
            cell.Add(icon);

            var arrow = new Label("↑") { name = "arrow", pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("le-cell__arrow");
            cell.Add(arrow);

            var id = new Label { name = "id", pickingMode = PickingMode.Ignore };
            id.AddToClassList("le-cell__id");
            cell.Add(id);

            var pos = new Vector2Int(x, y);
            cell.RegisterCallback<MouseDownEvent>(evt => OnCellMouseDown(evt, pos));

            return cell;
        }

        private void OnCellMouseDown(MouseDownEvent evt, Vector2Int pos)
        {
            bool ctrl = evt.ctrlKey || evt.commandKey;

            if (evt.button == 0)
            {
                if (ctrl)
                {
                    if (!_selectionStart.HasValue)
                    {
                        _selectionStart = pos;
                        _selection.Clear();
                        _selection.Add(pos);
                    }
                    else
                    {
                        SelectRange(_selectionStart.Value, pos);
                        _selectionStart = null;
                    }
                }
                else
                {
                    _selectionStart = null;
                    _selection.Clear();
                }

                RefreshCells();
                evt.StopPropagation();
            }
            else if (evt.button == 1)
            {
                if (!_selection.Contains(pos))
                {
                    _selection.Clear();
                    _selection.Add(pos);
                    RefreshCells();
                }

                CellRightClicked?.Invoke(pos, evt.mousePosition);
                evt.StopPropagation();
            }
        }

        private void SelectRange(Vector2Int start, Vector2Int end)
        {
            _selection.Clear();

            int minX = Mathf.Min(start.x, end.x);
            int maxX = Mathf.Max(start.x, end.x);
            int minY = Mathf.Min(start.y, end.y);
            int maxY = Mathf.Max(start.y, end.y);

            for (int x = minX; x <= maxX; x++)
            for (int y = minY; y <= maxY; y++)
                _selection.Add(new Vector2Int(x, y));
        }

        private void UpdateCell(int x, int y)
        {
            var cell = _cells[x, y];
            var data = _level.GetCell(x, y);

            var icon = cell.Q<VisualElement>("icon");
            var arrow = cell.Q<Label>("arrow");
            var id = cell.Q<Label>("id");

            bool hasBlock = data?.Block != null && data.Block.Icon != null;

            if (hasBlock)
            {
                icon.style.display = DisplayStyle.Flex;
                icon.style.backgroundImage = Background.FromSprite(data.Block.Icon);

                id.style.display = DisplayStyle.Flex;
                id.text = data.Block.ID;

                arrow.style.display = DisplayStyle.Flex;
                float angle = data.Rotation.eulerAngles.y;
                arrow.style.rotate = new Rotate(new Angle(angle, AngleUnit.Degree));
            }
            else
            {
                icon.style.display = DisplayStyle.None;
                id.style.display = DisplayStyle.None;
                arrow.style.display = DisplayStyle.None;
            }

            cell.EnableInClassList("le-cell--empty", !hasBlock);
            cell.EnableInClassList("le-cell--selected", _selection.Contains(new Vector2Int(x, y)));
        }
    }
}
