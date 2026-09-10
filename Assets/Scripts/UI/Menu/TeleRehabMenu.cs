using System;
using System.Collections;
using System.Collections.Generic;
using Assets.Scripts.Exercises;
using Assets.Scripts.Exercises.ExerciseTypes;
using Assets.Scripts.SaveSystem;
using Assets.Scripts.Util;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Assets.Scripts.UI
{
    /// <summary>
    ///     Self-building modern menu (dark-blue, flat, animated). Add this component to a
    ///     GameObject in the Menu scene (or run Tools > TeleRehab > Set Up Modern Menu) and
    ///     it constructs the whole UI from code at runtime: Home (Exercise / Replay /
    ///     Prediction / Settings / Exit), an integrated session-setup screen (patient
    ///     pick-or-create + sensors + class), and a small FSR-source settings screen.
    ///
    ///     No prefabs, no manual wiring. Replaces the F9 overlay and hides the old menu.
    /// </summary>
    public class TeleRehabMenu : MonoBehaviour
    {
        [Tooltip("Disable other Canvases in this scene so the old menu doesn't show through.")]
        public bool hideOtherCanvases = true;

        public string exerciseSceneName = "CompleteExercise";
        public string replaySceneName = "Replay";
        public string predictionSceneName = "Prediction";

        // ---- palette (flat dark blue, no gradients) ----
        private static readonly Color Bg        = C(11, 22, 38);
        private static readonly Color Panel     = C(17, 33, 54);
        private static readonly Color Card      = C(22, 45, 72);
        private static readonly Color CardHi    = C(31, 70, 110);
        private static readonly Color Accent     = C(76, 141, 255);
        private static readonly Color AccentDim = C(40, 78, 140);
        private static readonly Color Ink        = C(234, 242, 251);
        private static readonly Color Muted      = C(143, 166, 191);
        private static readonly Color Line       = C(36, 60, 90);
        private static readonly Color Danger     = C(226, 85, 99);

        private RectTransform _home, _setup, _settings, _current;
        private SessionMode _mode = SessionMode.Exercise;

        // setup state
        private string _patientId = "";
        private TMP_InputField _patientInput;
        private RectTransform _patientList;
        private bool _zed = true, _fsr = true, _eeg;
        private int _classIndex = 1; // 0 Idle, 1 Motion, 2 MotionCognitive
        private TMP_Text _setupTitle, _message;
        private readonly List<Action> _refreshToggles = new();

        private static Color C(int r, int g, int b) => new Color32((byte)r, (byte)g, (byte)b, 255);
        private static TMP_FontAsset Font =>
            TMP_Settings.defaultFontAsset != null
                ? TMP_Settings.defaultFontAsset
                : Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");

        private static Sprite _round;

        /// <summary>
        ///     Procedurally generated 9-sliced rounded-rectangle sprite (the builtin UI
        ///     sprites are not reachable via Resources at runtime).
        /// </summary>
        private static Sprite Round()
        {
            if (_round != null) return _round;

            const int size = 64, radius = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp
            };

            var px = new Color32[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                // Distance outside the inner (corner-inset) rect -> rounded corner alpha
                float dx = Mathf.Max(radius - x, x - (size - 1 - radius), 0);
                float dy = Mathf.Max(radius - y, y - (size - 1 - radius), 0);
                var d = Mathf.Sqrt(dx * dx + dy * dy);
                var a = d <= radius - 1f ? (byte)255
                    : d >= radius + 1f ? (byte)0
                    : (byte)Mathf.RoundToInt(255f * (1f - (d - (radius - 1f)) / 2f));
                px[y * size + x] = new Color32(255, 255, 255, a);
            }

            tex.SetPixels32(px);
            tex.Apply(false, true);

            _round = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect,
                new Vector4(radius + 2, radius + 2, radius + 2, radius + 2));
            _round.hideFlags = HideFlags.HideAndDontSave;
            return _round;
        }

        private void Awake()
        {
            if (hideOtherCanvases)
                foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                    if (c.transform.root != transform) c.enabled = false;

            EnsureEventSystem();
            var root = BuildCanvas();
            _home = BuildHome(root);
            _setup = BuildSetup(root);
            _settings = BuildSettings(root);

            _setup.gameObject.SetActive(false);
            _settings.gameObject.SetActive(false);
            _current = _home;
            StartCoroutine(Intro(_home));
        }

        // ============================================================ scaffolding

        private static void EnsureEventSystem()
        {
            var existing = FindFirstObjectByType<EventSystem>();
            if (existing == null)
            {
                var es = new GameObject("EventSystem");
                es.AddComponent<EventSystem>();
                es.AddComponent<StandaloneInputModule>();
                return;
            }

            // An EventSystem can exist but be inert: component disabled, or no enabled
            // input module attached. Repair instead of trusting it.
            if (!existing.enabled) existing.enabled = true;
            var module = existing.GetComponent<BaseInputModule>();
            if (module == null)
                existing.gameObject.AddComponent<StandaloneInputModule>();
            else if (!module.enabled)
                module.enabled = true;
        }

        /// <summary>
        ///     Temporary diagnostic: on every mouse press, log what the UI raycast sees so
        ///     a dead EventSystem / blocked raycast is identifiable from the Console.
        /// </summary>
        private void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            var es = EventSystem.current;
            if (es == null)
            {
                Debug.LogWarning("[MenuDiag] No EventSystem.current — UI clicks impossible. Repairing.");
                EnsureEventSystem();
                return;
            }

            var data = new PointerEventData(es) { position = Input.mousePosition };
            var hits = new List<RaycastResult>();
            es.RaycastAll(data, hits);
            Debug.Log($"[MenuDiag] module={(es.currentInputModule != null ? es.currentInputModule.GetType().Name : "NULL")} " +
                      $"hits={hits.Count} top={(hits.Count > 0 ? hits[0].gameObject.name : "none")}");
        }

        private RectTransform BuildCanvas()
        {
            var go = new GameObject("TeleRehabMenuCanvas");
            go.transform.SetParent(transform, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();

            var bg = NewImage("BG", go.transform, Bg);
            Stretch(bg.rectTransform);
            return go.GetComponent<RectTransform>();
        }

        // ============================================================ HOME

        private RectTransform BuildHome(RectTransform root)
        {
            var screen = NewScreen("Home", root);

            // top bar
            var brandDot = NewImage("Dot", screen, Accent);
            Anchor(brandDot.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(70, -64), new Vector2(18, 18));
            brandDot.sprite = Round(); brandDot.type = Image.Type.Sliced;
            var title = NewText("Brand", screen, "TeleRehab", 34, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(230, -56), new Vector2(360, 48));
            var sub = NewText("Sub", screen, "Capture · Replay · Analyse", 16, Muted, TextAlignmentOptions.Left);
            Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(232, -92), new Vector2(420, 26));

            // exit (top-right)
            var exit = NewButton("Exit", screen, "X", 22, Ink, Panel, () => Quit());
            Anchor(((RectTransform)exit.transform), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-64, -64), new Vector2(48, 48));

            // card grid
            var grid = new GameObject("Grid", typeof(RectTransform)).GetComponent<RectTransform>();
            grid.SetParent(screen, false);
            Anchor(grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(880, 470));
            var gl = grid.gameObject.AddComponent<GridLayoutGroup>();
            gl.cellSize = new Vector2(420, 215);
            gl.spacing = new Vector2(40, 40);
            gl.childAlignment = TextAnchor.MiddleCenter;
            gl.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            gl.constraintCount = 2;

            var cards = new[]
            {
                Card3("Exercise", "Run a capture session", Accent, () => OpenSetup(SessionMode.Exercise)),
                Card3("Replay", "Review & analyse a session", C(46, 122, 90), () => LoadScene(replaySceneName)),
                Card3("Prediction", "Live model (coming soon)", C(120, 92, 198), () => OpenSetup(SessionMode.Prediction)),
                Card3("Settings", "Sensors & connections", C(90, 104, 124), () => Show(_settings)),
            };
            foreach (var c in cards) c.SetParent(grid, false);

            return screen;
        }

        private RectTransform Card3(string title, string desc, Color tint, Action onClick)
        {
            var card = NewImage("Card_" + title, null, Card);
            card.sprite = Round(); card.type = Image.Type.Sliced;
            var rt = card.rectTransform;
            AddShadow(card.gameObject);

            var bar = NewImage("Bar", rt, tint);
            bar.sprite = Round(); bar.type = Image.Type.Sliced;
            Anchor(bar.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(8, 0), new Vector2(6, -28));
            bar.rectTransform.anchorMin = new Vector2(0, 0); bar.rectTransform.anchorMax = new Vector2(0, 1);
            bar.rectTransform.offsetMin = new Vector2(18, 22); bar.rectTransform.offsetMax = new Vector2(24, -22);

            var t = NewText("T", rt, title, 30, Ink, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(40, -36), Vector2.zero);
            t.rectTransform.offsetMin = new Vector2(44, -80); t.rectTransform.offsetMax = new Vector2(-24, -28);
            var d = NewText("D", rt, desc, 17, Muted, TextAlignmentOptions.TopLeft);
            d.rectTransform.anchorMin = new Vector2(0, 0); d.rectTransform.anchorMax = new Vector2(1, 1);
            d.rectTransform.offsetMin = new Vector2(44, 24); d.rectTransform.offsetMax = new Vector2(-24, -86);

            var btn = card.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick());
            var hov = card.gameObject.AddComponent<HoverFx>();
            hov.Init(card, Card, CardHi, tint);
            return rt;
        }

        // ============================================================ SETUP

        private RectTransform BuildSetup(RectTransform root)
        {
            var screen = NewScreen("Setup", root);
            var panel = NewImage("Panel", screen, Panel);
            panel.sprite = Round(); panel.type = Image.Type.Sliced;
            Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(940, 760));
            AddShadow(panel.gameObject);
            var p = panel.rectTransform;

            _setupTitle = NewText("Title", p, "Exercise — Setup", 28, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
            Anchor(_setupTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -52), Vector2.zero);
            _setupTitle.rectTransform.offsetMin = new Vector2(40, -86); _setupTitle.rectTransform.offsetMax = new Vector2(-40, -34);

            // Patient
            Label(p, "PATIENT ID", -110);
            _patientInput = NewInput("PatientInput", p, "Type a new patient ID…", s => _patientId = s);
            Anchor(_patientInput.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            _patientInput.GetComponent<RectTransform>().offsetMin = new Vector2(40, -200);
            _patientInput.GetComponent<RectTransform>().offsetMax = new Vector2(-40, -140);

            var listBg = NewImage("ListBg", p, Card);
            listBg.sprite = Round(); listBg.type = Image.Type.Sliced;
            var lrt = listBg.rectTransform;
            lrt.anchorMin = new Vector2(0, 1); lrt.anchorMax = new Vector2(1, 1);
            lrt.offsetMin = new Vector2(40, -300); lrt.offsetMax = new Vector2(-40, -212);
            _patientList = BuildHList(lrt);

            // Sensors
            Label(p, "SENSORS", -322);
            var sensorsRow = Row(p, -392, 56);
            AddPill(sensorsRow, "ZED", () => _zed, v => _zed = v);
            AddPill(sensorsRow, "FSR", () => _fsr, v => _fsr = v);
            AddPill(sensorsRow, "EEG  (M2)", () => _eeg, v => _eeg = v);

            // Class
            Label(p, "EXERCISE CLASS", -448);
            var classRow = Row(p, -518, 60);
            AddSegment(classRow, "Idle", 0);
            AddSegment(classRow, "Motion", 1);
            AddSegment(classRow, "Motion + Cognitive", 2);

            // message
            _message = NewText("Msg", p, "", 16, Danger, TextAlignmentOptions.Left);
            Anchor(_message.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 120), Vector2.zero);
            _message.rectTransform.offsetMin = new Vector2(40, 110); _message.rectTransform.offsetMax = new Vector2(-40, 150);

            // buttons
            var back = NewButton("Back", p, "Back", 20, Ink, Card, () => Show(_home));
            Anchor(((RectTransform)back.transform), new Vector2(0, 0), new Vector2(0, 0), new Vector2(150, 60), new Vector2(200, 60));
            var start = NewButton("Start", p, "Start  »", 22, Ink, Accent, StartSession);
            Anchor(((RectTransform)start.transform), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-200, 60), new Vector2(300, 64));

            return screen;
        }

        private RectTransform BuildSettings(RectTransform root)
        {
            var screen = NewScreen("Settings", root);
            var panel = NewImage("Panel", screen, Panel);
            panel.sprite = Round(); panel.type = Image.Type.Sliced;
            Anchor(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(820, 460));
            AddShadow(panel.gameObject);
            var p = panel.rectTransform;

            var title = NewText("Title", p, "Settings", 28, Ink, TextAlignmentOptions.Left, FontStyles.Bold);
            Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            title.rectTransform.offsetMin = new Vector2(40, -86); title.rectTransform.offsetMax = new Vector2(-40, -34);

            Label(p, "FSR CONNECTION", -120);
            var row = Row(p, -190, 60);
            var cur = PlayerPrefs.GetString("FSRConntype", "Mock");
            foreach (var opt in new[] { "Mock", "USB", "WebSocket" })
            {
                var o = opt;
                AddChoice(row, o, () => PlayerPrefs.GetString("FSRConntype", "Mock") == o, () =>
                {
                    PlayerPrefs.SetString("FSRConntype", o);
                    PlayerPrefs.Save();
                });
            }

            var hint = NewText("Hint", p, "Use Mock to capture without FSR hardware.", 15, Muted, TextAlignmentOptions.Left);
            Anchor(hint.rectTransform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            hint.rectTransform.offsetMin = new Vector2(40, -300); hint.rectTransform.offsetMax = new Vector2(-40, -260);

            var back = NewButton("Back", p, "Back", 20, Ink, Card, () => Show(_home));
            Anchor(((RectTransform)back.transform), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 60), new Vector2(220, 60));
            return screen;
        }

        // ============================================================ navigation / logic

        private void OpenSetup(SessionMode mode)
        {
            _mode = mode;
            if (_setupTitle != null)
                _setupTitle.text = mode == SessionMode.Prediction ? "Prediction — Setup" : "Exercise — Setup";
            if (_message != null) _message.text = "";
            RefreshPatients();
            Show(_setup);
        }

        private void RefreshPatients()
        {
            if (_patientList == null) return;
            for (var i = _patientList.childCount - 1; i >= 0; i--) Destroy(_patientList.GetChild(i).gameObject);
            foreach (var pId in SessionPaths.ListPatients())
            {
                var id = pId;
                var b = NewButton("p_" + id, _patientList, id, 16, Ink, Card, () =>
                {
                    _patientId = id;
                    if (_patientInput != null) _patientInput.text = id;
                });
                var le = b.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = Mathf.Max(90, id.Length * 12 + 36);
                le.minWidth = 90;
            }
        }

        private void StartSession()
        {
            if (string.IsNullOrWhiteSpace(_patientId))
            {
                Msg("Enter or pick a patient ID.");
                return;
            }

            var patientId = SessionPaths.SanitizePatientId(_patientId);
            var cls = (ExerciseClass)_classIndex;
            var scene = _mode == SessionMode.Prediction ? predictionSceneName : exerciseSceneName;
            if (!Application.CanStreamedLevelBeLoaded(scene))
            {
                Msg(_mode == SessionMode.Prediction
                    ? "Prediction scene isn't built yet."
                    : $"Scene '{scene}' is not in Build Settings.");
                return;
            }

            var startUtc = DateTime.UtcNow;
            SessionContext.PatientId = patientId;
            SessionContext.ExerciseClass = cls;
            SessionContext.Mode = _mode;
            SessionContext.ZedEnabled = _zed;
            SessionContext.FsrEnabled = _fsr;
            SessionContext.EegEnabled = _eeg;
            SessionContext.TrialNumber = SessionPaths.NextTrialNumber(patientId, cls.ToToken());
            SessionContext.SessionFolder = SessionPaths.CreateSessionFolder(patientId, cls.ToToken(), startUtc, out var sid);
            SessionContext.SessionId = sid;
            SessionContext.IsConfigured = true;

            var md = new ExerciseMetadata(0, "", cls.ToDisplay(), 0.1f, 0);
            md.patientData["patientId"] = patientId;
            md.patientData["class"] = cls.ToToken();
            md.patientData["mode"] = _mode.ToString();

            ExerciseProvider.isRemote = false;
            ExerciseProvider.exercise = new SandboxExercise(md);

            Debug.Log($"[TeleRehabMenu] Start {_mode}/{cls.ToToken()} for {patientId} -> {SessionContext.SessionFolder}");
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }

        private void Msg(string m) { if (_message != null) _message.text = m; }
        private void LoadScene(string s)
        {
            if (Application.CanStreamedLevelBeLoaded(s)) SceneManager.LoadScene(s);
            else Debug.LogWarning($"[TeleRehabMenu] Scene '{s}' not in Build Settings.");
        }
        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ============================================================ screen transitions

        private void Show(RectTransform target)
        {
            if (target == _current) return;
            if (_current != null) StartCoroutine(Out(_current));
            StartCoroutine(In(target));
            _current = target;
        }

        private IEnumerator Intro(RectTransform s) { yield return In(s); }

        private IEnumerator In(RectTransform s)
        {
            s.gameObject.SetActive(true);
            var cg = Cg(s);
            float t = 0; const float d = 0.22f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                var k = Ease(t / d);
                cg.alpha = k;
                s.anchoredPosition = new Vector2(Mathf.Lerp(40, 0, k), 0);
                yield return null;
            }
            cg.alpha = 1; s.anchoredPosition = Vector2.zero;
        }

        private IEnumerator Out(RectTransform s)
        {
            var cg = Cg(s);
            float t = 0; const float d = 0.16f;
            while (t < d)
            {
                t += Time.unscaledDeltaTime;
                var k = Ease(t / d);
                cg.alpha = 1 - k;
                s.anchoredPosition = new Vector2(Mathf.Lerp(0, -40, k), 0);
                yield return null;
            }
            cg.alpha = 0; s.gameObject.SetActive(false);
        }

        private static CanvasGroup Cg(Component c) =>
            c.GetComponent<CanvasGroup>() ? c.GetComponent<CanvasGroup>() : c.gameObject.AddComponent<CanvasGroup>();
        private static float Ease(float x) { x = Mathf.Clamp01(x); return 1 - Mathf.Pow(1 - x, 3); }

        // ============================================================ widget helpers

        private RectTransform NewScreen(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Stretch(rt);
            return rt;
        }

        private static Image NewImage(string name, Transform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            if (parent != null) go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static TMP_Text NewText(string name, Transform parent, string text, float size, Color color,
            TextAlignmentOptions align, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = Font; t.text = text; t.fontSize = size; t.color = color;
            t.alignment = align; t.fontStyle = style; t.richText = true;
            Stretch(t.rectTransform);
            return t;
        }

        private Button NewButton(string name, Transform parent, string label, float size, Color textColor, Color bg, Action onClick)
        {
            var img = NewImage(name, parent, bg);
            img.sprite = Round(); img.type = Image.Type.Sliced;
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => onClick());
            var t = NewText("Label", img.transform, label, size, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(t.rectTransform);
            var hov = img.gameObject.AddComponent<HoverFx>();
            hov.Init(img, bg, Lighten(bg), Accent);
            return btn;
        }

        private TMP_InputField NewInput(string name, Transform parent, string placeholder, Action<string> onChange)
        {
            var bg = NewImage(name, parent, Card);
            bg.sprite = Round(); bg.type = Image.Type.Sliced;
            var input = bg.gameObject.AddComponent<TMP_InputField>();

            var area = new GameObject("TextArea", typeof(RectTransform), typeof(RectMask2D)).GetComponent<RectTransform>();
            area.SetParent(bg.transform, false);
            area.anchorMin = Vector2.zero; area.anchorMax = Vector2.one;
            area.offsetMin = new Vector2(20, 8); area.offsetMax = new Vector2(-20, -8);

            var ph = NewText("Placeholder", area, placeholder, 20, Muted, TextAlignmentOptions.Left);
            Stretch(ph.rectTransform);
            var txt = NewText("Text", area, "", 20, Ink, TextAlignmentOptions.Left);
            Stretch(txt.rectTransform);

            input.textViewport = area;
            input.textComponent = txt;
            input.placeholder = ph;
            input.onValueChanged.AddListener(v => onChange(v));
            input.text = "";
            return input;
        }

        private RectTransform BuildHList(RectTransform parent)
        {
            var content = new GameObject("Patients", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(parent, false);
            Stretch(content);
            content.offsetMin = new Vector2(12, 8); content.offsetMax = new Vector2(-12, -8);
            var hl = content.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.childAlignment = TextAnchor.MiddleLeft;
            hl.spacing = 10; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true;
            hl.childControlWidth = true; hl.childControlHeight = true;
            return content;
        }

        private void Label(Transform parent, string text, float y)
        {
            var t = NewText("L_" + text, parent, text, 14, Muted, TextAlignmentOptions.Left, FontStyles.Bold);
            Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), Vector2.zero, Vector2.zero);
            t.rectTransform.offsetMin = new Vector2(42, y - 24); t.rectTransform.offsetMax = new Vector2(-40, y);
            t.characterSpacing = 6;
        }

        private RectTransform Row(Transform parent, float y, float h)
        {
            var row = new GameObject("Row", typeof(RectTransform)).GetComponent<RectTransform>();
            row.SetParent(parent, false);
            row.anchorMin = new Vector2(0, 1); row.anchorMax = new Vector2(1, 1);
            row.offsetMin = new Vector2(40, y - h); row.offsetMax = new Vector2(-40, y);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 14; hl.childForceExpandWidth = true; hl.childForceExpandHeight = true;
            hl.childControlWidth = true; hl.childControlHeight = true;
            return row;
        }

        private void AddPill(Transform row, string label, Func<bool> get, Action<bool> set)
        {
            var img = NewImage("Pill_" + label, row, get() ? AccentDim : Card);
            img.sprite = Round(); img.type = Image.Type.Sliced;
            var t = NewText("L", img.transform, label, 18, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(t.rectTransform);
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            Action refresh = () => img.color = get() ? Accent : Card;
            refresh();
            btn.onClick.AddListener(() => { set(!get()); refresh(); });
        }

        private void AddSegment(Transform row, string label, int index)
        {
            var img = NewImage("Seg_" + label, row, _classIndex == index ? Accent : Card);
            img.sprite = Round(); img.type = Image.Type.Sliced;
            var t = NewText("L", img.transform, label, 18, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(t.rectTransform);
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            Action refresh = () => img.color = _classIndex == index ? Accent : Card;
            _refreshToggles.Add(refresh);
            btn.onClick.AddListener(() =>
            {
                _classIndex = index;
                foreach (var r in _refreshToggles) r();
            });
        }

        private void AddChoice(Transform row, string label, Func<bool> get, Action onPick)
        {
            var img = NewImage("Ch_" + label, row, get() ? Accent : Card);
            img.sprite = Round(); img.type = Image.Type.Sliced;
            var t = NewText("L", img.transform, label, 18, Ink, TextAlignmentOptions.Center, FontStyles.Bold);
            Stretch(t.rectTransform);
            var btn = img.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                onPick();
                foreach (Transform sib in row) sib.GetComponent<Image>().color = Card;
                img.color = Accent;
            });
        }

        // ============================================================ layout utils

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
        }

        private static void Anchor(RectTransform rt, Vector2 aMin, Vector2 aMax, Vector2 anchoredPos, Vector2 size)
        {
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = new Vector2(0.5f, 0.5f);
            if (size != Vector2.zero) rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;
        }

        private static void AddShadow(GameObject go)
        {
            var s = go.AddComponent<Shadow>();
            s.effectColor = new Color(0, 0, 0, 0.45f);
            s.effectDistance = new Vector2(0, -5);
        }

        private static Color Lighten(Color c) => Color.Lerp(c, Color.white, 0.08f);

        // ============================================================ hover effect

        private class HoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
        {
            private Image _img; private Color _normal, _hover, _accent; private RectTransform _rt; private Coroutine _co;
            public void Init(Image img, Color normal, Color hover, Color accent)
            {
                _img = img; _normal = normal; _hover = hover; _accent = accent; _rt = img.rectTransform;
            }
            public void OnPointerEnter(PointerEventData e) => Animate(_hover, 1.03f);
            public void OnPointerExit(PointerEventData e) => Animate(_normal, 1f);
            public void OnPointerDown(PointerEventData e) => Animate(_accent, 0.98f);
            public void OnPointerUp(PointerEventData e) => Animate(_hover, 1.03f);
            private void Animate(Color target, float scale)
            {
                if (_co != null) StopCoroutine(_co);
                _co = StartCoroutine(Run(target, scale));
            }
            private IEnumerator Run(Color target, float scale)
            {
                Color start = _img.color; Vector3 s0 = _rt.localScale; var s1 = Vector3.one * scale;
                float t = 0; const float d = 0.12f;
                while (t < d)
                {
                    t += Time.unscaledDeltaTime; var k = Mathf.Clamp01(t / d);
                    _img.color = Color.Lerp(start, target, k);
                    _rt.localScale = Vector3.Lerp(s0, s1, k);
                    yield return null;
                }
                _img.color = target; _rt.localScale = s1;
            }
        }
    }
}
