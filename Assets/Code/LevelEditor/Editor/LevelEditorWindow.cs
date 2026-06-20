using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    public class LevelEditorWindow : EditorWindow
    {
        private const string LevelsFolder = "Assets/Resources/StaticData/LevelsData";

        private readonly List<LevelMatrixEditor> _levels = new();
        private readonly LevelHistory _history = new();
        private LevelMatrixEditor _selected;

        private DropdownField _levelDropdown;
        private TextField _nameField;
        private IntegerField _indexField;
        private IntegerField _widthField;
        private IntegerField _heightField;
        private VisualElement _inspector;
        private LevelGridView _grid;
        private BlockPaletteView _palette;
        private BlockDragController _dragController;
        private LogView _log;
        private VisualElement _gridViewport;
        private GridPanController _pan;
        private VisualElement _hintsPopup;
        private bool _hintsVisible;

        private IntegerField _newIndexField;
        private TextField _newNameField;
        private IntegerField _newWidthField;
        private IntegerField _newHeightField;

        [MenuItem("Tools/Grid Level Editor/Level Window")]
        private static void OpenWindow()
        {
            var window = GetWindow<LevelEditorWindow>();
            window.titleContent = new GUIContent("Level Editor");
            window.minSize = new Vector2(520, 600);
            window.Show();
        }

        private void CreateGUI()
        {
            LevelEditorStyles.Apply(rootVisualElement);
            rootVisualElement.AddToClassList("le-root");

            _dragController = new BlockDragController(rootVisualElement, () => _grid);

            var split = new TwoPaneSplitView(0, 170, TwoPaneSplitViewOrientation.Horizontal);
            split.style.flexGrow = 1;
            rootVisualElement.Add(split);

            _palette = new BlockPaletteView(_dragController.AttachTile, BlockLibraryWindow.ShowWindow, RefreshPalette);
            split.Add(_palette);

            // Right pane: scrollable controls on top, the grid viewport filling the rest down
            // to the log. The viewport clips the grid and the grid is freely panned inside it.
            var rightPane = new VisualElement();
            rightPane.style.flexGrow = 1;
            split.Add(rightPane);

            var controls = new ScrollView();
            rightPane.Add(controls);

            controls.Add(BuildSelectSection());
            controls.Add(BuildCreateSection());

            _inspector = new VisualElement();
            controls.Add(_inspector);

            _gridViewport = new VisualElement();
            _gridViewport.AddToClassList("le-grid-viewport");
            _gridViewport.style.flexGrow = 1;
            rightPane.Add(_gridViewport);

            _log = new LogView();
            rootVisualElement.Add(_log);

            BuildHelpOverlay();

            LoadLevels();
            RefreshDropdown();
            RefreshPalette();

            if (_levels.Count > 0)
                SelectLevel(_levels[0]);
            else
                RebuildInspector();
        }

        private void RefreshPalette() => _palette?.SetLibrary(LoadLibrary());

        /// <summary>A collapsible (vertical) section. State is remembered via viewDataKey.</summary>
        private static Foldout MakeSection(string title, string viewDataKey)
        {
            var fold = new Foldout { text = title, value = true, viewDataKey = viewDataKey };
            fold.AddToClassList("le-foldout");
            return fold;
        }

        // ---- Select / delete ----

        private VisualElement BuildSelectSection()
        {
            var box = MakeSection("Select Level", "le-fold-select");

            _levelDropdown = new DropdownField("Level");
            _levelDropdown.RegisterValueChangedCallback(_ =>
            {
                int index = _levelDropdown.index;
                if (index >= 0 && index < _levels.Count)
                    SelectLevel(_levels[index]);
            });
            box.Add(_levelDropdown);

            var row = new VisualElement();
            row.AddToClassList("le-row");

            var refresh = new Button(() =>
            {
                LoadLevels();
                RefreshDropdown();
                RefreshPalette();
                SelectLevel(_levels.FirstOrDefault());
            }) { text = "Refresh" };
            refresh.style.flexGrow = 1;
            row.Add(refresh);

            var delete = new Button(DeleteSelected) { text = "🗑 Delete Selected" };
            delete.AddToClassList("le-delete-button");
            delete.style.flexGrow = 1;
            row.Add(delete);

            box.Add(row);
            return box;
        }

        private void DeleteSelected()
        {
            if (_selected == null)
            {
                Debug.LogWarning("⚠️ No level selected to delete.");
                return;
            }

            if (!EditorUtility.DisplayDialog(
                    "Delete Level",
                    $"Are you sure you want to delete '{_selected.name}'?\nThis cannot be undone.",
                    "Delete", "Cancel"))
                return;

            string deletedName = _selected.name;
            string path = AssetDatabase.GetAssetPath(_selected);
            if (!string.IsNullOrEmpty(path))
            {
                AssetDatabase.DeleteAsset(path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                _log?.Log($"Deleted level '{deletedName}'", LogLevel.Info);
            }

            LoadLevels();
            RefreshDropdown();
            SelectLevel(_levels.FirstOrDefault());
        }

        // ---- Create ----

        private VisualElement BuildCreateSection()
        {
            var box = MakeSection("Create Level", "le-fold-create");

            _newIndexField = new IntegerField("Level Index") { value = 1 };
            _newNameField = new TextField("Name") { value = "NewLevel" };
            _newWidthField = new IntegerField("Width") { value = 5 };
            _newHeightField = new IntegerField("Height") { value = 5 };

            box.Add(_newIndexField);
            box.Add(_newNameField);
            box.Add(_newWidthField);
            box.Add(_newHeightField);

            var create = new Button(CreateLevel) { text = "Create Level" };
            create.AddToClassList("le-create-button");
            box.Add(create);

            return box;
        }

        private void CreateLevel()
        {
            if (!AssetDatabase.IsValidFolder(LevelsFolder))
                AssetDatabase.CreateFolder("Assets/Resources/StaticData", "LevelsData");

            string levelName = string.IsNullOrWhiteSpace(_newNameField.value) ? "NewLevel" : _newNameField.value;

            var level = CreateInstance<LevelMatrixEditor>();
            level.name = levelName;
            level.IndexLevel = _newIndexField.value;
            level.Resize(_newWidthField.value, _newHeightField.value);

            string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{LevelsFolder}/{levelName}.asset");
            AssetDatabase.CreateAsset(level, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            _log?.Log($"Created level '{level.name}'", LogLevel.Success);

            LoadLevels();
            RefreshDropdown();
            SelectLevel(level);
        }

        // ---- Inspector + grid ----

        private void RebuildInspector()
        {
            _inspector.Clear();
            _gridViewport.Clear();

            if (_selected == null)
            {
                _history.Clear();
                _inspector.Add(new HelpBox("No level selected. Create one above.", HelpBoxMessageType.Info));
                return;
            }

            var box = MakeSection($"Editing: {_selected.name}", "le-fold-editing");

            var nameRow = new VisualElement();
            nameRow.AddToClassList("le-row");
            _nameField = new TextField("Name") { value = _selected.name };
            _nameField.style.flexGrow = 1;
            nameRow.Add(_nameField);
            nameRow.Add(new Button(RenameSelected) { text = "Rename" });
            box.Add(nameRow);

            _indexField = new IntegerField("Level Index") { value = _selected.IndexLevel };
            _indexField.RegisterValueChangedCallback(evt =>
            {
                _selected.IndexLevel = evt.newValue;
                EditorUtility.SetDirty(_selected);
            });
            box.Add(_indexField);

            var sizeRow = new VisualElement();
            sizeRow.AddToClassList("le-row");

            _widthField = new IntegerField("Width") { value = _selected.Width };
            _heightField = new IntegerField("Height") { value = _selected.Height };
            _widthField.style.flexGrow = 1;
            _heightField.style.flexGrow = 1;
            sizeRow.Add(_widthField);
            sizeRow.Add(_heightField);

            var resize = new Button(ResizeSelected) { text = "Apply Size" };
            sizeRow.Add(resize);
            box.Add(sizeRow);

            _inspector.Add(box);

            _grid = new LevelGridView();
            _grid.GhostHost = rootVisualElement;
            _grid.Log = _log.Log;
            _grid.CellRightClicked += OnCellRightClicked;
            _grid.Changed += () => _history.Record();
            _grid.UndoRequested += PerformUndo;
            _grid.RedoRequested += PerformRedo;
            // Keep the grid at its natural size: align-self stops the cross-axis stretch and
            // flex-shrink 0 stops the viewport from compressing it vertically when the map is
            // larger than the window (which distorted the row spacing).
            _grid.style.alignSelf = Align.FlexStart;
            _grid.style.flexShrink = 0;
            _grid.SetLevel(_selected);

            // Fresh undo history per opened level (reset on level switch / rename).
            _history.Begin(_selected);

            // The grid fills the clipped viewport and is panned freely inside it with the
            // middle mouse button (no scroll); the ⊕ button in the zoom toolbar recenters it.
            _gridViewport.Add(_grid);
            _pan = new GridPanController(_gridViewport, _grid);

            _inspector.Add(BuildZoomToolbar());
        }

        /// <summary>Zoom controls for the grid (−/＋/1:1 plus a live percentage label). Ctrl+wheel also zooms.</summary>
        private VisualElement BuildZoomToolbar()
        {
            var row = new VisualElement();
            row.AddToClassList("le-zoom-row");

            var label = new Label { name = "zoom-label" };
            label.AddToClassList("le-zoom-label");

            void UpdateLabel() => label.text = $"{Mathf.RoundToInt(_grid.Zoom * 100f)}%";

            row.Add(new Label("Zoom") { name = "zoom-title" });
            row.Add(new Button(() => _grid.ZoomOut()) { text = "－", tooltip = "Zoom out" });
            row.Add(label);
            row.Add(new Button(() => _grid.ZoomIn()) { text = "＋", tooltip = "Zoom in" });
            row.Add(new Button(() => _grid.ResetZoom()) { text = "1:1", tooltip = "Reset zoom" });
            row.Add(new Button(() => _pan.ResetPosition()) { text = "⊕", tooltip = "Reset map position (middle-mouse drag to pan)" });

            _grid.ZoomChanged += UpdateLabel;
            UpdateLabel();

            return row;
        }

        private static readonly string[] HintLines =
        {
            "Palette → drag onto grid to place a block",
            "LMB drag — move a block / object",
            "Ctrl + LMB — select cells (range)",
            "RMB — block menu (rotate / copy / paste / clear)",
            "Ctrl + C / Ctrl + V — copy / paste selected cells",
            "Ctrl + Z / Ctrl + Y — undo / redo",
            "Backspace / Delete — clear selected cells",
            "Ctrl + Mouse Wheel — zoom the grid (or use −/＋)",
            "Middle-Mouse drag — pan the map (⊕ resets position)"
        };

        private void BuildHelpOverlay()
        {
            _hintsPopup = new VisualElement();
            _hintsPopup.AddToClassList("le-help-popup");
            _hintsPopup.style.display = DisplayStyle.None;

            _hintsPopup.Add(new Label("Controls") { name = "hints-title" });
            foreach (var line in HintLines)
            {
                var label = new Label("• " + line);
                label.AddToClassList("le-hints__line");
                _hintsPopup.Add(label);
            }

            rootVisualElement.Add(_hintsPopup);

            var help = new Button(ToggleHints) { text = "?", tooltip = "Controls" };
            help.AddToClassList("le-help-button");
            rootVisualElement.Add(help);

            // Dismiss when clicking outside the popup/button.
            rootVisualElement.RegisterCallback<PointerDownEvent>(evt =>
            {
                if (!_hintsVisible)
                    return;

                var target = evt.target as VisualElement;
                if (target != null && (_hintsPopup.Contains(target) || help.Contains(target)))
                    return;

                SetHintsVisible(false);
            }, TrickleDown.TrickleDown);
        }

        private void ToggleHints() => SetHintsVisible(!_hintsVisible);

        private void SetHintsVisible(bool visible)
        {
            _hintsVisible = visible;
            _hintsPopup.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void RenameSelected()
        {
            if (_selected == null)
                return;

            string newName = _nameField.value?.Trim();
            if (string.IsNullOrEmpty(newName) || newName == _selected.name)
                return;

            string path = AssetDatabase.GetAssetPath(_selected);
            string error = AssetDatabase.RenameAsset(path, newName);

            if (!string.IsNullOrEmpty(error))
            {
                _log.Log($"Rename failed: {error}", LogLevel.Error);
                return;
            }

            AssetDatabase.SaveAssets();
            _log.Log($"Renamed level to '{_selected.name}'", LogLevel.Success);

            LoadLevels();
            RefreshDropdown();
            SelectLevel(_selected);
        }

        private void ResizeSelected()
        {
            if (_selected == null)
                return;

            _selected.Resize(_widthField.value, _heightField.value);
            EditorUtility.SetDirty(_selected);

            _widthField.SetValueWithoutNotify(_selected.Width);
            _heightField.SetValueWithoutNotify(_selected.Height);
            _grid.Rebuild();
            _history.Record();

            _log.Log($"Resized to {_selected.Width}×{_selected.Height}", LogLevel.Info);
        }

        // ---- Undo / redo (per-level, window-local history) ----

        private void OnDisable() => _history.Clear();

        private void PerformUndo()
        {
            if (_history.Undo())
            {
                SyncAfterHistory();
                _log.Log("Undo", LogLevel.Info);
            }
            else
            {
                _log.Log("Nothing to undo", LogLevel.Info);
            }
        }

        private void PerformRedo()
        {
            if (_history.Redo())
            {
                SyncAfterHistory();
                _log.Log("Redo", LogLevel.Info);
            }
            else
            {
                _log.Log("Nothing to redo", LogLevel.Info);
            }
        }

        /// <summary>Re-reads the restored level into the grid and size fields (size can change via resize undo).</summary>
        private void SyncAfterHistory()
        {
            if (_selected == null)
                return;

            _grid.SetLevel(_selected); // re-reads cells, clears selection, rebuilds
            _widthField?.SetValueWithoutNotify(_selected.Width);
            _heightField?.SetValueWithoutNotify(_selected.Height);
        }

        private void OnCellRightClicked(Vector2Int pos, Vector2 panelMouse)
        {
            var library = LoadLibrary();
            if (library == null)
            {
                Debug.LogWarning("⚠️ No BlockLibrary found. Open the Block Library window to create one.");
                return;
            }

            Vector2 screenPos = position.position + panelMouse;
            var activator = new Rect(screenPos, Vector2.zero);

            BlockPopup.Show(activator, _selected, library, _grid.Selection.ToList(),
                onChanged: () =>
                {
                    EditorUtility.SetDirty(_selected);
                    _grid.RefreshCells();
                    _history.Record();
                },
                onPreview: () => _grid.RefreshCells(),
                _log.Log,
                cells => _grid.FlashCells(cells));
        }

        // ---- Data loading ----

        private void SelectLevel(LevelMatrixEditor level)
        {
            _selected = level;

            if (level != null)
            {
                int index = _levels.IndexOf(level);
                if (index >= 0)
                    _levelDropdown.SetValueWithoutNotify(_levelDropdown.choices[index]);
            }

            RebuildInspector();
        }

        private void LoadLevels()
        {
            _levels.Clear();

            if (!AssetDatabase.IsValidFolder(LevelsFolder))
                return;

            string[] guids = AssetDatabase.FindAssets("t:LevelMatrixEditor", new[] { LevelsFolder });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var level = AssetDatabase.LoadAssetAtPath<LevelMatrixEditor>(path);
                if (level != null)
                    _levels.Add(level);
            }

            _levels.Sort((a, b) => a.IndexLevel.CompareTo(b.IndexLevel));
        }

        private void RefreshDropdown()
        {
            _levelDropdown.choices = _levels.Select(l => l.name).ToList();
        }

        private static BlockLibrary LoadLibrary()
        {
            string[] guids = AssetDatabase.FindAssets("t:BlockLibrary");
            if (guids.Length == 0)
                return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<BlockLibrary>(path);
        }
    }
}
