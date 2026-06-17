using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Code.LevelEditor
{
    [CreateAssetMenu(fileName = "NewBlock", menuName = "StaticData/Levels/Block", order = 802)]
    public class BlockDataEditor : ScriptableObject
    {
        [SerializeField] private string id;
        [SerializeField] private Sprite icon;
        [SerializeField] private GameObject prefab;

        // Multi-cell shape as cell offsets from the block's center (origin).
        // Empty == a single 1x1 block.
        [SerializeField] private List<Vector2Int> footprint = new();

        private static readonly List<Vector2Int> SingleCell = new() { Vector2Int.zero };

        public string ID => id;
        public Sprite Icon => icon;
        public GameObject Prefab => prefab;

        public IReadOnlyList<Vector2Int> Footprint =>
            footprint != null && footprint.Count > 0 ? footprint : SingleCell;

        public bool IsMultiCell => footprint != null && footprint.Count > 1;

        public void SetFootprint(IEnumerable<Vector2Int> offsets)
        {
            footprint = offsets != null ? new List<Vector2Int>(offsets) : new List<Vector2Int>();
        }

        public void SetID(string newId)
        {
            id = newId;
            this.name = newId;

#if UNITY_EDITOR
            string assetPath = AssetDatabase.GetAssetPath(this);
            if (!string.IsNullOrEmpty(assetPath))
            {
                AssetDatabase.RenameAsset(assetPath, newId);
            }
#endif
        }

        public void SetIcon(Sprite newIcon)
        {
            icon = newIcon;
        }

        public void SetPrefab(GameObject newPrefab)
        {
            prefab = newPrefab;
        }

        public override string ToString() => string.IsNullOrEmpty(id) ? "Unnamed Block" : id;

        public override bool Equals(object obj) => obj is BlockDataEditor other && id == other.id;

        public override int GetHashCode() => id != null ? id.GetHashCode() : 0;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(id))
                SetID(id);
        }
#endif
    }
}