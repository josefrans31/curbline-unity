# Curbline (Unity)

The Unity rebuild of [Curbline](https://github.com/josefrans31/Curbline), the 3D parking and city driving game, for the App Store and Google Play. The plan and build order are in `unity/PLAN.md` in that repo.

- Unity **6.6** (`6000.6.7f1`), Universal 3D (URP) template. Move to 6.7 LTS when it ships.
- Our code lives in `Assets/_Curbline/`. Everything else in `Assets/` is from the template or, later, from asset packs.

## Phase 1: drivable car (current)

1. Open the project in Unity Hub.
2. Menu **Curbline > Create Phase 1 test scene**, then press **Play**.
3. Press **I** to start the engine (it shifts to D), **W** to drive.

The scene builds itself at Play time: a walled test lot with bays, parked cars, a cone slalom and a parallel-parking gap; a placeholder car sized from the real car data; chase, cockpit and top-down cameras; a HUD with touch controls.

| Key | Does |
| --- | --- |
| W / S, arrows | Gas, brake (Space brakes too) |
| A / D | Steer |
| I | Engine on or off |
| F / R / N / P | Drive, reverse, neutral, park |
| X / hold Shift | Drift mode toggle / hold |
| C | Camera: chase, cockpit, top-down |
| L | Headlights |
| , and . | Previous / next car (all 24) |
| Backspace | Back to the start, cones reset |
| T | Show or hide the touch controls in the editor |

Controller: left stick steers, right trigger gas, left trigger brake, A drift (hold), X horn, Y camera, D-pad gears, Back engine, Start reset, bumpers change car.

### Scripts

| File | What it is |
| --- | --- |
| `Scripts/Car/CarCatalog.cs` | The 24 cars and their body sizes, ported from the web game |
| `Scripts/Car/CarController.cs` | The driving model, a port of the web game's `physics()` and `rpmStep()` |
| `Scripts/Car/CarInput.cs` | Keyboard, controller and touch input (Input System) |
| `Scripts/Car/PlaceholderCar.cs` | Box-and-cylinder stand-in car with spinning, steering wheels, lights and tire marks |
| `Scripts/Camera/CameraRig.cs` | Chase, cockpit and top-down cameras |
| `Scripts/UI/DriveHud.cs` | Speed, gear, rpm, messages, steering wheel, pedals, gear buttons |
| `Scripts/World/TestLot.cs` | The Phase 1 test lot |
| `Scripts/Core/Phase1Bootstrap.cs` | Builds the Phase 1 scene at Play time |
| `Editor/CurblineMenu.cs` | The Curbline menu |

Axes note: Unity is left-handed, the web game is right-handed. When porting positions from `index.html`, use `x_unity = -x_web` and `heading_unity = -heading_web`.
