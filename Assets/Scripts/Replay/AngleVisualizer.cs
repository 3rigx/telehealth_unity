using System.Collections.Generic;
using UnityEngine;

namespace Assets.Scripts.Replay
{
    public class AngleVisualizer : MonoBehaviour
    {
        // ─── Arc Gauge Dimensions ────────────────────────────────────────────────
        [Header("Arc Gauge Dimensions")]
        public float arcRadius      = 0.12f;
        public float arcLineWidth   = 0.007f;
        public float trackLineWidth = 0.003f;
        public int   arcSegments    = 64;

        // ─── Bone Rays ───────────────────────────────────────────────────────────
        [Header("Bone Rays")]
        public bool  showBoneRays  = true;
        public float boneRayLength = 0.09f;
        public float boneRayWidth  = 0.004f;

        // ─── Rest Angle Needle ───────────────────────────────────────────────────
        [Header("Rest Angle Needle")]
        public bool  showRestNeedle  = true;
        public float needleInnerFrac = 0.70f;
        public float needleOuterFrac = 1.20f;
        public float needleWidth     = 0.005f;

        // ─── Deviation Fill ──────────────────────────────────────────────────────
        [Header("Deviation Fill")]
        [Tooltip("When true the arc spans rest→current instead of 0→current")]
        public bool showDeviationArc = true;

        // ─── Trend Arrow ─────────────────────────────────────────────────────────
        [Header("Trend Arrow")]
        public bool  showTrendArrow       = true;
        [Tooltip("Minimum angular velocity (deg/s) required to draw the arrow")]
        public float trendSensitivity     = 2f;
        public float trendArrowLength     = 0.035f;
        public float trendArrowWidth      = 0.005f;
        public Color trendIncreaseColor   = new Color(1.00f, 0.55f, 0.10f, 0.90f);
        public Color trendDecreaseColor   = new Color(0.30f, 0.75f, 1.00f, 0.90f);

        // ─── Min/Max Markers ─────────────────────────────────────────────────────
        [Header("Min/Max Markers")]
        public bool  showMinMaxMarkers  = true;
        public float markerInnerFrac    = 0.80f;
        public float markerOuterFrac    = 1.15f;
        public float markerWidth        = 0.004f;
        public Color minMarkerColor     = new Color(0.40f, 0.90f, 1.00f, 0.85f);
        public Color maxMarkerColor     = new Color(1.00f, 0.50f, 0.10f, 0.85f);

        // ─── Angular Velocity ────────────────────────────────────────────────────
        [Header("Angular Velocity")]
        public bool  showVelocityPulse     = true;
        [Tooltip("deg/s that maps to maximum width multiplier")]
        public float velocityMaxDegPerSec  = 120f;
        public float velocityWidthMin      = 0.004f;
        public float velocityWidthMax      = 0.016f;

        // ─── Time In Zone ────────────────────────────────────────────────────────
        [Header("Time In Zone")]
        public bool  showTimeInZone       = false;
        [Tooltip("Outer radius of the time-in-zone fill ring, relative to arcRadius")]
        public float timeRingRadiusOffset = 0.025f;
        public float timeRingWidth        = 0.006f;
        [Tooltip("Seconds of zone time that fills the ring completely")]
        public float timeRingMaxSeconds   = 10f;

        // ─── Asymmetry ───────────────────────────────────────────────────────────
        [Header("Asymmetry")]
        public bool  showAsymmetry         = true;
        [Tooltip("Angle difference (degrees) above which the asymmetry line is shown")]
        public float asymmetryThreshold    = 10f;
        [Tooltip("Angle difference at which line becomes fully red")]
        public float asymmetryMaxDegrees   = 40f;
        public float asymmetryLineWidth    = 0.004f;

        // ─── Sparkline Trail ─────────────────────────────────────────────────────
        [Header("Sparkline Trail")]
        public bool  showSparkline        = false;
        [Tooltip("Number of recent angle samples plotted on the trail ring")]
        public int   sparklineHistory     = 40;
        [Tooltip("Radius offset of the sparkline ring beyond arcRadius")]
        public float sparklineRadiusOffset = 0.018f;
        public float sparklineWidth        = 0.003f;
        public Color sparklineColor        = new Color(1.00f, 1.00f, 1.00f, 0.35f);

        // ─── Text Display ────────────────────────────────────────────────────────
        [Header("Text Display")]
        public bool  showAngleText    = true;
        public bool  showJointName    = true;
        public bool  showDeviation    = true;
        public float textOffset       = 0.18f;
        public int   fontSize         = 14;
        public Color textColor        = Color.white;
        public Font  textFont;

