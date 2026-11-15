// Phase 1 driving HUD, built in code with uGUI: speed, gear, rpm, touches, messages,
// and the touch controls (steering wheel or arrows, gas and brake pedals, gear buttons).
// The real HUD gets proper art in Phase 8; the layout follows the web game.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

namespace Curbline
{
    public class DriveHud : MonoBehaviour
    {
        public CarController Car;
        public CarInput Input;
        public Func<string> Title = () => "";

        static readonly Color Panel = new Color(0.05f, 0.07f, 0.09f, 0.72f);
        static readonly Color Amber = new Color(1f, 0.69f, 0f);
        static readonly Color Ink = new Color(0.93f, 0.95f, 0.96f);

        Font font;
        Text speedText, unitText, titleText, statsText, toastText, keysText;
        Image rpmFill;
        RectTransform wheel, touchRoot, arrows;
        Image toastBg;
        readonly Image[] gearBtns = new Image[4];
        Image engineBtn, driftBtn;
        float toastT;
        bool touchUi;
        bool useArrows;

        public void Build(CarController car, CarInput input)
        {
            Car = car; Input = input;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            EnsureEventSystem();

            var canvasGo = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.6f;
            var root = (RectTransform)canvasGo.transform;
            var safe = MakeRect("Safe", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.gameObject.AddComponent<SafeArea>();

            // top left: what you are driving
            var info = Box("Info", safe, new Vector2(0, 1), new Vector2(24, -24), new Vector2(520, 120));
            titleText = Label(info, "", 30, TextAnchor.UpperLeft, new Vector2(18, -12), new Vector2(490, 40), FontStyle.Bold);
            statsText = Label(info, "", 22, TextAnchor.UpperLeft, new Vector2(18, -56), new Vector2(490, 60));

            // top right: buttons
            float bx = -24;
            foreach (var (label, act) in new (string, Action)[] {
                ("Reset", () => Input.RequestReset()), ("Car >", () => Input.RequestNextCar()), ("< Car", () => Input.RequestPrevCar()),
                ("Lights", () => Input.RequestLights()), ("Camera", () => Input.RequestCamera()) })
            {
                MakeButton(safe, label, new Vector2(1, 1), new Vector2(bx, -24), new Vector2(130, 64), act);
                bx -= 142;
            }

            // bottom center: speed, gear, rpm
            var dash = Box("Dash", safe, new Vector2(0.5f, 0), new Vector2(0, 24), new Vector2(300, 150));
            speedText = Label(dash, "0", 72, TextAnchor.MiddleCenter, new Vector2(0, -6), new Vector2(300, 84), FontStyle.Bold, centered: true);
            unitText = Label(dash, "km/h", 22, TextAnchor.MiddleCenter, new Vector2(0, -88), new Vector2(300, 28), centered: true);
            var rpmBg = Img(dash, new Color(1, 1, 1, 0.12f), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(240, 10));
            rpmFill = Img(rpmBg, Amber, new Vector2(0, 0.5f), Vector2.zero, new Vector2(240, 10));
            rpmFill.rectTransform.pivot = new Vector2(0, 0.5f);

            // gear selector, to the left of the pedals
            var gears = Box("Gears", safe, new Vector2(1, 0), new Vector2(-440, 24), new Vector2(96, 330));
            string g = "PRND";
            for (int i = 0; i < 4; i++)
            {
                char gc = g[i];
                var b = MakeButton(gears, gc.ToString(), new Vector2(0.5f, 1), new Vector2(0, -10 - i * 78), new Vector2(76, 70), () => Input.RequestGear(gc), 34);
                gearBtns[i] = b;
            }
            engineBtn = MakeButton(safe, "ENGINE", new Vector2(1, 0), new Vector2(-440, 370), new Vector2(96, 70), () => Input.RequestEngine(), 18);
            driftBtn = MakeButton(safe, "DRIFT", new Vector2(1, 0), new Vector2(-440, 452), new Vector2(96, 70), () => Input.RequestDrift(), 18);

            // touch controls
            touchRoot = MakeRect("Touch", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var gas = Pedal(touchRoot, "GAS", new Vector2(1, 0), new Vector2(-24, 24), new Vector2(150, 260), on => Input.TouchThrottle = on ? 1 : 0);
            Pedal(touchRoot, "BRAKE", new Vector2(1, 0), new Vector2(-190, 24), new Vector2(150, 190), on => Input.TouchBrake = on ? 1 : 0);
            wheel = WheelControl(touchRoot);
            arrows = MakeRect("Arrows", touchRoot, new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 24), new Vector2(360, 180));
            arrows.pivot = Vector2.zero;
            Pedal(arrows, "<", new Vector2(0, 0), Vector2.zero, new Vector2(170, 170), on => Input.TouchSteerDir = on ? -1 : (Input.TouchSteerDir == -1 ? 0 : Input.TouchSteerDir));
            Pedal(arrows, ">", new Vector2(0, 0), new Vector2(190, 0), new Vector2(170, 170), on => Input.TouchSteerDir = on ? 1 : (Input.TouchSteerDir == 1 ? 0 : Input.TouchSteerDir));
            arrows.gameObject.SetActive(false);

            // messages and the key list
            toastBg = Img(safe, Panel, new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 64));
            toastText = Label(toastBg.rectTransform, "", 26, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(900, 64), centered: true);
            toastBg.gameObject.SetActive(false);
            keysText = Label(safe, "W/S gas & brake · A/D steer · I engine · F/R/N/P gears · X drift (Shift hold) · C camera · L lights · , . change car · Backspace reset · T touch controls",
                18, TextAnchor.LowerLeft, new Vector2(24, 190), new Vector2(1100, 30), anchor: new Vector2(0, 0));

            touchUi = Application.isMobilePlatform || HasTouchscreen();
            ApplyTouch();
            Input.TouchUiToggled += () => { touchUi = !touchUi; ApplyTouch(); };
            Car.Message += Flash;
        }

