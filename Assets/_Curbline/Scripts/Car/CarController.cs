// The Curbline car model, ported from physics() and rpmStep() in the web game.
// An arcade model, not a tire simulation: speed along the heading, a steering angle,
// a cap on how fast the car can turn at speed (grip), and a slide angle for drift mode.
//
// Axes: Unity is left-handed, so heading h (radians) points along (sin h, 0, cos h) and a
// positive h turns right. The web game is right-handed and turns right with a negative h.
// When levels and the city are ported, mirror them: x_unity = -x_web, h_unity = -h_web.
using System;
using UnityEngine;

namespace Curbline
{
    [RequireComponent(typeof(Rigidbody))]
    public class CarController : MonoBehaviour
    {
        const float Grip = 12.5f, DriftGrip = 7.2f, SlipMax = 0.95f;   // m/s² of sideways grip with and without drift mode; biggest slide angle (rad)
        static readonly float[] GearSteps = { 0f, 0.11f, 0.24f, 0.4f, 0.6f, 0.8f, 1.01f };

        public CarSpec Spec { get; private set; }
        public CarInput Input { get; private set; }

        // state, public so the HUD, camera and visuals can read it
        public char Gear { get; private set; } = 'P';
        public bool Engine { get; private set; }
        public bool EngineRunning => Engine && crank <= 0;
        public bool DriftMode { get; private set; }
        public float Speed => v;                 // m/s along the heading, negative in reverse
        public float Heading => h;               // radians
        public float Slip => slip;               // radians between heading and travel direction
        public float SteerAngle => steer;        // radians at the front wheels
        public float Skid => skid;               // 0..1, for tire marks and squeal
        public float Rpm => rpm;                 // 0..1
        public bool Braking => Input != null && Input.Brake > 0.05f;
        public int Hits { get; private set; }

        public event Action<string> Message;     // short notes for the HUD
        public event Action<float> Hit;          // impact speed (m/s)

        Rigidbody rb;
        float v, h, vh, slip, steer, skid, rpm, crank, hitCd, hintT, lastThr;
        bool started, touching;
        readonly System.Collections.Generic.HashSet<Rigidbody> knocked = new System.Collections.Generic.HashSet<Rigidbody>();

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.mass = 1200f;
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
        }

        public void Setup(CarSpec spec, CarInput input)
        {
            if (Input != null) Unhook();
            Spec = spec; Input = input;
            input.GearRequested += SetGear;
            input.EngineToggled += ToggleEngine;
            input.DriftToggled += ToggleDrift;
        }

        void OnDestroy() { if (Input != null) Unhook(); }

        void Unhook()
        {
            Input.GearRequested -= SetGear;
            Input.EngineToggled -= ToggleEngine;
            Input.DriftToggled -= ToggleDrift;
        }

        /// <summary>Put the car somewhere, stopped, in Park with the engine off.</summary>
        public void Place(Vector3 pos, float headingRad, bool keepEngine = false)
        {
            h = vh = headingRad; v = 0; slip = 0; steer = 0; skid = 0;
            if (!keepEngine) { Engine = false; started = false; crank = 0; rpm = 0; }
            Gear = keepEngine && Engine ? 'D' : 'P';
            rb.position = pos; rb.rotation = Quaternion.Euler(0, h * Mathf.Rad2Deg, 0);
            transform.SetPositionAndRotation(rb.position, rb.rotation);
            rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero;
        }

        public void ResetHits() { Hits = 0; knocked.Clear(); }

        public void SetGear(char g)
        {
            if (g != 'P' && g != 'R' && g != 'N' && g != 'D') return;
            Gear = g;
        }

        public void ToggleEngine()
        {
            if (Engine)
            {
                if (Mathf.Abs(v) > 1.5f) { Message?.Invoke("Stop the car before you switch the engine off"); return; }
                Engine = false; crank = 0; Message?.Invoke("Engine off");
            }
            else
            {
                Engine = true; crank = Spec.Cylinders > 0 ? 0.55f : 0.15f; rpm = 0.5f;
                if (!started) { started = true; if (Gear == 'P') Gear = 'D'; }
                Message?.Invoke("Engine running");
            }
        }

