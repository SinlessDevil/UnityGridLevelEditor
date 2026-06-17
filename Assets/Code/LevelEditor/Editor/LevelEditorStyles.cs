using UnityEditor;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>Shared USS loading and small layout helpers for the level editor windows.</summary>
    public static class LevelEditorStyles
    {
        private static StyleSheet _styleSheet;

        public static void Apply(VisualElement root)
        {
            var sheet = LoadStyleSheet();
            if (sheet != null && !root.styleSheets.Contains(sheet))
                root.styleSheets.Add(sheet);
        }

        public static VisualElement Section(string title)
        {
            var box = new VisualElement();
            box.AddToClassList("le-section");

            var label = new Label(title);
            label.AddToClassList("le-section__title");
            box.Add(label);

            return box;
        }

        private static StyleSheet LoadStyleSheet()
        {
            if (_styleSheet != null)
                return _styleSheet;

            string[] guids = AssetDatabase.FindAssets("LevelEditor t:StyleSheet");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith("LevelEditor.uss"))
                {
                    _styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
                    break;
                }
            }

            return _styleSheet;
        }
    }
}