        // ─── Colors ──────────────────────────────────────────────────────────────
        [Header("Colors")]
        public Color normalColor   = new Color(0.20f, 0.90f, 0.40f, 1.00f);
        public Color warningColor  = new Color(1.00f, 0.80f, 0.10f, 1.00f);
        public Color criticalColor = new Color(1.00f, 0.25f, 0.25f, 1.00f);
        public Color trackColor    = new Color(1.00f, 1.00f, 1.00f, 0.12f);
        public Color needleColor   = new Color(0.55f, 0.85f, 1.00f, 0.90f);
        public Color boneRayColor  = new Color(1.00f, 1.00f, 1.00f, 0.25f);

        // ─── Thresholds ──────────────────────────────────────────────────────────
        [Header("Thresholds (degrees)")]
        public float warningThreshold  = 30f;
        public float criticalThreshold = 60f;

        // ─── Critical Pulse ──────────────────────────────────────────────────────
        [Header("Critical Pulse")]
        public bool  pulseOnCritical = true;
        public float pulseSpeed      = 4.0f;
        public float pulseMinAlpha   = 0.25f;

        // ─── Angle Selection ─────────────────────────────────────────────────────
        [Header("Angle Selection")]
        public bool enableAngleDisplay = true;
        public bool showLeftElbow      = true;
        public bool showRightElbow     = true;
        public bool showLeftKnee       = true;
        public bool showRightKnee      = true;
        public bool showLeftShoulder   = true;
        public bool showRightShoulder  = true;
        public bool showLeftHip        = true;
        public bool showRightHip       = true;

        // ─────────────────────────────────────────────────────────────────────────
        // Per-joint accumulated statistics (Layer 1 accumulator)
        // ─────────────────────────────────────────────────────────────────────────
        private class JointStats
        {
            public float previousAngle;
            public float angularVelocity;
            public float minAngleSeen;
            public float maxAngleSeen;
            public float timeInWarning;
            public float timeInCritical;
            public readonly Queue<float> sparklineBuffer = new();
            public bool initialized;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // ArcDisplay — one instance per visible joint
        // ─────────────────────────────────────────────────────────────────────────
        private class ArcDisplay
        {
            public GameObject   root;
            public LineRenderer track;
            public LineRenderer arc;
            public LineRenderer needle;
            public LineRenderer boneRay0;
            public LineRenderer boneRay1;
            public LineRenderer trendArrow;
            public LineRenderer minMarker;
            public LineRenderer maxMarker;
            public LineRenderer timeInZoneRing;
            public LineRenderer sparklineTrail;
            public LineRenderer asymmetryLine;
            public TextMesh     angleText;
            public bool         isCritical;
        }

        private readonly Dictionary<string, ArcDisplay>  _displays   = new();
        private readonly Dictionary<string, JointStats>  _stats      = new();
        private Camera   _cam;
        private Material _lineMat;

