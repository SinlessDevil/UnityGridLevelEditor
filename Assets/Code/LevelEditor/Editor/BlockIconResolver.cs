using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Resolves the editor-only visual for a block and applies it as a VisualElement's
    /// background. Priority: the block's explicit <see cref="BlockDataEditor.Icon"/> sprite
    /// (a manual override), otherwise Unity's auto-generated asset preview of the prefab —
    /// the same thumbnail shown in the Project window. Asset previews render asynchronously,
    /// so when one isn't ready yet the element polls (on its own scheduler) until it is.
    /// </summary>
    internal static class BlockIconResolver
    {
        /// <summary>True when the block can show something (a sprite or a previewable prefab).</summary>
        public static bool HasVisual(BlockDataEditor block) =>
            block != null && (block.Icon != null || block.Prefab != null);

        public static void Apply(VisualElement target, BlockDataEditor block)
        {
            if (block != null && block.Icon != null)
            {
                target.style.backgroundImage = Background.FromSprite(block.Icon);
                return;
            }

            var prefab = block != null ? block.Prefab : null;
            if (prefab == null)
            {
                target.style.backgroundImage = StyleKeyword.None;
                return;
            }

            var preview = AssetPreview.GetAssetPreview(prefab);
            if (preview != null)
            {
                target.style.backgroundImage = Background.FromTexture2D(preview);
                return;
            }

            // The preview is generated asynchronously: blank for now, then poll the element's
            // own scheduler (auto-paused when it leaves the panel) until the preview resolves.
            target.style.backgroundImage = StyleKeyword.None;
            int instanceId = prefab.GetInstanceID();

            IVisualElementScheduledItem poll = null;
            poll = target.schedule.Execute(() =>
            {
                var tex = AssetPreview.GetAssetPreview(prefab);
                if (tex != null)
                {
                    target.style.backgroundImage = Background.FromTexture2D(tex);
                    poll.Pause();
                }
                else if (!AssetPreview.IsLoadingAssetPreview(instanceId))
                {
                    poll.Pause(); // no preview will come (e.g. prefab without renderers)
                }
            }).Every(100);
        }
    }
}
