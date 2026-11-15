// A simple box-and-cylinder car, sized from the car's body profile, so Phase 1 can be driven
// before any art is bought. It is swapped for real models in Phase 7. It also shows what the
// real car prefab will need: wheels that spin and steer, head, brake and reverse lights, tire marks.
using UnityEngine;

namespace Curbline
{
    public class PlaceholderCar : MonoBehaviour
    {
        /// <summary>The car whose wheels and lights this follows. Leave null for a parked car.</summary>
        public CarController Car;
        CarSpec spec;
        Transform root;
        Transform[] steerPivots = new Transform[2];
        Transform[] spinners = new Transform[4];
        Material brakeMat, reverseMat, headMat;
        Light[] headLights = new Light[2];
        TrailRenderer[] marks = new TrailRenderer[2];
        float spin;
        bool lightsOn;

        static readonly Color BrakeOff = new Color(0.35f, 0.02f, 0.02f), BrakeOn = new Color(1f, 0.08f, 0.05f);

        public void Build(CarSpec s)
        {
            spec = s;
            if (root != null) Destroy(root.gameObject);
            root = new GameObject("Visual").transform;
            root.SetParent(transform, false);

            var p = s.Body;
            float L = p.L, W = p.W, H = p.H, b = p.B, wr = p.WheelR;
            float belt = b + (H - b) * 0.52f;        // top of the doors
            var paint = Mats.New(s.Color, 0.75f, 0.25f);
            var glass = Mats.Get(new Color(0.08f, 0.1f, 0.13f), 0.95f, 0.1f);
            var black = Mats.Get(new Color(0.06f, 0.06f, 0.07f), 0.3f);
            var tire = Mats.Get(new Color(0.07f, 0.07f, 0.075f), 0.15f);
            var rim = Mats.Get(new Color(0.78f, 0.8f, 0.83f), 0.8f, 0.9f);
            headMat = Mats.New(new Color(0.9f, 0.9f, 0.85f), 0.9f, 0f, new Color(0.25f, 0.25f, 0.22f));
            brakeMat = Mats.New(new Color(0.5f, 0.05f, 0.05f), 0.8f, 0f, BrakeOff);
            reverseMat = Mats.New(new Color(0.85f, 0.85f, 0.85f), 0.8f, 0f, Color.black);

            // lower body and the cabin
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0, (b + belt) / 2, 0), new Vector3(W, belt - b, L), paint, "Body");
            float cabinL = L * (s.Class == "pickup" ? 0.36f : s.Class == "van" ? 0.78f : 0.5f);
            float cabinZ = s.Class == "pickup" ? 0.1f * L : s.Class == "van" ? -0.05f * L : -0.06f * L;
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0, (belt + H) / 2, cabinZ), new Vector3(W * 0.84f, H - belt, cabinL), glass, "Cabin");
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0, H - 0.03f, cabinZ), new Vector3(W * 0.82f, 0.06f, cabinL * 0.92f), paint, "Roof");
            // bumpers
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0, b + 0.12f, L / 2 + 0.03f), new Vector3(W * 0.96f, 0.2f, 0.08f), black, "FrontBumper");
            Mats.Prim(PrimitiveType.Cube, root, new Vector3(0, b + 0.12f, -L / 2 - 0.03f), new Vector3(W * 0.96f, 0.2f, 0.08f), black, "RearBumper");

            // lights
            float lightY = belt - 0.12f;
            for (int i = 0; i < 2; i++)
            {
                float sx = i == 0 ? -1 : 1;
                Mats.Prim(PrimitiveType.Cube, root, new Vector3(sx * W * 0.34f, lightY, L / 2 + 0.005f), new Vector3(W * 0.2f, 0.1f, 0.04f), headMat, "Headlight");
                Mats.Prim(PrimitiveType.Cube, root, new Vector3(sx * W * 0.36f, lightY, -L / 2 - 0.005f), new Vector3(W * 0.18f, 0.1f, 0.04f), brakeMat, "BrakeLight");
                Mats.Prim(PrimitiveType.Cube, root, new Vector3(sx * W * 0.2f, lightY, -L / 2 - 0.005f), new Vector3(W * 0.07f, 0.08f, 0.04f), reverseMat, "ReverseLight");
                var lg = new GameObject("HeadlightBeam");
                lg.transform.SetParent(root, false);
                lg.transform.localPosition = new Vector3(sx * W * 0.34f, lightY, L / 2 + 0.1f);
                lg.transform.localRotation = Quaternion.Euler(8, 0, 0);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Spot; l.range = 32; l.spotAngle = 62; l.intensity = 6; l.color = new Color(1f, 0.96f, 0.86f);
                l.shadows = LightShadows.None; l.enabled = false;
                headLights[i] = l;
            }

            // wheels: pivot (steers) > spinner (rolls) > tire mesh
            float axleX = W / 2 - 0.16f, rearZ = -L / 2 + p.RearAxle, frontZ = -L / 2 + p.FrontAxle;
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2; float sx = i % 2 == 0 ? -1 : 1;
                var pivot = new GameObject(front ? "FrontWheel" : "RearWheel").transform;
                pivot.SetParent(root, false);
                pivot.localPosition = new Vector3(sx * axleX, wr, front ? frontZ : rearZ);
                var spinner = new GameObject("Spin").transform;
                spinner.SetParent(pivot, false);
                var t = Mats.Prim(PrimitiveType.Cylinder, spinner, Vector3.zero, new Vector3(wr * 2, 0.12f, wr * 2), tire, "Tire");
                t.transform.localRotation = Quaternion.Euler(0, 0, 90);
                var r = Mats.Prim(PrimitiveType.Cylinder, spinner, new Vector3(sx * 0.115f, 0, 0), new Vector3(wr * 1.3f, 0.01f, wr * 1.3f), rim, "Rim");
                r.transform.localRotation = Quaternion.Euler(0, 0, 90);
                // a spoke, so you can see the wheel turn
                Mats.Prim(PrimitiveType.Cube, spinner, new Vector3(sx * 0.125f, 0, 0), new Vector3(0.012f, wr * 1.2f, 0.07f), black, "Spoke");
                if (front) steerPivots[i] = pivot;
                spinners[i] = spinner;
                if (!front)
                {
                    var m = new GameObject("TireMark");
                    m.transform.SetParent(pivot, false);
                    m.transform.localPosition = new Vector3(0, -wr + 0.02f, 0);
                    m.transform.localRotation = Quaternion.Euler(90, 0, 0);   // lie flat on the road
                    var tr = m.AddComponent<TrailRenderer>();
                    tr.alignment = LineAlignment.TransformZ;
                    tr.time = 10f; tr.minVertexDistance = 0.2f; tr.widthMultiplier = 0.22f;
                    tr.material = TrailMat;
                    tr.startColor = tr.endColor = new Color(0.04f, 0.04f, 0.05f, 0.45f);
                    tr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    tr.emitting = false;
                    marks[i - 2] = tr;
                }
            }
            foreach (var mr in root.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            SetLights(lightsOn);
        }

        static Material trailMat;
        static Material TrailMat
        {
            get
            {
                if (trailMat == null)
                {
                    var sh = Shader.Find("Sprites/Default");
                    trailMat = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Unlit"));
                }
                return trailMat;
            }
        }

        public void SetLights(bool on)
        {
            lightsOn = on;
            foreach (var l in headLights) if (l != null) l.enabled = on;
            if (headMat != null) Mats.SetEmission(headMat, on ? new Color(2.2f, 2.1f, 1.8f) : new Color(0.25f, 0.25f, 0.22f));
        }

        void LateUpdate()
        {
            var car = Car;
            if (car == null || root == null || spec == null) return;
            float dt = Time.deltaTime, wr = spec.Body.WheelR;
            spin = (spin + car.Speed / wr * dt * Mathf.Rad2Deg) % 360f;
            for (int i = 0; i < 4; i++) if (spinners[i] != null) spinners[i].localRotation = Quaternion.Euler(spin, 0, 0);
            for (int i = 0; i < 2; i++) if (steerPivots[i] != null) steerPivots[i].localRotation = Quaternion.Euler(0, car.SteerAngle * Mathf.Rad2Deg, 0);

            bool braking = car.Braking || car.Gear == 'P' && car.EngineRunning && Mathf.Abs(car.Speed) < 0.1f;
            float tail = lightsOn ? 0.55f : 0f;
            Mats.SetEmission(brakeMat, braking ? BrakeOn * 3f : Color.Lerp(BrakeOff, BrakeOn, tail));
            Mats.SetEmission(reverseMat, car.Gear == 'R' && car.EngineRunning ? new Color(2f, 2f, 2f) : Color.black);

            bool marking = car.Skid > 0.35f && Mathf.Abs(car.Speed) > 2f;
            foreach (var m in marks) if (m != null) m.emitting = marking;
        }
    }
}
