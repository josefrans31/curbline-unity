// Reads keyboard, game controller and on-screen touch controls into one set of driving inputs.
// Ported from readInput() in the web game. Uses the Input System package when it is active,
// and the old Input Manager otherwise.
using System;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace Curbline
{
    public class CarInput : MonoBehaviour
    {
        public const float MaxWheelRot = Mathf.PI * 1.4f;   // how far the on-screen wheel turns each way

        [Range(0.5f, 1.5f)] public float SteerSensitivity = 1f;

        // results, read by CarController
        public float Throttle { get; private set; }
        public float Brake { get; private set; }
        public float Steer { get; private set; }            // -1 left .. +1 right
        public bool DriftHold { get; private set; }
        public bool Horn { get; private set; }
        public float WheelRot => wheelRot;

        // written by the touch HUD
        [NonSerialized] public float TouchThrottle, TouchBrake;
        [NonSerialized] public int TouchSteerDir;          // arrow buttons: -1, 0, +1
        [NonSerialized] public bool WheelHeld;
        [NonSerialized] public bool TouchHorn;

        // one-shot actions
        public event Action<char> GearRequested;           // 'P', 'R', 'N', 'D'
        public event Action EngineToggled, CameraCycled, LightsToggled, DriftToggled, NextCar, PrevCar, ResetCar, TouchUiToggled;

        float wheelRot;

        public void SetWheelFromTouch(float rot) => wheelRot = Mathf.Clamp(rot, -MaxWheelRot, MaxWheelRot);

        // the HUD buttons call these so that every source goes through the same events
        public void RequestGear(char g) => GearRequested?.Invoke(g);
        public void RequestEngine() => EngineToggled?.Invoke();
        public void RequestCamera() => CameraCycled?.Invoke();
        public void RequestLights() => LightsToggled?.Invoke();
        public void RequestDrift() => DriftToggled?.Invoke();
        public void RequestNextCar() => NextCar?.Invoke();
        public void RequestPrevCar() => PrevCar?.Invoke();
        public void RequestReset() => ResetCar?.Invoke();

        void Update()
        {
            float dt = Time.deltaTime;
            float kThr = 0, kBrk = 0, kSt = 0, padThr = 0, padBrk = 0, padSteer = 0;
            bool shift = false, hornKey = false, padDrift = false, padHorn = false;

#if ENABLE_INPUT_SYSTEM
            var kb = Keyboard.current;
            if (kb != null)
            {
                kThr = (kb.wKey.isPressed || kb.upArrowKey.isPressed) ? 1 : 0;
                kBrk = (kb.sKey.isPressed || kb.downArrowKey.isPressed || kb.spaceKey.isPressed) ? 1 : 0;
                kSt = ((kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1 : 0) - ((kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1 : 0);
                shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                hornKey = kb.hKey.isPressed;
                if (kb.fKey.wasPressedThisFrame) GearRequested?.Invoke('D');
                if (kb.rKey.wasPressedThisFrame) GearRequested?.Invoke('R');
                if (kb.nKey.wasPressedThisFrame) GearRequested?.Invoke('N');
                if (kb.pKey.wasPressedThisFrame) GearRequested?.Invoke('P');
                if (kb.iKey.wasPressedThisFrame) EngineToggled?.Invoke();
                if (kb.cKey.wasPressedThisFrame) CameraCycled?.Invoke();
                if (kb.lKey.wasPressedThisFrame) LightsToggled?.Invoke();
                if (kb.xKey.wasPressedThisFrame) DriftToggled?.Invoke();
                if (kb.periodKey.wasPressedThisFrame) NextCar?.Invoke();
                if (kb.commaKey.wasPressedThisFrame) PrevCar?.Invoke();
                if (kb.backspaceKey.wasPressedThisFrame) ResetCar?.Invoke();
                if (kb.tKey.wasPressedThisFrame) TouchUiToggled?.Invoke();
            }
            var gp = Gamepad.current;
            if (gp != null)
            {
                padSteer = gp.leftStick.ReadValue().x;
                if (Mathf.Abs(padSteer) < 0.12f) padSteer = 0;   // dead zone
                padThr = gp.rightTrigger.ReadValue();
                padBrk = gp.leftTrigger.ReadValue();
                padDrift = gp.buttonSouth.isPressed;   // A
                padHorn = gp.buttonWest.isPressed;     // X
                if (gp.buttonNorth.wasPressedThisFrame) CameraCycled?.Invoke();   // Y
                if (gp.dpad.up.wasPressedThisFrame) GearRequested?.Invoke('D');
                if (gp.dpad.down.wasPressedThisFrame) GearRequested?.Invoke('R');
                if (gp.dpad.left.wasPressedThisFrame) GearRequested?.Invoke('P');
                if (gp.dpad.right.wasPressedThisFrame) GearRequested?.Invoke('N');
                if (gp.selectButton.wasPressedThisFrame) EngineToggled?.Invoke();
                if (gp.startButton.wasPressedThisFrame) ResetCar?.Invoke();
                if (gp.rightShoulder.wasPressedThisFrame) NextCar?.Invoke();
                if (gp.leftShoulder.wasPressedThisFrame) PrevCar?.Invoke();
            }
#else
            kThr = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) ? 1 : 0;
            kBrk = (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.Space)) ? 1 : 0;
            kSt = ((Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) ? 1 : 0) - ((Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) ? 1 : 0);
            shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            hornKey = Input.GetKey(KeyCode.H);
            if (Input.GetKeyDown(KeyCode.F)) GearRequested?.Invoke('D');
            if (Input.GetKeyDown(KeyCode.R)) GearRequested?.Invoke('R');
            if (Input.GetKeyDown(KeyCode.N)) GearRequested?.Invoke('N');
            if (Input.GetKeyDown(KeyCode.P)) GearRequested?.Invoke('P');
            if (Input.GetKeyDown(KeyCode.I)) EngineToggled?.Invoke();
            if (Input.GetKeyDown(KeyCode.C)) CameraCycled?.Invoke();
            if (Input.GetKeyDown(KeyCode.L)) LightsToggled?.Invoke();
            if (Input.GetKeyDown(KeyCode.X)) DriftToggled?.Invoke();
            if (Input.GetKeyDown(KeyCode.Period)) NextCar?.Invoke();
            if (Input.GetKeyDown(KeyCode.Comma)) PrevCar?.Invoke();
            if (Input.GetKeyDown(KeyCode.Backspace)) ResetCar?.Invoke();
            if (Input.GetKeyDown(KeyCode.T)) TouchUiToggled?.Invoke();
#endif
            if (kSt == 0) kSt = TouchSteerDir;

            Throttle = Mathf.Max(kThr, TouchThrottle, padThr);
            Brake = Mathf.Max(kBrk, TouchBrake, padBrk);

            float sens = SteerSensitivity;
            if (padSteer != 0)   // the stick is the wheel
                wheelRot = Mathf.Sign(padSteer) * Mathf.Pow(Mathf.Abs(padSteer), 2.2f - sens * 1.2f) * MaxWheelRot;
            else if (kSt != 0)
                wheelRot = Mathf.Clamp(wheelRot + kSt * dt * 4.5f * sens, -MaxWheelRot, MaxWheelRot);
            else if (!WheelHeld)
            {
                float back = dt * 4.2f;   // self-centering
                wheelRot = Mathf.Abs(wheelRot) < back ? 0 : wheelRot - Mathf.Sign(wheelRot) * back;
            }
            Steer = wheelRot / MaxWheelRot;
            DriftHold = shift || padDrift;
            Horn = hornKey || padHorn || TouchHorn;
        }
    }
}