        public void UseArrows(bool on) { useArrows = on; ApplyTouch(); }

        void ApplyTouch()
        {
            touchRoot.gameObject.SetActive(touchUi);
            wheel.gameObject.SetActive(!useArrows);
            arrows.gameObject.SetActive(useArrows);
            keysText.gameObject.SetActive(!touchUi);
        }

        static bool HasTouchscreen()
        {
#if ENABLE_INPUT_SYSTEM
            return Touchscreen.current != null;
#else
            return UnityEngine.Input.touchSupported;
#endif
        }

        public void Flash(string msg)
        {
            toastText.text = msg;
            toastBg.gameObject.SetActive(true);
            toastT = 2.6f;
        }

        void Update()
        {
            if (Car == null || Car.Spec == null) return;
            speedText.text = Mathf.RoundToInt(Mathf.Abs(Car.Speed) * 3.6f).ToString();
            rpmFill.rectTransform.sizeDelta = new Vector2(240 * Car.Rpm, 10);
            titleText.text = Car.Spec.Name + "  <size=20>" + Car.Spec.ClassLabel + "</size>";
            statsText.text = Title() + "\nTouches: " + Car.Hits + (Car.DriftMode ? "   ·   Drift mode" : "");
            string g = "PRND";
            for (int i = 0; i < 4; i++) gearBtns[i].color = Car.Gear == g[i] ? Amber : Panel;
            engineBtn.color = Car.Engine ? new Color(0.24f, 0.81f, 0.56f) : Color.Lerp(Panel, Amber, 0.5f + 0.5f * Mathf.Sin(Time.time * 6));
            driftBtn.color = Car.DriftMode ? Amber : Panel;
            if (wheel != null) wheel.localRotation = Quaternion.Euler(0, 0, -Input.WheelRot * Mathf.Rad2Deg);
            if (toastT > 0) { toastT -= Time.deltaTime; if (toastT <= 0) toastBg.gameObject.SetActive(false); }
        }

        // ---------- building blocks ----------
        static void EnsureEventSystem()
        {
            if (EventSystem.current != null || FindAnyObjectByType<EventSystem>() != null) return;
#if ENABLE_INPUT_SYSTEM
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
#else
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
#endif
        }

        static RectTransform MakeRect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rt = (RectTransform)go.transform;
            rt.SetParent(parent, false);
            rt.anchorMin = aMin; rt.anchorMax = aMax; rt.pivot = aMin;
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        static Image Img(Transform parent, Color c, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var rt = MakeRect("Image", parent, anchor, anchor, pos, size);
            var img = rt.gameObject.AddComponent<Image>();
            img.color = c;
            return img;
        }

        RectTransform Box(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            var img = Img(parent, Panel, anchor, pos, size);
            img.name = name;
            img.raycastTarget = false;
            return img.rectTransform;
        }

