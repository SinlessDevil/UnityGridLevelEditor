using System;
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

        public BlockPaletteView(Action<VisualElement, BlockDataEditor> attachDrag)
        {
            _attachDrag = attachDrag;
            AddToClassList("le-palette");

            var title = new Label("Blocks");
            title.AddToClassList("le-section__title");
            Add(title);

            var scroll = new ScrollView();
            scroll.style.flexGrow = 1;
            Add(scroll);

            _tilesContainer = new VisualElement();
            _tilesContainer.AddToClassList("le-palette__tiles");
            scroll.Add(_tilesContainer);
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
