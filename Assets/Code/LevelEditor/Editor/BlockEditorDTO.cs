using System;
using UnityEditor;
using UnityEngine;

namespace Code.LevelEditor.Editor
{
    [Serializable]
    public class BlockEditorDTO
    {
        public string Id;
        public Sprite Icon;
        public GameObject Prefab;

        public void LoadFrom(BlockDataEditor block)
        {
            Id = block.ID;
            Icon = block.Icon;
            Prefab = block.Prefab;
        }

        public void ApplyTo(BlockDataEditor block)
        {
            block.SetID(Id);
            block.SetIcon(Icon);
            block.SetPrefab(Prefab);
            EditorUtility.SetDirty(block);
            AssetDatabase.SaveAssets();
        }
    }
}