        // ─────────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ─────────────────────────────────────────────────────────────────────────
        private void Awake()
        {
            _cam     = Camera.main ?? FindFirstObjectByType<Camera>();
            _lineMat = new Material(Shader.Find("Sprites/Default")) { renderQueue = 3001 };

            if (textFont == null)
                textFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        private void Update()
        {
            if (!pulseOnCritical) return;

            float t     = (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f;
            float alpha = Mathf.Lerp(pulseMinAlpha, 1f, t);

            foreach (var d in _displays.Values)
            {
                if (!d.isCritical || d.arc == null) continue;
                Color c = criticalColor;
                c.a = alpha;
                d.arc.startColor = d.arc.endColor = c;
            }
        }

        private void OnDestroy() => ClearAllDisplays();

        // ─────────────────────────────────────────────────────────────────────────
        // Public API
        // ─────────────────────────────────────────────────────────────────────────
        public void UpdateAngleDisplays(JointAngleCalculator.JointAngle[] angles)
        {
            if (!enableAngleDisplay) return;

            foreach (var angle in angles)
            {
                if (ShouldShowAngle(angle.name))
                    UpdateDisplay(EnrichAngle(angle));
                else
                    SetDisplayActive(angle.name, false);
            }

            if (showAsymmetry)
                UpdateAsymmetryLines();
        }

        public void ClearAllDisplays()
        {
            foreach (var d in _displays.Values)
                if (d?.root != null) Destroy(d.root);
            _displays.Clear();
            _stats.Clear();
        }

        public void ToggleAngleDisplay()
        {
            enableAngleDisplay = !enableAngleDisplay;
            if (!enableAngleDisplay) ClearAllDisplays();
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Layer 1 — accumulate per-joint history and enrich the struct
        // ─────────────────────────────────────────────────────────────────────────
        private JointAngleCalculator.JointAngle EnrichAngle(JointAngleCalculator.JointAngle angle)
        {
            if (!_stats.TryGetValue(angle.name, out JointStats s))
            {
                s = new JointStats
                {
                    previousAngle = angle.currentAngle,
                    minAngleSeen  = angle.currentAngle,
                    maxAngleSeen  = angle.currentAngle,
                    initialized   = true
                };
                _stats[angle.name] = s;
            }

            float dt = Time.deltaTime > 0f ? Time.deltaTime : 0.016f;
            angle.previousAngle   = s.previousAngle;
            angle.angularVelocity = (angle.currentAngle - s.previousAngle) / dt;

            s.minAngleSeen = Mathf.Min(s.minAngleSeen, angle.currentAngle);
            s.maxAngleSeen = Mathf.Max(s.maxAngleSeen, angle.currentAngle);
            angle.minAngleSeen = s.minAngleSeen;
            angle.maxAngleSeen = s.maxAngleSeen;

            float dev = Mathf.Abs(angle.deviationFromRest);
            if (dev >= criticalThreshold)
                s.timeInCritical += dt;
            else if (dev >= warningThreshold)
                s.timeInWarning += dt;
            angle.timeInWarning  = s.timeInWarning;
            angle.timeInCritical = s.timeInCritical;

            s.sparklineBuffer.Enqueue(angle.currentAngle);
            while (s.sparklineBuffer.Count > Mathf.Max(2, sparklineHistory))
                s.sparklineBuffer.Dequeue();

            s.previousAngle = angle.currentAngle;
            return angle;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Layer 2/3 — build and update display
        // ─────────────────────────────────────────────────────────────────────────
        private void UpdateDisplay(JointAngleCalculator.JointAngle angle)
        {
            if (!_displays.TryGetValue(angle.name, out ArcDisplay d))
            {
                d = BuildArcDisplay(angle.name);
                _displays[angle.name] = d;
            }

            d.root.transform.position = angle.position;
            if (_cam != null)
                d.root.transform.LookAt(_cam.transform.position);

            float deviation  = angle.deviationFromRest;
            Color gaugeColor = GetColor(deviation);
            d.isCritical     = Mathf.Abs(deviation) >= criticalThreshold;

            // ── Main arc ──────────────────────────────────────────────────────────
            float arcW = arcLineWidth;
            if (showVelocityPulse)
            {
                float velT = Mathf.Clamp01(Mathf.Abs(angle.angularVelocity) / velocityMaxDegPerSec);
                arcW = Mathf.Lerp(velocityWidthMin, velocityWidthMax, velT);
            }

            float arcStart = Mathf.Clamp(showDeviationArc ? angle.restAngle : 0f, 0f, 180f);
            float arcEnd   = Mathf.Clamp(angle.currentAngle, 0f, 180f);
            RebuildArc(d.arc, 180f - arcStart, 180f - arcEnd, arcRadius, arcSegments, gaugeColor, arcW);

            // ── Rest needle ───────────────────────────────────────────────────────
            if (showRestNeedle && d.needle != null)
            {
                float rad   = (180f - angle.restAngle) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                d.needle.SetPosition(0, dir * (arcRadius * needleInnerFrac));
                d.needle.SetPosition(1, dir * (arcRadius * needleOuterFrac));
                d.needle.startColor = d.needle.endColor = needleColor;
            }

            // ── Bone rays ─────────────────────────────────────────────────────────
            if (showBoneRays)
            {
                float midRad = (180f - angle.currentAngle * 0.5f) * Mathf.Deg2Rad;
                SetBoneRay(d.boneRay0,  new Vector3(Mathf.Cos(midRad), Mathf.Sin(midRad), 0f));
                SetBoneRay(d.boneRay1, -new Vector3(Mathf.Cos(midRad), Mathf.Sin(midRad), 0f));
            }

            // ── Text ──────────────────────────────────────────────────────────────
            if (showAngleText && d.angleText != null)
            {
                string line1 = showJointName   ? ShortJointName(angle.name) + "\n" : "";
                string line2 = $"{angle.currentAngle:F0}°";
                string line3 = showDeviation   ? $"\nΔ{angle.deviationFromRest:+0;-0;0}°" : "";
                d.angleText.text  = line1 + line2 + line3;
                d.angleText.color = textColor;
                d.angleText.transform.localPosition = new Vector3(0, textOffset, 0);
            }

            // ── Trend arrow ───────────────────────────────────────────────────────
            if (showTrendArrow && d.trendArrow != null)
            {
                float vel = angle.angularVelocity;
                if (Mathf.Abs(vel) >= trendSensitivity)
                {
                    float   curRad  = (180f - angle.currentAngle) * Mathf.Deg2Rad;
                    Vector3 origin  = new Vector3(Mathf.Cos(curRad), Mathf.Sin(curRad), 0f) * arcRadius;
                    Vector3 tangent = new Vector3(-Mathf.Sin(curRad), Mathf.Cos(curRad), 0f);
                    if (vel < 0f) tangent = -tangent;
                    d.trendArrow.positionCount = 2;
                    d.trendArrow.SetPosition(0, origin);
                    d.trendArrow.SetPosition(1, origin + tangent * trendArrowLength);
                    d.trendArrow.startWidth = d.trendArrow.endWidth = trendArrowWidth;
                    Color ac = vel > 0f ? trendIncreaseColor : trendDecreaseColor;
                    d.trendArrow.startColor = d.trendArrow.endColor = ac;
                    d.trendArrow.gameObject.SetActive(true);
                }
                else
                {
                    d.trendArrow.gameObject.SetActive(false);
                }
            }
            else if (d.trendArrow != null)
            {
                d.trendArrow.gameObject.SetActive(false);
            }

            // ── Min marker ────────────────────────────────────────────────────────
            if (showMinMaxMarkers && d.minMarker != null)
            {
                float rad   = (180f - Mathf.Clamp(angle.minAngleSeen, 0f, 180f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                d.minMarker.SetPosition(0, dir * (arcRadius * markerInnerFrac));
                d.minMarker.SetPosition(1, dir * (arcRadius * markerOuterFrac));
                d.minMarker.startWidth = d.minMarker.endWidth = markerWidth;
                d.minMarker.startColor = d.minMarker.endColor = minMarkerColor;
            }

            // ── Max marker ────────────────────────────────────────────────────────
            if (showMinMaxMarkers && d.maxMarker != null)
            {
                float rad   = (180f - Mathf.Clamp(angle.maxAngleSeen, 0f, 180f)) * Mathf.Deg2Rad;
                Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
                d.maxMarker.SetPosition(0, dir * (arcRadius * markerInnerFrac));
                d.maxMarker.SetPosition(1, dir * (arcRadius * markerOuterFrac));
                d.maxMarker.startWidth = d.maxMarker.endWidth = markerWidth;
                d.maxMarker.startColor = d.maxMarker.endColor = maxMarkerColor;
            }

            // ── Time-in-zone ring ─────────────────────────────────────────────────
            if (showTimeInZone && d.timeInZoneRing != null)
            {
                float totalZoneTime = angle.timeInWarning + angle.timeInCritical;
                float fillFrac      = Mathf.Clamp01(totalZoneTime / Mathf.Max(0.001f, timeRingMaxSeconds));
                float ringRadius    = arcRadius + timeRingRadiusOffset;
                Color ringColor     = angle.timeInCritical > angle.timeInWarning ? criticalColor : warningColor;
                ringColor.a         = 0.55f;

                if (fillFrac > 0.001f)
                {
                    float sweepDeg = fillFrac * 360f;
                    RebuildArc(d.timeInZoneRing, 90f, 90f - sweepDeg, ringRadius, arcSegments, ringColor, timeRingWidth);
                    d.timeInZoneRing.gameObject.SetActive(true);
                }
                else
                {
                    d.timeInZoneRing.gameObject.SetActive(false);
                }
            }

            // ── Sparkline trail ───────────────────────────────────────────────────
            if (showSparkline && d.sparklineTrail != null && _stats.TryGetValue(angle.name, out JointStats st))
            {
                float[] buf   = new float[st.sparklineBuffer.Count];
                st.sparklineBuffer.CopyTo(buf, 0);
                int     count = buf.Length;
                float   sRad  = arcRadius + sparklineRadiusOffset;

                if (count >= 2)
                {
                    d.sparklineTrail.positionCount = count;
                    for (int i = 0; i < count; i++)
                    {
                        float rad = (180f - Mathf.Clamp(buf[i], 0f, 180f)) * Mathf.Deg2Rad;
                        d.sparklineTrail.SetPosition(i,
                            new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * sRad);
                    }
                    Color sc = sparklineColor;
                    d.sparklineTrail.startColor  = new Color(sc.r, sc.g, sc.b, 0.10f);
                    d.sparklineTrail.endColor    = new Color(sc.r, sc.g, sc.b, sc.a);
                    d.sparklineTrail.startWidth  = sparklineWidth * 0.5f;
                    d.sparklineTrail.endWidth    = sparklineWidth;
                    d.sparklineTrail.gameObject.SetActive(true);
                }
                else
                {
                    d.sparklineTrail.gameObject.SetActive(false);
                }
            }

            d.root.SetActive(true);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Asymmetry pass — runs after all joints are updated
        // ─────────────────────────────────────────────────────────────────────────
        private static readonly (string left, string right)[] _asymmetryPairs = new[]
        {
            ("Left Elbow",    "Right Elbow"),
            ("Left Knee",     "Right Knee"),
            ("Left Shoulder", "Right Shoulder"),
            ("Left Hip",      "Right Hip"),
        };

        private void UpdateAsymmetryLines()
        {
            foreach (var (left, right) in _asymmetryPairs)
            {
                bool hasLeft  = _displays.TryGetValue(left,  out ArcDisplay dL) && dL.root.activeSelf;
                bool hasRight = _displays.TryGetValue(right, out ArcDisplay dR) && dR.root.activeSelf;

                if (!hasLeft || !hasRight)
                {
                    HideAsymmetryLine(dL);
                    HideAsymmetryLine(dR);
                    continue;
                }

                float angleL  = _stats.TryGetValue(left,  out JointStats sL) ? sL.previousAngle : 0f;
                float angleR  = _stats.TryGetValue(right, out JointStats sR) ? sR.previousAngle : 0f;
                float diff    = Mathf.Abs(angleL - angleR);

                if (diff < asymmetryThreshold)
                {
                    HideAsymmetryLine(dL);
                    HideAsymmetryLine(dR);
                    continue;
                }

                float t     = Mathf.Clamp01((diff - asymmetryThreshold) /
                              Mathf.Max(0.001f, asymmetryMaxDegrees - asymmetryThreshold));
                Color color = Color.Lerp(normalColor, criticalColor, t);
                color.a     = 0.70f;

                DrawWorldLine(dL.asymmetryLine, dL.root.transform.position, dR.root.transform.position, color);
                DrawWorldLine(dR.asymmetryLine, dR.root.transform.position, dL.root.transform.position, color);
            }
        }

        private void HideAsymmetryLine(ArcDisplay d)
        {
            if (d?.asymmetryLine != null)
                d.asymmetryLine.gameObject.SetActive(false);
        }

        private void DrawWorldLine(LineRenderer lr, Vector3 from, Vector3 to, Color color)
        {
            if (lr == null) return;
            lr.useWorldSpace     = true;
            lr.positionCount     = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = lr.endWidth = asymmetryLineWidth;
            lr.startColor = lr.endColor = color;
            lr.gameObject.SetActive(true);
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Build display objects
        // ─────────────────────────────────────────────────────────────────────────
        private ArcDisplay BuildArcDisplay(string jointName)
        {
            var d  = new ArcDisplay();
            d.root = new GameObject($"ArcGauge_{jointName}");
            d.root.transform.SetParent(transform, worldPositionStays: false);

            d.track = MakeLR("Track", d.root, trackColor, trackLineWidth);
            RebuildArc(d.track, 0f, 360f, arcRadius, arcSegments, trackColor, trackLineWidth);

            d.arc = MakeLR("Arc", d.root, normalColor, arcLineWidth);

            if (showRestNeedle)
            {
                d.needle = MakeLR("Needle", d.root, needleColor, needleWidth);
                d.needle.positionCount = 2;
            }

            if (showBoneRays)
            {
                d.boneRay0 = MakeLR("BoneRay0", d.root, boneRayColor, boneRayWidth);
                d.boneRay0.positionCount = 2;
                d.boneRay1 = MakeLR("BoneRay1", d.root, boneRayColor, boneRayWidth);
                d.boneRay1.positionCount = 2;
            }

            d.trendArrow = MakeLR("TrendArrow", d.root, trendIncreaseColor, trendArrowWidth);
            d.trendArrow.positionCount = 2;
            d.trendArrow.gameObject.SetActive(false);

            d.minMarker = MakeLR("MinMarker", d.root, minMarkerColor, markerWidth);
            d.minMarker.positionCount = 2;

            d.maxMarker = MakeLR("MaxMarker", d.root, maxMarkerColor, markerWidth);
            d.maxMarker.positionCount = 2;

            d.timeInZoneRing = MakeLR("TimeInZoneRing", d.root, warningColor, timeRingWidth);
            d.timeInZoneRing.gameObject.SetActive(false);

            d.sparklineTrail = MakeLR("SparklineTrail", d.root, sparklineColor, sparklineWidth);
            d.sparklineTrail.gameObject.SetActive(false);

            d.asymmetryLine = MakeLR("AsymmetryLine", d.root, normalColor, asymmetryLineWidth);
            d.asymmetryLine.positionCount = 2;
            d.asymmetryLine.useWorldSpace = true;
            d.asymmetryLine.gameObject.SetActive(false);

            if (showAngleText)
                d.angleText = MakeText("AngleText", d.root);

            return d;
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Helpers
        // ─────────────────────────────────────────────────────────────────────────
        private static void RebuildArc(LineRenderer lr, float startDeg, float endDeg,
            float radius, int maxSegments, Color color, float width)
        {
            if (lr == null) return;
            int pts = Mathf.Max(2, Mathf.RoundToInt(maxSegments * Mathf.Abs(endDeg - startDeg) / 360f));
            lr.positionCount = pts;
            lr.startWidth    = lr.endWidth = width;
            lr.startColor    = lr.endColor = color;
            for (int i = 0; i < pts; i++)
            {
                float t   = pts > 1 ? (float)i / (pts - 1) : 0f;
                float rad = Mathf.Lerp(startDeg, endDeg, t) * Mathf.Deg2Rad;
                lr.SetPosition(i, new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f) * radius);
            }
        }

        private void SetBoneRay(LineRenderer lr, Vector3 dir)
        {
            if (lr == null) return;
            lr.SetPosition(0, Vector3.zero);
            lr.SetPosition(1, dir * boneRayLength);
        }

        private TextMesh MakeText(string goName, GameObject parent)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;

            var tm           = go.AddComponent<TextMesh>();
            tm.font          = textFont;
            tm.fontSize      = fontSize;
            tm.color         = textColor;
            tm.anchor        = TextAnchor.MiddleCenter;
            tm.alignment     = TextAlignment.Center;
            tm.characterSize = 0.001f;
            tm.richText      = false;
            return tm;
        }

        private LineRenderer MakeLR(string goName, GameObject parent, Color color, float width)
        {
            var go = new GameObject(goName);
            go.transform.SetParent(parent.transform, worldPositionStays: false);
            go.transform.localPosition = Vector3.zero;

            var lr           = go.AddComponent<LineRenderer>();
            lr.material      = _lineMat;
            lr.startColor    = lr.endColor = color;
            lr.startWidth    = lr.endWidth = width;
            lr.useWorldSpace = false;
            return lr;
        }

        private Color GetColor(float deviation)
        {
            float abs = Mathf.Abs(deviation);
            if (abs >= criticalThreshold) return criticalColor;
            if (abs >= warningThreshold)  return warningColor;
            return normalColor;
        }

        private static string ShortJointName(string name) => name switch
        {
            "Left Elbow"     => "L.Elbow",
            "Right Elbow"    => "R.Elbow",
            "Left Knee"      => "L.Knee",
            "Right Knee"     => "R.Knee",
            "Left Shoulder"  => "L.Shldr",
            "Right Shoulder" => "R.Shldr",
            "Left Hip"       => "L.Hip",
            "Right Hip"      => "R.Hip",
            _                => name
        };

        private bool ShouldShowAngle(string name) => name switch
        {
            "Left Elbow"     => showLeftElbow,
            "Right Elbow"    => showRightElbow,
            "Left Knee"      => showLeftKnee,
            "Right Knee"     => showRightKnee,
            "Left Shoulder"  => showLeftShoulder,
            "Right Shoulder" => showRightShoulder,
            "Left Hip"       => showLeftHip,
            "Right Hip"      => showRightHip,
            _                => true
        };

        private void SetDisplayActive(string name, bool active)
        {
            if (_displays.TryGetValue(name, out var d) && d?.root != null)
                d.root.SetActive(active);
        }
    }
}
