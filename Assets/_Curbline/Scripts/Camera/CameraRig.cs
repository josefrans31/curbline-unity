// Chase, cockpit and top-down cameras, ported from updateCamera() in the web game.
using UnityEngine;

namespace Curbline
{
    public enum CamMode { Chase, Cockpit, Top }

    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public CarController Target;
        public CamMode Mode = CamMode.Chase;
        public LayerMask Blockers = ~0;   // what the chase camera must not go through

        Camera cam;
        Vector3 pos, look;
        bool snap = true;

        void Awake() { cam = GetComponent<Camera>(); }

        public void Cycle()
        {
            Mode = (CamMode)(((int)Mode + 1) % 3);
            snap = true;
        }

        public void Snap() => snap = true;

        public static string Label(CamMode m) => m == CamMode.Chase ? "Chase" : m == CamMode.Cockpit ? "Cockpit" : "Top-down";

        void LateUpdate()
        {
            if (Target == null || Target.Spec == null) return;
            float dt = Time.deltaTime;
            var c = Target; var body = c.Spec.Body;
            Vector3 cp = c.transform.position;
            float hh = c.Heading;
            Vector3 f = new Vector3(Mathf.Sin(hh), 0, Mathf.Cos(hh));

            if (Mode == CamMode.Cockpit)
            {
                // driver's eye: a little left of center, behind the windshield
                Vector3 eye = c.transform.TransformPoint(new Vector3(-body.W * 0.2f, body.B + (body.H - body.B) * 0.78f, body.L * 0.02f));
                transform.position = eye;
                transform.rotation = Quaternion.LookRotation(f * 10f + Vector3.down * 0.6f, Vector3.up);
                cam.fieldOfView = 72;
                snap = true;
                return;
            }

            Vector3 want, lookAt;
            if (Mode == CamMode.Chase)
            {
                float dist = body.L * 1.15f + 3.2f, yaw = hh - c.Slip * 0.4f;
                want = new Vector3(cp.x - Mathf.Sin(yaw) * dist, cp.y + 2.6f, cp.z - Mathf.Cos(yaw) * dist);
                lookAt = cp + f * 2f + Vector3.up * 0.8f;
                cam.fieldOfView = 60;

                // keep the camera out of walls and buildings
                Vector3 from = cp + Vector3.up * 1.2f;
                Vector3 to = want - from;
                if (Physics.SphereCast(from, 0.3f, to.normalized, out var hit, to.magnitude, Blockers, QueryTriggerInteraction.Ignore)
                    && hit.rigidbody == null)   // only fixed things: walls, buildings, parked cars, not cones
                {
                    float d = Mathf.Max(1.2f, hit.distance - 0.3f);
                    want = from + to.normalized * d + Vector3.up * (1 - d / to.magnitude) * 2f;
                }
            }
            else
            {
                want = new Vector3(cp.x - Mathf.Sin(hh) * 3.5f, cp.y + 15f, cp.z - Mathf.Cos(hh) * 3.5f);
                lookAt = cp + f * 1.2f;
                cam.fieldOfView = 55;
            }

            float k = snap ? 1 : 1 - Mathf.Pow(0.004f, dt);
            float kl = snap ? 1 : 1 - Mathf.Pow(0.0005f, dt);
            pos = Vector3.Lerp(pos, want, k);
            look = Vector3.Lerp(look, lookAt, kl);
            snap = false;
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        }
    }
}
