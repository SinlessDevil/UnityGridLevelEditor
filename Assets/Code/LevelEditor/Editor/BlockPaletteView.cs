using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Left-hand palette of blocks (read from the BlockLibrary). Each tile is a
    /// drag source; wiring the actual drag is delegated to <paramref name="attachDrag"/>.
    /// </summary>
    public class BlockPaletteView : VisualElement
    {
        private readonly Action<VisualElement, BlockDataEditor> _attachDrag;
        private readonly VisualElement _tilesContainer;

        public BlockPaletteView(
            Action<VisualElement, BlockDataEditor> attachDrag,
            Action onEditBlocks,
            Action onRefresh)
        {
            _attachDrag = attachDrag;
            AddToClassList("le-palette");

            var title = new Label("Blocks");
            title.AddToClassList("le-section__title");
            Add(title);

            var buttons = new VisualElement();
            buttons.AddToClassList("le-row");

            var edit = new Button(onEditBlocks) { text = "Block Editor", tooltip = "Open the block editor" };
            edit.style.flexGrow = 1;
            buttons.Add(edit);

            buttons.Add(BuildRefreshButton(onRefresh));

            Add(buttons);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            Add(scroll);

            _tilesContainer = new VisualElement();
            _tilesContainer.AddToClassList("le-palette__tiles");
            scroll.Add(_tilesContainer);
        }

        private static Button BuildRefreshButton(Action onRefresh)
        {
            var refresh = new Button(onRefresh) { tooltip = "Refresh palette" };

            var icon = EditorGUIUtility.IconContent("Refresh").image as Texture2D;
            if (icon != null)
            {
                var image = new Image
                {
                    image = icon,
                    scaleMode = ScaleMode.ScaleToFit,
                    pickingMode = PickingMode.Ignore
                };
                image.style.width = 16;
                image.style.height = 16;
                refresh.Add(image);
                refresh.style.width = 26;
            }
            else
            {
                refresh.text = "Refresh";
            }

            return refresh;
        }

        public void SetLibrary(BlockLibrary library)
        {
            _tilesContainer.Clear();

            if (library?.AllBlocks == null)
            {
                _tilesContainer.Add(new HelpBox("No BlockLibrary found.", HelpBoxMessageType.Info));
                return;
            }

            if (library.AllBlocks.Count == 0)
            {
                _tilesContainer.Add(new HelpBox("Library is empty.\nAdd blocks in the Block Window.",
                    HelpBoxMessageType.Info));
                return;
            }

            foreach (var block in library.AllBlocks)
            {
                if (block == null)
                    continue;

                _tilesContainer.Add(BuildTile(block));
            }
        }

        private VisualElement BuildTile(BlockDataEditor block)
        {
            var tile = new VisualElement { tooltip = block.ID };
            tile.AddToClassList("le-palette-tile");

            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("le-palette-tile__icon");
            if (block.Icon != null)
                icon.style.backgroundImage = Background.FromSprite(block.Icon);
            tile.Add(icon);

            var label = new Label(block.ID) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("le-palette-tile__label");
            tile.Add(label);

            _attachDrag?.Invoke(tile, block);

            return tile;
        }
    }
}
