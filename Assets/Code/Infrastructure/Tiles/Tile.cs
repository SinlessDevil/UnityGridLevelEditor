using System;
using UnityEngine;
using DG.Tweening;

namespace Code
{
    public class Tile : MonoBehaviour
    {
        [Header("Refs")]
        [SerializeField] private MeshRenderer _tile;
        [SerializeField] private GameObject _positionSetUpBlock;

        private Vector3 _constantScaleTile;
        private Vector3 _blockTargetScale = Vector3.one;

        private GameObject _block;

        private Tween _tileTween;
        private Tween _blockTween;

        public void SetColor(Color color)
        {
            _tile.materials[0].color = color;
        }

        public void SetUpScale()
        {
            _constantScaleTile = _tile.transform.localScale;
        }
        
        public void SetBlock(GameObject block)
        {
            _block = block;
            _block.transform.position = _positionSetUpBlock.transform.position;
            _block.transform.SetParent(this.transform);

            // Remember the prefab's own scale so the show animation restores it
            // instead of forcing every block to scale 1.
            _blockTargetScale = _block.transform.localScale;
            if (_blockTargetScale == Vector3.zero)
                _blockTargetScale = Vector3.one;

            _block.transform.localScale = Vector3.zero;
        }

        public void PlayAnimationShowTile()
        {
            _tile.transform.localScale = Vector3.zero;
            _tileTween?.Kill();

            _tileTween = _tile.transform.DOScale(_constantScaleTile, 0.25f)
                .SetEase(Ease.OutBack);
        }

        public void PlayAnimationHideTile(Action callback)
        {
            _tileTween?.Kill();

            _tileTween = _tile.transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    callback?.Invoke();
                });
        }

        public void PlayAnimationShowBlock()
        {
            if (_block == null) return;

            _block.transform.localScale = Vector3.zero;
            _blockTween?.Kill();

            _blockTween = _block.transform.DOScale(_blockTargetScale, 0.25f)
                .SetEase(Ease.OutBack);
        }

        public void PlayAnimationHideBlock(Action callback)
        {
            if (_block == null) return;

            _blockTween?.Kill();

            _blockTween = _block.transform.DOScale(Vector3.zero, 0.2f)
                .SetEase(Ease.InBack)
                .OnComplete(() =>
                {
                    callback?.Invoke();
                });
        }
    }
}