        public void ToggleDrift()
        {
            DriftMode = !DriftMode;
            Message?.Invoke(DriftMode ? "Drift mode on. Turn in and keep your foot down" : "Drift mode off");
        }

        void FixedUpdate()
        {
            if (Spec == null || Input == null) return;
            float dt = Time.fixedDeltaTime;

            // if the physics engine stopped us against something, take its word for our speed
            if (touching)
            {
                float actual = Vector3.Dot(rb.linearVelocity, Dir(vh));
                if (Mathf.Abs(actual) < Mathf.Abs(v)) v = actual;
            }

            Step(dt);

            // drive the body with velocities so the physics engine still stops us at walls
            rb.linearVelocity = Dir(vh) * v;
            float yawNow = rb.rotation.eulerAngles.y * Mathf.Deg2Rad;
            rb.angularVelocity = new Vector3(0, WrapA(h - yawNow) / dt, 0);
            touching = false;   // OnCollisionEnter/Stay set it again during this physics step
        }

        void Update()
        {
            if (Spec == null || Input == null) return;
            RpmStep(Time.deltaTime);
            Hints(Time.deltaTime);
        }

        static Vector3 Dir(float a) => new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a));

        void Step(float dt)
        {
            bool run = EngineRunning;
            float thr = run ? Input.Throttle : 0, brk = Input.Brake, st = Input.Steer;
            float lockRad = Spec.SteerLock, sr = 2.6f * (0.55f + 0.45f * Input.SteerSensitivity);
            steer += Mathf.Clamp(st * lockRad - steer, -sr * dt, sr * dt);

            float a = 0; bool creep = run && brk == 0;   // an idling automatic rolls on its own
            if (Gear == 'P') { a = -Mathf.Sign(v) * Mathf.Min(Mathf.Abs(v) / dt, 14f); thr = 0; }
            else if (Gear == 'D') { if (thr > 0) a += v < 0 ? thr * 8 : thr * Spec.Accel * (1 - Mathf.Max(0, v) / Spec.TopSpeed); else if (creep && v < 1.4f) a += 1.1f; }
            else if (Gear == 'R') { if (thr > 0) a -= v > 0 ? thr * 8 : thr * Spec.Accel * 0.7f * (1 - Mathf.Max(0, -v) / 9f); else if (creep && v > -1.1f) a -= 1.1f; }
            if (brk > 0 && Mathf.Abs(v) > 0.001f) a -= Mathf.Sign(v) * brk * 10f;
            if (Mathf.Abs(v) > 0.001f) a -= v * 0.06f + Mathf.Sign(v) * 0.25f;   // drag and rolling resistance

            float nv = v + a * dt;
            if ((brk > 0 || Gear == 'P' || (thr == 0 && (!creep || Gear == 'N'))) && Mathf.Sign(nv) != Mathf.Sign(v) && v != 0) nv = 0;
            if (Mathf.Abs(nv) < 0.02f && thr == 0 && (!creep || (Gear != 'D' && Gear != 'R'))) nv = 0;

            // steering: tires only grip so hard, so fast corners run wide; drift mode lets the tail step out
            float wb = Spec.Body.Wheelbase, sp = Mathf.Abs(nv), yawWant = nv / wb * Mathf.Tan(steer);
            float sk = 0;
            if ((DriftMode || Input.DriftHold) && nv > 4.5f)
            {
                h += Mathf.Clamp(yawWant * 1.25f, -2.4f, 2.4f) * dt;
                vh += Mathf.Clamp(WrapA(h - vh), -DriftGrip / sp * dt, DriftGrip / sp * dt);
                float s = WrapA(h - vh);
                if (Mathf.Abs(s) > SlipMax) { s = Mathf.Sign(s) * SlipMax; h = vh + s; }
                slip = s; nv = Mathf.Max(0, nv - Mathf.Abs(Mathf.Sin(s)) * 3 * dt); sk = Mathf.Min(1, Mathf.Abs(s) * 2.6f);
            }
            else
            {
                float maxYaw = Grip / Mathf.Max(sp, 0.5f);
                if (Mathf.Abs(yawWant) > maxYaw * 1.08f) sk = Mathf.Min(1, (Mathf.Abs(yawWant) / maxYaw - 1) * 1.4f);
                h += Mathf.Clamp(yawWant, -maxYaw, maxYaw) * dt;
                float rec = (nv > 0.5f ? 2.8f : 20f) * dt, sl = WrapA(h - vh);   // a leftover slide dies away once the tires bite again
                vh = Mathf.Abs(sl) <= rec ? h : vh + Mathf.Sign(sl) * rec;
                slip = WrapA(h - vh);
                if (Mathf.Abs(slip) > 0.1f && sp > 3) sk = Mathf.Max(sk, Mathf.Min(1, Mathf.Abs(slip) * 2.6f));
            }
            if (brk > 0.9f && sp > 14) sk = Mathf.Max(sk, 0.45f);
            skid = sk; v = nv;
            h = WrapA(h); vh = h - WrapA(h - vh);
            hitCd = Mathf.Max(0, hitCd - dt);
        }

        void RpmStep(float dt)
        {
            if (crank > 0) crank -= dt;
            if (!Engine) { rpm = Mathf.Max(0, rpm - dt * 1.6f); return; }
            float thr = crank > 0 ? 0 : Input.Throttle, sp = Mathf.Abs(v), target;
            if (Spec.Cylinders == 0) target = 0.04f + Mathf.Min(1, sp / Spec.TopSpeed) * 0.9f + thr * 0.06f;
            else if (Gear == 'P' || Gear == 'N') target = 0.1f + thr * 0.8f;
            else if (Gear == 'R') target = 0.1f + Mathf.Min(1, sp / 9f) * 0.5f + thr * 0.14f;
            else
            {
                float r = Mathf.Min(1, sp / Spec.TopSpeed); int g = 0;
                while (r >= GearSteps[g + 1]) g++;
                float f = (r - GearSteps[g]) / (GearSteps[g + 1] - GearSteps[g]);
                target = (g == 0 ? 0.1f + f * 0.78f : 0.42f + f * 0.5f) + thr * 0.08f + skid * 0.1f;
            }
            rpm += (Mathf.Min(1, target) - rpm) * Mathf.Min(1, dt * (target > rpm ? 5.5f : 3.4f));
            lastThr = thr;
        }

        void Hints(float dt)
        {
            hintT -= dt;
            if (Input.Throttle > 0.3f && hintT <= 0 && Mathf.Abs(v) < 0.3f)
            {
                if (!Engine) { Message?.Invoke("Start the engine first (I)"); hintT = 3; }
                else if (Gear == 'P') { Message?.Invoke("You are in Park. Shift to D or R"); hintT = 3; }
                else if (Gear == 'N') { Message?.Invoke("You are in Neutral. Shift to D or R"); hintT = 3; }
            }
        }

        // ---------- collisions ----------
        void OnCollisionEnter(Collision c)
        {
            touching = true;
            float impact = Mathf.Abs(v);
            var other = c.rigidbody;
            if (other != null && !other.isKinematic && other.mass < 50f)
            {
                // something light, like a cone: it gets knocked away and we lose a little speed
                v *= 0.92f;
                if (knocked.Add(other)) CountHit(Mathf.Max(1f, impact), light: true);   // each cone counts once
                return;
            }
            Vector3 n = c.contactCount > 0 ? c.GetContact(0).normal : -Dir(vh);   // points away from what we hit
            float into = -Vector3.Dot(Dir(vh), n) * Mathf.Sign(v);
            if (into > 0.2f) { v *= -0.25f; vh = h; slip = 0; } else v *= 0.9f;
            if (impact > 0.35f) CountHit(impact, light: false);
        }

        void OnCollisionStay(Collision c) => touching = true;

        void CountHit(float impact, bool light)
        {
            if (hitCd > 0 && !light) return;
            hitCd = 0.7f;
            Hits++;
            Hit?.Invoke(impact);
        }

        static float WrapA(float a)
        {
            while (a > Mathf.PI) a -= Mathf.PI * 2;
            while (a < -Mathf.PI) a += Mathf.PI * 2;
            return a;
        }
    }
}
