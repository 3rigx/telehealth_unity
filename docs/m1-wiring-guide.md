# Milestone 1 — Editor Wiring Guide

**Goal of M1:** capture a session into **separate per-sensor files** inside a **patient-ID folder**, and replay it. EEG is still M2; ZED + FSR are captured now.

All C# for M1 is written. The steps below are the Unity-Editor work only you can do. Nothing runs until these are done. Do them in order.

---

## 0. Let Unity compile the new scripts
Open the project and let it import. Confirm **no compile errors** in the Console. New files:
- `Assets/Scripts/Exercises/ExerciseClass.cs`
- `Assets/Scripts/Util/SessionContext.cs`
- `Assets/Scripts/SaveSystem/SessionTime.cs`, `SessionManifest.cs`, `SessionPaths.cs`, `SessionWriter.cs`, `SessionLoader.cs`
- `Assets/Scripts/SaveSystem/Writers/FsrCsvWriter.cs`, `ZedSkeletonWriter.cs`
- `Assets/Scripts/UI/Menu/SessionSetupController.cs`

Edited: `ExerciseController.cs`, `ReplayController.cs` (Telerehab broadcast removed).

---

## 1. Build the "SessionSetup" screen (Menu scene)

Open `Assets/Scenes/MainMenu/Menu.unity`. Find the GameObject that has the **`MenuController`** component (parent of the MainMenu / Settings / Feature screens).

1. Duplicate an existing screen (e.g. the Settings screen panel) under that same parent and rename it **`SessionSetup`**.
   - ⚠️ **It must be left ACTIVE in the hierarchy at scene load.** `MenuController` discovers screens with `GetComponentsInChildren` (active only) at `Start()`, *then* deactivates them. An inactive screen object will never be found.
   - The GameObject's component name strips to the screen name: keep the component **`SessionSetupController`**, which registers as screen **"SessionSetup"**.
2. Remove the old (Settings) controller component from the duplicate; **Add Component → `SessionSetupController`**.
3. Lay out these child UI widgets inside the panel:
   - **TMP_Dropdown** → "Patient" (pick-or-create list)
   - **TMP_InputField** → "New patient ID"
   - **Toggle** ×3 → "ZED", "FSR", "EEG"
   - **TMP_Dropdown** → "Class"
   - **Button** → "Start"
   - *(optional)* **TMP_Text** title, and reuse the existing error **PopupController**
4. Select the `SessionSetup` object and fill the **SessionSetupController** Inspector fields:
   - `Menu Controller` → the MenuController object
   - `Exercise Scene Name` = `CompleteExercise` (default)
   - `Prediction Scene Name` = `Prediction` (scene exists only after M6; fine to leave)
   - `Patient Dropdown`, `New Patient Field`, `Zed/Fsr/Eeg Toggle`, `Class Dropdown`, `Title Text`, `Error Controller` → drag the widgets
5. **EEG toggle:** set it **off** and **un-checked `Interactable`** for now, with a label like "EEG (M2)". The flag is still recorded in the manifest; the connector arrives in M2.
6. **Start button** → OnClick → `SessionSetupController.OnStart`.
7. You can leave the patient/class dropdowns empty in the editor — the controller fills them at `OnEnable` (class options must stay in enum order Idle / Motion / Motion+Cognitive; the controller enforces this).

---

## 2. Rewire the main-menu buttons

Still in `Menu.unity`, on the MainMenu screen:
- **Exercise** button: change OnClick from `MainMenuController.StartExercise` → **`SessionSetupController.OpenForExercise`**.
- Add a **Prediction** button → **`SessionSetupController.OpenForPrediction`** (will show "Prediction scene not available yet" until M6 — expected).
- **Replay** button: leave as `MainMenuController.StartReplay` (unchanged).
- **Free Mode**: leave as-is (retained, no-save sandbox).

> If you skip the rewire, the Exercise button still loads `CompleteExercise` directly but without a `SessionContext`, so capture falls back to the **legacy save dialogs** instead of per-sensor files.

---

## 3. Remove the Unity-side broadcast (per your instruction)

Delete the Telerehab broadcast GameObjects/components from the scenes that have them (search the Hierarchy for these component types in `Menu.unity` and `Replay.unity`):
- `TelerehabBootstrap`
- `TelerehabWebSocketServer`
- `TelerehabDispatcher`
- `TelerehabVideoStreamer`

The C# classes stay in `Assets/Scripts/Network/` (dormant, reversible) — we only remove them from the scenes so no server starts and nothing broadcasts. The code-side broadcast calls are already removed from `ReplayController`.

> Note: `ReplayController` lost its public fields `ParticipantId / SessionNumber / TrialNumber / Condition`. Unity will silently drop those serialized values from the Replay scene — no action needed.

---

## 4. Build Settings
`File → Build Settings`: confirm `Menu`, `CompleteExercise`, and `Replay` are in the scene list (they already are). `Prediction` is added in M6.

---

## 5. Verify (no EEG hardware needed)

1. **Sensors:** in Settings set FSR connection type to **Mock** (so capture runs without real FSR). ZED needs the camera/SDK as today; if you don't have ZED attached, capture still writes files but skeleton frames will be empty.
2. Play → **Menu → Exercise** → SessionSetup:
   - Pick **➕ New patient**, type e.g. `P001`.
   - ZED on, FSR on, EEG off.
   - Class = **Motion**.
   - **Start** → the `CompleteExercise` scene runs with the live session.
3. Let it record a few seconds, then **Close/Stop** (the existing exercise Close button).
4. Open the data folder:
   `C:\Users\<you>\AppData\LocalLow\<Company>\<Product>\Sessions\P001\<timestamp>_Motion\`
   Confirm it contains: **`session.json`**, **`zed_skeleton.json`**, **`fsr.csv`**, and **`zed_video.svo`** (if ZED was recording).
   - Open `session.json` — patientId, exerciseClass=`Motion`, trialNumber, sample counts, `eeg.enabled=false`.
   - Open `fsr.csv` — header + one row per captured tick.
5. **Replay:** Menu → Replay → in the file dialog navigate into that session folder and pick **`session.json`** → the avatar should animate and the angle charts populate, exactly as before.
6. **Legacy check (optional):** Replay → pick an **old** fused `.json` save → it still loads (legacy fallback in `SessionLoader`).
7. **Discard check:** start another session, hit **Discard** → confirm the just-created session folder is removed (so it doesn't inflate the next trial number).

---

## What "done" looks like for M1
- Exercise capture writes a patient-ID/session folder with **separate** `zed_skeleton.json` + `fsr.csv` + `zed_video.svo` + `session.json`.
- Replay loads a session via `session.json` and animates/charts as before.
- Old saves still replay.
- No WebSocket server starts; nothing broadcasts.

Once you've verified this, we move to **M2 (Unicorn EEG connector + eeg.csv + signal-quality feedback)**.
