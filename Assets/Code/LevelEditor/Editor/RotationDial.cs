using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Code.LevelEditor.Editor
{
    /// <summary>
    /// A circular knob for setting a Y rotation in degrees. 0° points up and the angle
    /// grows clockwise, matching the grid's direction arrow. Drag the handle to rotate:
    /// <see cref="AngleChanged"/> fires continuously (for a live preview) and
    /// <see cref="AngleCommitted"/> fires once on release (for a single undo step).
    /// </summary>
    public sealed class RotationDial : VisualElement
    {
        private const float Size = 90f;

        private static readonly Color RingColor = new(0.59f, 0.59f, 0.59f);
        private static readonly Color HandleColor = new(0.31f, 0.86f, 0.31f);

        private float _angle;
        private int _pointerId = -1;

        public event Action<float> AngleChanged;
        public event Action<float> AngleCommitted;

        public float Angle => _angle;

        public RotationDial()
        {
            AddToClassList("le-rotation-dial");
            style.width = Size;
            style.height = Size;
            style.alignSelf = Align.Center;

            generateVisualContent += OnGenerateVisualContent;
            RegisterCallback<PointerDownEvent>(OnPointerDown);
            RegisterCallback<PointerMoveEvent>(OnPointerMove);
            RegisterCallback<PointerUpEvent>(OnPointerUp);
        }

        public void SetAngleWithoutNotify(float angle)
        {
            _angle = Normalize(angle);
            MarkDirtyRepaint();
        }

        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button != 0)
                return;

            _pointerId = evt.pointerId;
            this.CapturePointer(evt.pointerId);
            UpdateFromPointer(evt.localPosition);
            evt.StopPropagation();
        }

        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (evt.pointerId != _pointerId)
                return;

            UpdateFromPointer(evt.localPosition);
            evt.StopPropagation();
        }

        private void OnPointerUp(PointerUpEvent evt)
        {
            if (evt.pointerId != _pointerId)
                return;

            if (this.HasPointerCapture(_pointerId))
                this.ReleasePointer(_pointerId);
            _pointerId = -1;

            AngleCommitted?.Invoke(_angle);
            evt.StopPropagation();
        }

        private void UpdateFromPointer(Vector2 local)
        {
            Vector2 center = contentRect.center;
            float dx = local.x - center.x;
            float dy = local.y - center.y;

            // 0° = up, clockwise positive (screen Y points down). Round to whole degrees.
            float angle = Mathf.Atan2(dx, -dy) * Mathf.Rad2Deg;
            _angle = Normalize(Mathf.Round(angle));

            MarkDirtyRepaint();
            AngleChanged?.Invoke(_angle);
        }

        private static float Normalize(float angle)
        {
            angle %= 360f;
            if (angle < 0f)
                angle += 360f;
            return angle;
        }

        private void OnGenerateVisualContent(MeshGenerationContext mgc)
        {
            var rect = contentRect;
            if (rect.width <= 0f || rect.height <= 0f)
                return;

            var p = mgc.painter2D;
            Vector2 center = rect.center;
            float radius = Mathf.Min(rect.width, rect.height) / 2f - 4f;

            // Outer ring.
            p.lineWidth = 2f;
            p.strokeColor = RingColor;
            p.BeginPath();
            p.Arc(center, radius, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            p.Stroke();

            // Handle line from center to the current direction.
            float rad = _angle * Mathf.Deg2Rad;
            Vector2 dir = new(Mathf.Sin(rad), -Mathf.Cos(rad));
            Vector2 tip = center + dir * radius;

            p.strokeColor = HandleColor;
            p.lineWidth = 2f;
            p.BeginPath();
            p.MoveTo(center);
            p.LineTo(tip);
            p.Stroke();

            // Handle knob.
            p.fillColor = HandleColor;
            p.BeginPath();
            p.Arc(tip, 5f, new Angle(0f, AngleUnit.Degree), new Angle(360f, AngleUnit.Degree));
            p.Fill();
        }
    }
}
