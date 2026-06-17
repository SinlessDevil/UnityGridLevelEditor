using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// Draws a placed multi-cell object as one merged plate that follows its exact
    /// footprint (any shape: rectangle, L, T, cross...). Fill/outline are painted
    /// per-cell with internal seams removed; hit-testing is restricted to the real
    /// cells so clicks on empty (concave) cells fall through to the grid. The icon,
    /// direction arrow and id are placed on real cells so they stay inside the shape.
    /// </summary>
    public class ObjectPlate : VisualElement
    {
        private static readonly Color FillColor = new(0.43f, 0.43f, 0.43f);
        private static readonly Color OutlineColor = new(0.59f, 0.59f, 0.59f);
        private static readonly Color SelectedColor = new(0.31f, 0.86f, 0.31f);
        private static readonly Color GhostFill = new(0.43f, 0.43f, 0.43f, 0.45f);
        private static readonly Color GhostOutline = new(0.65f, 0.65f, 0.65f, 0.8f);

        private readonly bool _ghost;
        private readonly VisualElement _decorLayer;

        private Rect[] _rects = Array.Empty<Rect>();
        private Vector2Int[] _coords = Array.Empty<Vector2Int>();
        private HashSet<Vector2Int> _set = new();
        private bool _selected;

        private Sprite _icon;
        private string _id;
        private float _arrowAngle;
        private float _zoom = 1f;

        public ObjectPlate(bool ghost = false)
        {
            _ghost = ghost;
            if (ghost)
                pickingMode = PickingMode.Ignore;

            // left/top/width/height are absolute coordinates within the host layer.
            style.position = Position.Absolute;

            _decorLayer = new VisualElement { pickingMode = PickingMode.Ignore };
            _decorLayer.style.position = Position.Absolute;
            _decorLayer.style.left = 0;
            _decorLayer.style.top = 0;
            _decorLayer.style.right = 0;
            _decorLayer.style.bottom = 0;
            Add(_decorLayer);

            generateVisualContent += OnGenerateVisualContent;
        }

        public void SetContent(Sprite icon, string id, float arrowAngle)
        {
            _icon = icon;
            _id = id;
            _arrowAngle = arrowAngle;
            RebuildDecor();
        }

        /// <summary>Scales the decor (icon padding, arrow/id fonts) to match the grid zoom.</summary>
        public void SetZoom(float zoom) => _zoom = zoom;

        /// <summary>Sets the cell rects (local space) and their grid coords (for seam detection).</summary>
        public void SetShape(Rect[] rects, Vector2Int[] coords)
        {
            _rects = rects;
            _coords = coords;
            _set = new HashSet<Vector2Int>(coords);
            MarkDirtyRepaint();
            RebuildDecor();
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected)
                return;

            _selected = selected;
            MarkDirtyRepaint();
        }

        public override bool ContainsPoint(Vector2 localPoint)
        {
            foreach (var r in _rects)
                if (r.Contains(localPoint))
                    return true;

            return false;
        }

        // ---- Painting ----

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            if (_rects.Length == 0)
                return;

            var p = mgc.painter2D;

            p.fillColor = _ghost ? GhostFill : FillColor;
            foreach (var r in _rects)
            {
                p.BeginPath();
                p.MoveTo(new Vector2(r.xMin, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMin));
                p.LineTo(new Vector2(r.xMax, r.yMax));
                p.LineTo(new Vector2(r.xMin, r.yMax));
                p.ClosePath();
                p.Fill();
            }

            p.lineWidth = 2f;
            p.strokeColor = _selected ? SelectedColor : (_ghost ? GhostOutline : OutlineColor);

            for (int i = 0; i < _rects.Length; i++)
            {
                var r = _rects[i];
                var c = _coords[i];

                if (!_set.Contains(new Vector2Int(c.x - 1, c.y))) StrokeLine(p, r.xMin, r.yMin, r.xMin, r.yMax);
                if (!_set.Contains(new Vector2Int(c.x + 1, c.y))) StrokeLine(p, r.xMax, r.yMin, r.xMax, r.yMax);
                if (!_set.Contains(new Vector2Int(c.x, c.y - 1))) StrokeLine(p, r.xMin, r.yMin, r.xMax, r.yMin);
                if (!_set.Contains(new Vector2Int(c.x, c.y + 1))) StrokeLine(p, r.xMin, r.yMax, r.xMax, r.yMax);
            }
        }

        private static void StrokeLine(Painter2D p, float x1, float y1, float x2, float y2)
        {
            p.BeginPath();
            p.MoveTo(new Vector2(x1, y1));
            p.LineTo(new Vector2(x2, y2));
            p.Stroke();
        }

        // ---- Decor (icon / arrow / id) placed on real cells ----

        private void RebuildDecor()
        {
            _decorLayer.Clear();
            if (_rects.Length == 0 || _ghost)
                return;

            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (var c in _coords)
            {
                minX = Mathf.Min(minX, c.x);
                minY = Mathf.Min(minY, c.y);
                maxX = Mathf.Max(maxX, c.x);
                maxY = Mathf.Max(maxY, c.y);
            }

            int centerX = (minX + maxX) / 2;
            bool rectangular = _coords.Length == (maxX - minX + 1) * (maxY - minY + 1);

            // One icon: spans the whole rectangle, otherwise sits on the central cell.
            if (_icon != null)
                AddIcon(rectangular ? UnionRect() : _rects[NearestToCentroid()]);

            // Arrow at the top of the top row's central cell.
            AddArrow(_rects[PickRowCell(minY, centerX)]);

            // Id at the bottom of the bottom row's central cell.
            if (!string.IsNullOrEmpty(_id))
                AddId(_rects[PickRowCell(maxY, centerX)]);
        }

        private void AddIcon(Rect rect)
        {
            float pad = 5f * _zoom;
            var icon = new VisualElement { pickingMode = PickingMode.Ignore };
            icon.AddToClassList("le-plate__tile");
            icon.style.backgroundImage = Background.FromSprite(_icon);
            icon.style.left = rect.xMin + pad;
            icon.style.top = rect.yMin + pad;
            icon.style.width = Mathf.Max(0f, rect.width - 2 * pad);
            icon.style.height = Mathf.Max(0f, rect.height - 2 * pad);
            _decorLayer.Add(icon);
        }

        private void AddArrow(Rect cell)
        {
            var arrow = new Label("↑") { pickingMode = PickingMode.Ignore };
            arrow.AddToClassList("le-plate__arrow");
            arrow.style.fontSize = 18f * _zoom;
            arrow.style.left = cell.center.x;
            arrow.style.top = cell.yMin - 2f * _zoom;
            arrow.style.translate = new Translate(Length.Percent(-50), 0);
            arrow.style.rotate = new Rotate(new Angle(_arrowAngle, AngleUnit.Degree));
            _decorLayer.Add(arrow);
        }

        private void AddId(Rect cell)
        {
            var label = new Label(_id) { pickingMode = PickingMode.Ignore };
            label.AddToClassList("le-plate__id");
            label.style.fontSize = 10f * _zoom;
            label.style.left = cell.center.x;
            label.style.top = cell.yMax - 14f * _zoom;
            label.style.translate = new Translate(Length.Percent(-50), 0);
            _decorLayer.Add(label);
        }

        private Rect UnionRect()
        {
            float minx = float.MaxValue, miny = float.MaxValue, maxx = float.MinValue, maxy = float.MinValue;
            foreach (var r in _rects)
            {
                minx = Mathf.Min(minx, r.xMin);
                miny = Mathf.Min(miny, r.yMin);
                maxx = Mathf.Max(maxx, r.xMax);
                maxy = Mathf.Max(maxy, r.yMax);
            }

            return Rect.MinMaxRect(minx, miny, maxx, maxy);
        }

        private int NearestToCentroid()
        {
            Vector2 centroid = Vector2.zero;
            foreach (var r in _rects)
                centroid += r.center;
            centroid /= _rects.Length;

            int best = 0;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _rects.Length; i++)
            {
                float d = (_rects[i].center - centroid).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = i;
                }
            }

            return best;
        }

        private int PickRowCell(int gridY, int centerX)
        {
            int best = 0;
            int bestDx = int.MaxValue;
            for (int i = 0; i < _coords.Length; i++)
            {
                if (_coords[i].y != gridY)
                    continue;

                int dx = Mathf.Abs(_coords[i].x - centerX);
                if (dx < bestDx)
                {
                    bestDx = dx;
                    best = i;
                }
            }

            return best;
        }
    }
}
