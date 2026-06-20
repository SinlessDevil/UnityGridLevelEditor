using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Builds and refreshes the per-cell visuals (icon, direction arrow, id label and
    /// state classes). A standalone block draws its icon here; cells that belong to a
    /// multi-cell instance are left empty because the merged plate draws them instead.
    /// </summary>
    internal sealed class CellRenderer
    {
        private readonly GridContext _ctx;
        private readonly GridZoom _zoom;
        private readonly GridSelection _selection;
        private readonly Action<PointerDownEvent, Vector2Int> _onPointerDown;

        public CellRenderer(
            GridContext ctx,
            GridZoom zoom,
            GridSelection selection,
            Action<PointerDownEvent, Vector2Int> onPointerDown)
        {
            _ctx = ctx;
            _zoom = zoom;
            _selection = selection;
            _onPointerDown = onPointerDown;
        }

        public VisualElement Build(int x, int y)
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
            cell.RegisterCallback<PointerDownEvent>(evt => _onPointerDown(evt, pos));

            _zoom.ApplyCellMetrics(cell);
            return cell;
        }

        public void Update(int x, int y)
        {
            var cell = _ctx.Cells[x, y];
            var data = _ctx.Level.GetCell(x, y);

            var icon = cell.Q<VisualElement>("icon");
            var arrow = cell.Q<Label>("arrow");
            var id = cell.Q<Label>("id");

            bool hasBlock = data?.Block != null;
            bool isInstance = hasBlock && data.InstanceId != 0;
            // Per-cell icon is only drawn for standalone blocks; instances draw a plate.
            bool showIcon = hasBlock && !isInstance && BlockIconResolver.HasVisual(data.Block);

            if (showIcon)
            {
                icon.style.display = DisplayStyle.Flex;
                BlockIconResolver.Apply(icon, data.Block);

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
            cell.EnableInClassList("le-cell--instance", isInstance);
            cell.EnableInClassList("le-cell--selected", _selection.Contains(new Vector2Int(x, y)));
        }
    }
}
