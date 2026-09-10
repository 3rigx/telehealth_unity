using System.Collections.Generic;
using Assets.Scripts.Replay;
using UnityEngine;

namespace Assets.Scripts.UI.Chart
{
    /// <summary>
    /// Plots one angle time-series line per joint using UIGraphRenderer instances.
    /// Attach to a UI panel (RectTransform on a Canvas). Assign linePrefab to the
    /// same prefab used by SkeletonChartController.
    /// </summary>
    public class AngleChartController : MonoBehaviour
    {
        public GameObject linePrefab;

        private readonly Dictionary<string, UIGraphRenderer> _lines = new();
        private int _colorIndex;

        private static readonly Color[] _colors =
        {
            new Color(1.00f, 0.40f, 0.40f), // Left Elbow
            new Color(0.40f, 0.85f, 0.40f), // Right Elbow
            new Color(0.40f, 0.60f, 1.00f), // Left Knee
            new Color(1.00f, 0.85f, 0.20f), // Right Knee
            new Color(1.00f, 0.45f, 0.90f), // Left Shoulder
            new Color(0.40f, 0.90f, 1.00f), // Right Shoulder
            new Color(1.00f, 0.60f, 0.20f), // Left Hip
            new Color(0.75f, 0.40f, 1.00f), // Right Hip
        };

        public int cursor
        {
            set
            {
                foreach (var line in _lines.Values)
                    line.cursor = value;
            }
        }

        public void AddAngleState(JointAngleCalculator.JointAngle[] angles)
        {
            foreach (var angle in angles)
            {
                if (!_lines.TryGetValue(angle.name, out UIGraphRenderer line))
                    line = CreateLine(angle.name);
                line.AddValue(angle.currentAngle);
            }
        }

        public void Clear()
        {
            foreach (var line in _lines.Values)
                if (line != null) Destroy(line.gameObject);
            _lines.Clear();
            _colorIndex = 0;
        }

        private UIGraphRenderer CreateLine(string jointName)
        {
            var go = Instantiate(linePrefab, transform, worldPositionStays: false);
            var lr = go.GetComponent<UIGraphRenderer>();
            if (lr.values == null) lr.values = new List<float>();
            lr.color = _colors[_colorIndex % _colors.Length];
            _colorIndex++;
            _lines[jointName] = lr;
            return lr;
        }
    }
}