        Text Label(Transform parent, string text, int size, TextAnchor align, Vector2 pos, Vector2 box, FontStyle style = FontStyle.Normal, bool centered = false, Vector2? anchor = null)
        {
            var a = anchor ?? (centered ? new Vector2(0.5f, 1) : new Vector2(0, 1));
            var rt = MakeRect("Text", parent, a, a, pos, box);
            if (centered) rt.pivot = new Vector2(0.5f, 1);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = size; t.alignment = align; t.color = Ink; t.fontStyle = style; t.text = text;
            t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        Image MakeButton(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action onClick, int fontSize = 22)
        {
            var img = Img(parent, Panel, anchor, pos, size);
            img.name = "Button " + text;
            if (anchor.x == 0.5f) img.rectTransform.pivot = new Vector2(0.5f, anchor.y);
            var b = img.gameObject.AddComponent<Button>();
            b.onClick.AddListener(() => onClick());
            var t = Label(img.rectTransform, text, fontSize, TextAnchor.MiddleCenter, Vector2.zero, size, FontStyle.Bold, anchor: new Vector2(0.5f, 0.5f));
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        Image Pedal(Transform parent, string text, Vector2 anchor, Vector2 pos, Vector2 size, Action<bool> held)
        {
            var img = Img(parent, Panel, anchor, pos, size);
            img.name = "Pedal " + text;
            var h = img.gameObject.AddComponent<HoldButton>();
            h.Changed = on => { held(on); img.color = on ? Amber : Panel; };
            var t = Label(img.rectTransform, text, 28, TextAnchor.MiddleCenter, Vector2.zero, size, FontStyle.Bold, anchor: new Vector2(0.5f, 0.5f));
            t.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            return img;
        }

        RectTransform WheelControl(Transform parent)
        {
            var rt = MakeRect("Wheel", parent, Vector2.zero, Vector2.zero, new Vector2(40 + 170, 40 + 170), new Vector2(340, 340));
            rt.pivot = new Vector2(0.5f, 0.5f);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = WheelSprite();
            img.color = new Color(1, 1, 1, 0.9f);
            var w = rt.gameObject.AddComponent<TouchWheel>();
            w.Input = Input;
            return rt;
        }

        // a steering wheel drawn into a texture: rim, three spokes and an amber top marker
        static Sprite WheelSprite()
        {
            const int N = 256; var tex = new Texture2D(N, N, TextureFormat.RGBA32, false);
            var px = new Color32[N * N];
            float c = (N - 1) / 2f;
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float dx = x - c, dy = y - c, r = Mathf.Sqrt(dx * dx + dy * dy) / c, ang = Mathf.Atan2(dx, dy);
                    Color col = Color.clear;
                    if (r > 0.78f && r < 0.98f) col = Mathf.Abs(ang) < 0.12f ? new Color(1f, 0.69f, 0f) : new Color(0.16f, 0.19f, 0.22f);
                    else if (r < 0.26f) col = new Color(0.1f, 0.12f, 0.14f);
                    else if (r < 0.8f && (Mathf.Abs(dy) < 9 && dy <= 4 || dy < 0 && Mathf.Abs(dx) < 9)) col = new Color(0.55f, 0.6f, 0.65f);
                    px[y * N + x] = col;
                }
            tex.SetPixels32(px); tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f));
        }
    }

    /// <summary>A UI element that reports when it is pressed and released (pedals, arrows).</summary>
    public class HoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public Action<bool> Changed;
        public void OnPointerDown(PointerEventData e) => Changed?.Invoke(true);
        public void OnPointerUp(PointerEventData e) => Changed?.Invoke(false);
        void OnDisable() => Changed?.Invoke(false);
    }

    /// <summary>The on-screen steering wheel: drag it round like a real one.</summary>
    public class TouchWheel : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public CarInput Input;
        float lastAng, rot;

        float AngleOf(PointerEventData e)
        {
            var rt = (RectTransform)transform;
            Vector2 center = RectTransformUtility.WorldToScreenPoint(e.pressEventCamera, rt.position);
            Vector2 d = e.position - center;
            return Mathf.Atan2(d.x, d.y);   // 0 at the top, positive clockwise
        }

        public void OnPointerDown(PointerEventData e)
        {
            Input.WheelHeld = true;
            rot = Input.WheelRot;
            lastAng = AngleOf(e);
        }

        public void OnDrag(PointerEventData e)
        {
            float a = AngleOf(e), d = Mathf.DeltaAngle(lastAng * Mathf.Rad2Deg, a * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            lastAng = a;
            rot = Mathf.Clamp(rot + d, -CarInput.MaxWheelRot, CarInput.MaxWheelRot);
            Input.SetWheelFromTouch(rot);
        }

        public void OnPointerUp(PointerEventData e) => Input.WheelHeld = false;
        void OnDisable() { if (Input != null) Input.WheelHeld = false; }
    }

    /// <summary>Keeps the HUD inside the phone's safe area (notches, rounded corners).</summary>
    public class SafeArea : MonoBehaviour
    {
        Rect last;
        void Update()
        {
            var sa = Screen.safeArea;
            if (sa == last || Screen.width == 0) return;
            last = sa;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            rt.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
