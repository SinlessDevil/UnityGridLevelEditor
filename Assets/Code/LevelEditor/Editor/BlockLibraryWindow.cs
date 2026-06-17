using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    public class BlockLibraryWindow : EditorWindow
    {
        private const string BlocksFolder = "Assets/Resources/StaticData/BlocksData";

        private enum SortMode
        {
            ByID,
            ByPrefabName,
            ByIconName
        }

        private BlockLibrary _library;
        private BlockDataEditor _selectedBlock;
        private readonly BlockEditorDTO _blockDraft = new();

        private string _searchFilter = "";
        private SortMode _sortMode = SortMode.ByID;
        private bool _sortAscending = true;

        // create fields
        private TextField _newIdField;
        private ObjectField _newSpriteField;
        private ObjectField _newPrefabField;
        private BlockShapeEditor _newShapeEditor;

        // list + selected
        private VisualElement _listContainer;
        private VisualElement _selectedContainer;

        [MenuItem("Tools/Grid Level Editor/Block Window")]
        public static void ShowWindow()
        {
            var window = GetWindow<BlockLibraryWindow>(false, "Block Library", true);
            window.minSize = new Vector2(420, 560);
        }

        private void CreateGUI()
        {
            LevelEditorStyles.Apply(rootVisualElement);
            rootVisualElement.AddToClassList("le-root");

            LoadOrCreateLibrary();

            var scroll = new ScrollView();
            rootVisualElement.Add(scroll);

            scroll.Add(BuildCreateSection());
            scroll.Add(BuildSearchSection());

            var listBox = LevelEditorStyles.Section("Existing Blocks");
            _listContainer = new VisualElement();
            listBox.Add(_listContainer);
            scroll.Add(listBox);

            _selectedContainer = new VisualElement();
            scroll.Add(_selectedContainer);

            RebuildList();
            RebuildSelected();
        }

        // ---- Library ----

        private void LoadOrCreateLibrary()
        {
            var guids = AssetDatabase.FindAssets("t:BlockLibrary");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                _library = AssetDatabase.LoadAssetAtPath<BlockLibrary>(path);
            }
            else
            {
                CreateLibrary();
            }
        }

        private void CreateLibrary()
        {
            if (!AssetDatabase.IsValidFolder(BlocksFolder))
                AssetDatabase.CreateFolder("Assets/Resources/StaticData", "BlocksData");

            var asset = CreateInstance<BlockLibrary>();
            asset.AllBlocks = new List<BlockDataEditor>();

            var path = $"{BlocksFolder}/BlockLibrary.asset";
            AssetDatabase.CreateAsset(asset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _library = asset;
            Debug.Log("BlockLibrary created at " + path);
        }

        // ---- Create block ----

        private VisualElement BuildCreateSection()
        {
            var box = LevelEditorStyles.Section("Create Block");

            _newIdField = new TextField("Block ID");
            box.Add(_newIdField);

            _newSpriteField = new ObjectField("Icon") { objectType = typeof(Sprite), allowSceneObjects = false };
            box.Add(_newSpriteField);

            _newPrefabField = new ObjectField("Prefab") { objectType = typeof(GameObject), allowSceneObjects = false };
            box.Add(_newPrefabField);

            box.Add(new Label("Shape") { style = { marginTop = 4 } });
            _newShapeEditor = new BlockShapeEditor();
            box.Add(_newShapeEditor);

            var create = new Button(CreateNewBlock) { text = "Create New Block" };
            create.AddToClassList("le-create-button");
            box.Add(create);

            return box;
        }

        private void CreateNewBlock()
        {
            string id = _newIdField.value;
            var sprite = _newSpriteField.value as Sprite;

            if (string.IsNullOrEmpty(id) || sprite == null)
            {
                Debug.LogWarning("Block ID and Icon are required.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(BlocksFolder))
                AssetDatabase.CreateFolder("Assets/Resources/StaticData", "BlocksData");

            var newBlock = CreateInstance<BlockDataEditor>();
            newBlock.name = id;
            newBlock.SetID(id);
            newBlock.SetIcon(sprite);
            newBlock.SetPrefab(_newPrefabField.value as GameObject);
            newBlock.SetFootprint(_newShapeEditor.GetOffsets());

            var assetPath = AssetDatabase.GenerateUniqueAssetPath($"{BlocksFolder}/{id}.asset");
            AssetDatabase.CreateAsset(newBlock, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            _library.AllBlocks ??= new List<BlockDataEditor>();
            _library.AllBlocks.Add(newBlock);
            EditorUtility.SetDirty(_library);
            AssetDatabase.SaveAssets();

            Debug.Log($"Created new block: {id}");

            _newIdField.value = "";
            _newSpriteField.value = null;
            _newPrefabField.value = null;
            RebuildList();
        }

        // ---- Search / sort ----

        private VisualElement BuildSearchSection()
        {
            var box = LevelEditorStyles.Section("Search & Sort");

            var search = new TextField("Filter");
            search.RegisterValueChangedCallback(evt =>
            {
                _searchFilter = evt.newValue;
                RebuildList();
            });
            box.Add(search);

            var row = new VisualElement();
            row.AddToClassList("le-row");

            var sort = new EnumField("Sort", _sortMode);
            sort.style.flexGrow = 1;
            sort.RegisterValueChangedCallback(evt =>
            {
                _sortMode = (SortMode)evt.newValue;
                RebuildList();
            });
            row.Add(sort);

            var direction = new Button { text = _sortAscending ? "▲" : "▼" };
            direction.clicked += () =>
            {
                _sortAscending = !_sortAscending;
                direction.text = _sortAscending ? "▲" : "▼";
                RebuildList();
            };
            row.Add(direction);

            box.Add(row);
            return box;
        }

        // ---- List ----

        private void RebuildList()
        {
            _listContainer.Clear();

            if (_library == null)
            {
                _listContainer.Add(new HelpBox("No BlockLibrary asset found.", HelpBoxMessageType.Error));
                return;
            }

            if (_library.AllBlocks == null || _library.AllBlocks.Count == 0)
            {
                _listContainer.Add(new HelpBox("No blocks found.", HelpBoxMessageType.Info));
                return;
            }

            var blocks = _library.AllBlocks.Where(b => b != null).ToList();

            if (!string.IsNullOrEmpty(_searchFilter))
                blocks = blocks.Where(b => b.ID.ToLower().Contains(_searchFilter.ToLower())).ToList();

            blocks.Sort(CompareBlocks);
            if (!_sortAscending)
                blocks.Reverse();

            foreach (var block in blocks)
                _listContainer.Add(BuildBlockRow(block));
        }

        private int CompareBlocks(BlockDataEditor a, BlockDataEditor b)
        {
            return _sortMode switch
            {
                SortMode.ByID => string.Compare(a.ID, b.ID, StringComparison.OrdinalIgnoreCase),
                SortMode.ByIconName => string.Compare(a.Icon?.name ?? "", b.Icon?.name ?? "",
                    StringComparison.OrdinalIgnoreCase),
                SortMode.ByPrefabName => string.Compare(a.Prefab?.name ?? "", b.Prefab?.name ?? "",
                    StringComparison.OrdinalIgnoreCase),
                _ => 0
            };
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

            var label = new Label($"{block.ID}   ({(block.Prefab != null ? block.Prefab.name : "no prefab")})");
            label.AddToClassList("le-block-row__label");
            row.Add(label);

            var select = new Button(() =>
            {
                _selectedBlock = block;
                _blockDraft.LoadFrom(block);
                RebuildSelected();
            }) { text = "Select" };
            select.style.width = 60;
            row.Add(select);

            var delete = new Button(() => DeleteBlock(block)) { text = "X" };
            delete.style.width = 24;
            delete.AddToClassList("le-delete-button");
            row.Add(delete);

            return row;
        }

        private void DeleteBlock(BlockDataEditor block)
        {
            if (!EditorUtility.DisplayDialog("Delete Block",
                    $"Are you sure you want to delete '{block.ID}'?", "Yes", "No"))
                return;

            var path = AssetDatabase.GetAssetPath(block);
            _library.AllBlocks.Remove(block);

            if (_selectedBlock == block)
                _selectedBlock = null;

            if (!string.IsNullOrEmpty(path))
                AssetDatabase.DeleteAsset(path);

            EditorUtility.SetDirty(_library);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            RebuildList();
            RebuildSelected();
        }

        // ---- Selected block editor ----

        private void RebuildSelected()
        {
            _selectedContainer.Clear();

            var box = LevelEditorStyles.Section("Selected Block");

            if (_selectedBlock == null)
            {
                box.Add(new HelpBox("No block selected.", HelpBoxMessageType.Info));
                _selectedContainer.Add(box);
                return;
            }

            var idField = new TextField("ID") { value = _blockDraft.Id };
            idField.RegisterValueChangedCallback(evt => _blockDraft.Id = evt.newValue);
            box.Add(idField);

            var iconField = new ObjectField("Icon") { objectType = typeof(Sprite), value = _blockDraft.Icon };
            iconField.RegisterValueChangedCallback(evt => _blockDraft.Icon = evt.newValue as Sprite);
            box.Add(iconField);

            var prefabField = new ObjectField("Prefab") { objectType = typeof(GameObject), value = _blockDraft.Prefab };
            prefabField.RegisterValueChangedCallback(evt => _blockDraft.Prefab = evt.newValue as GameObject);
            box.Add(prefabField);

            box.Add(new Label("Shape") { style = { marginTop = 4 } });
            var shapeEditor = new BlockShapeEditor();
            shapeEditor.SetOffsets(_blockDraft.Footprint);
            box.Add(shapeEditor);

            var apply = new Button(() =>
            {
                _blockDraft.Footprint = shapeEditor.GetOffsets();
                _blockDraft.ApplyTo(_selectedBlock);
                RebuildList();
                RebuildSelected();
            }) { text = "Apply Changes" };
            apply.AddToClassList("le-create-button");
            box.Add(apply);

            _selectedContainer.Add(box);
        }
    }
}
