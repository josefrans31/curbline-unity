// Phase 1: one scene object builds everything at Play time: the test lot, the car, the camera
// and the HUD. Put it on an empty GameObject (menu Curbline > Create Phase 1 test scene does it).
using UnityEngine;

namespace Curbline
{
    public class Phase1Bootstrap : MonoBehaviour
    {
        [Tooltip("Which car to start in; see CarCatalog for the ids.")]
        public string StartCar = "kestrel";
        [Tooltip("Steer with left and right arrow buttons instead of the wheel on touch screens.")]
        public bool TouchArrows = false;

        CarController car;
        CarInput input;
        PlaceholderCar visual;
        BoxCollider body;
        CameraRig rig;
        DriveHud hud;
        TestLot lot;
        int carIndex;
        bool lights;

        void Start()
        {
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 1f / 60f;   // the web game steps at 60 Hz too

            SetUpLight();
            lot = new GameObject("TestLot").AddComponent<TestLot>();
            lot.Build();

            // the car: rigidbody and collider on the root, the placeholder model as a child
            var go = new GameObject("PlayerCar");
            input = go.AddComponent<CarInput>();
            car = go.AddComponent<CarController>();
            body = go.AddComponent<BoxCollider>();
            body.sharedMaterial = new PhysicsMaterial("Car") { dynamicFriction = 0, staticFriction = 0, frictionCombine = PhysicsMaterialCombine.Minimum, bounciness = 0 };
            visual = go.AddComponent<PlaceholderCar>();
            visual.Car = car;

            var camGo = Camera.main != null ? Camera.main.gameObject : new GameObject("Main Camera", typeof(Camera), typeof(AudioListener)) { tag = "MainCamera" };
            camGo.GetComponent<Camera>().farClipPlane = 600;
            if (!camGo.TryGetComponent(out rig)) rig = camGo.AddComponent<CameraRig>();
            rig.Target = car;

            hud = new GameObject("HUD Root").AddComponent<DriveHud>();
            hud.Title = () => "Phase 1 test lot · " + CameraRig.Label(rig.Mode) + " camera";

            carIndex = Mathf.Max(0, IndexOf(StartCar));
            SetCar(carIndex, TestLot.SpawnPos, TestLot.SpawnHeading);
            hud.Build(car, input);
            hud.UseArrows(TouchArrows);

            input.CameraCycled += () => { rig.Cycle(); hud.Flash(CameraRig.Label(rig.Mode) + " camera"); };
            input.LightsToggled += () => { lights = !lights; visual.SetLights(lights); hud.Flash(lights ? "Headlights on" : "Headlights off"); };
            input.NextCar += () => SwitchCar(+1);
            input.PrevCar += () => SwitchCar(-1);
            input.ResetCar += () =>
            {
                car.Place(TestLot.SpawnPos, TestLot.SpawnHeading);
                car.ResetHits(); lot.ResetCones(); rig.Snap();
                hud.Flash("Back to the start");
            };
            car.Hit += impact => hud.Flash(impact > 4 ? "Crunch! That counts as a touch" : "Touch");
            hud.Flash("Press I (or ENGINE) to start the engine, then W (or GAS) to drive");
        }

        static int IndexOf(string id)
        {
            var all = CarCatalog.All;
            for (int i = 0; i < all.Count; i++) if (all[i].Id == id) return i;
            return -1;
        }

        void SwitchCar(int step)
        {
            if (Mathf.Abs(car.Speed) > 1f) { hud.Flash("Stop before you change cars"); return; }
            int n = CarCatalog.All.Count;
            carIndex = ((carIndex + step) % n + n) % n;
            SetCar(carIndex, car.transform.position, car.Heading);
            var s = car.Spec;
            hud.Flash($"{s.Name} · {s.ClassLabel} · {Mathf.RoundToInt(s.TopSpeed * 3.6f)} km/h top speed");
        }

        void SetCar(int index, Vector3 pos, float heading)
        {
            var spec = CarCatalog.All[index];
            car.Setup(spec, input);
            var b = spec.Body;
            body.size = new Vector3(b.W + 0.04f, b.H - b.B, b.L + 0.14f);
            body.center = new Vector3(0, b.B + (b.H - b.B) / 2, 0);
            visual.Build(spec);
            visual.SetLights(lights);
            car.Place(pos, heading);
            rig.Snap();
        }

        static void SetUpLight()
        {
            Light sun = null;
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null) sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            sun.intensity = 1.2f;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.shadows = LightShadows.Soft;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.85f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.52f, 0.5f);
            RenderSettings.ambientGroundColor = new Color(0.25f, 0.24f, 0.22f);
        }
    }
}
