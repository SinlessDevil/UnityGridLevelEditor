using System;
using System.Collections.Generic;
using System.Linq;
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
        public List<Vector2Int> Footprint = new();

        public void LoadFrom(BlockDataEditor block)
        {
            Id = block.ID;
            Icon = block.Icon;
            Prefab = block.Prefab;
            Footprint = block.Footprint.ToList();
        }

        public void ApplyTo(BlockDataEditor block)
        {
            block.SetID(Id);
            block.SetIcon(Icon);
            block.SetPrefab(Prefab);
            block.SetFootprint(Footprint);
            EditorUtility.SetDirty(block);
            AssetDatabase.SaveAssets();
        }
    }
}
