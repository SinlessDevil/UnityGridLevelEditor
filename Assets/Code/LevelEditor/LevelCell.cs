using System;
using UnityEngine;

namespace Code.LevelEditor
{
    [Serializable]
    public class LevelCell
    {
        public BlockDataEditor Block;
        public Quaternion Rotation = Quaternion.identity;

        // 0 = standalone single cell. A positive value groups all cells of one
        // placed multi-cell block instance.
        public int InstanceId;
    }
}