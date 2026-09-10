# Telehealth-Unity Upgrade — Design & Implementation Plan

**Status:** Approved design, pre-implementation. Generated 2026-06-11.
**Build model:** Claude writes all C# + ScriptableObjects + per-milestone Editor-wiring guides; a human wires scenes/prefabs/UI in the Unity Editor and runs/verifies. Claude cannot author scene/prefab YAML reliably, cannot compile Unity, and cannot test the Unicorn hardware.

---

## 1. Locked decisions (from the design interview)

| # | Topic | Decision |
|---|-------|----------|
| 1 | EEG existence | **Real EEG now.** Device = **g.tec Unicorn Hybrid Black** (8 EEG ch @ 250 Hz, 24-bit, +IMU, Bluetooth). |
| 2 | EEG runtime | **In-process in Unity** via the Unicorn .NET/C API (`Unicorn.dll` P/Invoke), background thread @ 250 Hz, EEG file written in C#. |
| 3 | EEG file format | **CSV** now (timestamp + 8 EEG + 6 IMU + status). EDF is an optional later converter. |
| 4 | EEG live feedback | **Signal-quality / electrode-contact indicator only** — no cognitive number during the live session (that is analysis, deferred to Replay). |
| 5 | Prediction | **Deferred stub.** Menu entry + placeholder scene with an empty, clearly-marked "model goes here" slot. No ML/training/inference built now. The 3 exercise classes are the future training labels. |
| 6 | Save model | **Separate per-sensor files are the source of truth**, tied by a `session.json` manifest. A new loader re-joins ZED+FSR into the in-memory `SensorSystemState` list Replay already expects; EEG loads as a parallel 250 Hz series on its own chart. |
| 7 | Cognitive task | **Minimal app-presented task with logged onset/offset markers.** Default = serial-subtraction / arithmetic prompts. Stimulus + response timestamps written into the manifest so EEG epochs are labelable. |
| 8 | Sensor selection | **Per-session enable toggle + per-sensor source configured in Settings.** Enabled-but-unreachable sensor blocks start with an error. Manifest records which sensors were active. |
| 9 | Live exercise UI | **Status HUD + live avatar mirror.** ZED tracking dot/"in frame"; FSR live L/R pressure bars / foot-color map; EEG overall quality dot (expand to 8 ch). Arithmetic overlay only in Motion+Cognitive. Animated, no scores/metrics. |
| 10 | Replay analysis | **Modular toggleable panel per modality, curated metrics, scrubber-synced, CSV-exportable.** (Metrics in §4.) |
| 11 | User ID | **Pick-or-create** from existing `Sessions/` folders; new IDs sanitized to a filesystem-safe slug. Folder name = patient ID. |
| 12 | Dashboard | **Remove dashboard wiring** from capture/replay paths. `Telerehab*` network classes stay dormant in the repo (not deleted), reversible. |
| — | Free Mode | **Retained** as a no-save sandbox/calibration screen. |

---

## 2. Capture session flow (target)

```
Menu (Mode select: Exercise | Replay | Prediction)
   │
   ├─ Exercise / Prediction ──► Session Setup screen
   │        • Pick-or-create Patient ID (dropdown of existing + "New patient")
   │        • Per-sensor enable toggles (ZED / FSR / EEG) + show configured source
   │        • Exercise class: IDLE | Motion | Motion+Cognitive   (Exercise only;
   │          Prediction does NOT use the 3 classes)
   │        • Start  ──► validate enabled sensors connect ──► run session
   │                       • live HUD + avatar mirror, no analysis
   │                       • Motion+Cognitive: arithmetic overlay + event logging
   │                       • on Stop: write per-sensor files + session.json
   │
   ├─ Replay ──► load session.json ──► re-join ──► animate + per-modality analysis
   │
   └─ Prediction ──► placeholder scene (capture-like shell, empty model slot)
```

---

## 3. On-disk layout & formats

### 3.1 Folder layout
```
<persistentDataPath>/Sessions/<PatientId>/<yyyy-MM-dd_HH-mm-ss>_<Class>/
    session.json        ← manifest (source-of-truth index + integrity hash)
    zed_skeleton.json   ← per-frame SkeletonState series
    zed_video.svo       ← raw ZED recording (already produced today)
    fsr.csv             ← per-sample L/R foot, 4 channels each
    eeg.csv             ← per-sample 8ch EEG + 6ch IMU @ 250 Hz
```
`<Class>` ∈ `Idle | Motion | MotionCognitive` (or `Prediction` for prediction mode).
Patient folder is created once; sessions accumulate under it; trial number auto-increments per patient+class.

### 3.2 `session.json` manifest schema (v1)
```json
{
  "schemaVersion": 1,
  "patientId": "P001",
  "sessionId": "2026-06-11_14-03-22_MotionCognitive",
  "mode": "Exercise",
  "exerciseClass": "MotionCognitive",
  "operator": "",
  "trialNumber": 3,
  "startUtc": "2026-06-11T14:03:22Z",
  "endUtc":   "2026-06-11T14:09:55Z",
  "sensors": {
    "zed": { "enabled": true, "source": "live", "skeletonFile": "zed_skeleton.json",
             "videoFile": "zed_video.svo", "sampleRateHz": 10, "frameCount": 3940 },
    "fsr": { "enabled": true, "source": "WebSocket", "file": "fsr.csv",
             "sampleRateHz": 10, "sampleCount": 3940 },
    "eeg": { "enabled": true, "source": "Unicorn", "file": "eeg.csv",
             "sampleRateHz": 250, "channels": 8, "sampleCount": 98750 }
  },
  "cognitiveTask": {
    "type": "SerialSubtraction",
    "events": [
      { "onsetUtc": "...", "offsetUtc": "...", "prompt": "100-7", "response": "93", "correct": true }
    ]
  },
  "hash": "<sha256 over manifest fields + per-file digests>"
}
```

### 3.3 `fsr.csv` columns
`timestamp_utc, t_ms, L_toe, L_mid_inner, L_mid_outer, L_heel, R_toe, R_mid_inner, R_mid_outer, R_heel`
(FSRState = Toe / Middle_Inner / Middle_Outer / Heel, per foot.)

### 3.4 `eeg.csv` columns (Unicorn Hybrid Black acquisition order — verify against installed SDK)
`timestamp_utc, sample_index, EEG1..EEG8, ACC_X, ACC_Y, ACC_Z, GYR_X, GYR_Y, GYR_Z, BATTERY, COUNTER, VALIDATION`
Unicorn stream = 17 channels (8 EEG, 3 accel, 3 gyro, battery, counter, validation). Sample rate 250 Hz.
Size note: ~17 cols × 250 Hz ≈ a few MB per minute as text — acceptable for minutes-long sessions.

### 3.5 `zed_skeleton.json`
Serialized list of the existing `SkeletonState` (joint positions/rotations, root rotation, feet offset, camera pose, body format) — one entry per frame with its UTC timestamp. This is today's skeleton payload, just split out of the fused file.

### 3.6 Time alignment / re-join
All files carry **UTC timestamps**. The loader joins **ZED + FSR by nearest-timestamp** back into the `SensorSystemState` list. **EEG stays an independent 250 Hz series**; Replay maps the scrubber time → nearest EEG sample index for its own panel (never crammed into `SensorSystemState`).

### 3.7 Legacy files
Old fused-JSON saves remain loadable via a retained legacy path in `SessionLoader` (detect: no `session.json`, single fused file). No migration required; new sessions use the new layout.

---

## 4. Replay analysis — per-modality modules

Interface `IAnalysisModule` (Init/OnCursor/Summary/Clear), one toggleable panel each, scrubber-synced, summary stats per session, CSV export via existing `CSVCreator`.

- **Skeleton (`SkeletonAnalysisModule`)** — joint angles (existing `JointAngleCalculator`) **+ range-of-motion per joint, left/right symmetry, joint velocity & smoothness (jerk), rep detection/count.**
- **FSR (`FsrAnalysisModule`)** — **center-of-pressure trajectory, L/R load balance over time, heel↔toe loading, asymmetry %, sway area.**
- **EEG (`EegAnalysisModule`)** — **per-band power time series (δ/θ/α/β/γ), derived cognitive-load index (e.g. frontal θ or engagement β/(α+θ)), spectrogram, overlaid with cognitive-task markers.**

Computation lives in / extends the existing `Statistics` layer (`DerivedState`, `DerivedMetadata`, `StatisticsController`), which is currently a shallow 10-sample window and will be fleshed out.

---

## 5. Component inventory

### 5.1 New C# scripts
**Save system**
- `SaveSystem/SessionManifest.cs` — serializable manifest + read/write + hash.
- `SaveSystem/SessionPaths.cs` — builds folder/file paths from patientId + class + timestamp; enumerates existing patients/sessions for pick-or-create.
- `SaveSystem/SessionWriter.cs` — orchestrates per-sensor writers + manifest finalize.
- `SaveSystem/Writers/FsrCsvWriter.cs`, `EegCsvWriter.cs`, `ZedSkeletonWriter.cs`.
- `SaveSystem/SessionLoader.cs` — reads manifest, loads each file, re-joins ZED+FSR into `SensorSystemState`, exposes EEG series; legacy fused-JSON fallback.

**EEG**
- `Sensors/EEG/UnicornInterop.cs` — `DllImport("Unicorn")` P/Invoke signatures.
- `Sensors/EEG/UnicornEEGConnector.cs` — background acquisition thread @ 250 Hz, ring buffer.
- `Sensors/EEG/MockEEGConnector.cs` — synthetic stream for no-hardware runs.
- `Sensors/EEG/EEGState.cs`, `Sensors/EEG/EEGSignalQuality.cs`.

**Sensor selection**
- `Sensors/SensorSessionConfig.cs` — per-session enable flags + per-sensor source (PlayerPrefs/ScriptableObject backed).

**Menu / setup**
- `UI/Menu/ModeMenuController.cs` — top-level Exercise / Replay / Prediction.
- `UI/Menu/SessionSetupController.cs` — patient pick-or-create, sensor toggles, class select, Start.

**Exercise classes / cognitive task**
- `Exercises/ExerciseClass.cs` — enum `Idle | Motion | MotionCognitive`.
- `Exercises/Cognitive/SerialSubtractionTask.cs`, `Exercises/Cognitive/CognitiveTaskLogger.cs`.

**Live UI**
- `UI/Exercise/SensorStatusHud.cs` + small feedback widgets (pressure bars, quality dot).

**Replay analysis**
- `Replay/Analysis/IAnalysisModule.cs`, `AnalysisPanelController.cs`,
  `SkeletonAnalysisModule.cs`, `FsrAnalysisModule.cs`, `EegAnalysisModule.cs`.

**Prediction**
- `Prediction/PredictionStubController.cs` — capture-like shell, empty model slot.

### 5.2 Modified C# scripts
- `Exercises/ExerciseController.cs` — replace `SaveManager.Save`/save dialogs with `SessionWriter`; inject class + active sensors; remove `Telerehab*` calls; integrate cognitive task + live HUD.
- `Sensors/SensorSystemController.cs` — add EEG, honor per-session enable + source, drop hard ZED+FSR requirement.
- `Replay/ReplayController.cs` — load via `SessionLoader`; remove `TelerehabBroadcaster`/`TelerehabCommandBus`; mount analysis modules + EEG series.
- `UI/Menu/MainMenuController.cs` (+ menu screens) — restructure to Exercise/Replay/Prediction + setup flow.
- `UI/Menu/SettingsMenuController.cs` — add EEG + ZED source config alongside FSR.
- `SaveSystem/SaveManager.cs` — keep for legacy load; new writes go through `SessionWriter`.

`SensorSystemState` stays ZED+FSR (EEG is parallel, not fused).

---

## 6. Milestones & per-milestone Editor obligations

**M1 — Save refactor + user-ID flow (foundation).**
Code: manifest, paths, FSR/ZED writers, loader (+ legacy fallback), `SessionSetupController`, pick-or-create.
Editor: build Session Setup screen (patient dropdown + new-patient field + class buttons + sensor toggles); rewire `ExerciseController` to use it; remove the two save-dialog calls.

**M2 — EEG connector (needs hardware to verify).**
Code: `UnicornInterop`, `UnicornEEGConnector`, `EegCsvWriter`, `EEGSignalQuality`, `MockEEGConnector`.
Editor: drop `Unicorn.dll` into `Assets/Plugins/`; add EEG source dropdown in Settings + EEG enable toggle.

**M3 — Menu reorg + classes + cognitive task.**
Code: `ModeMenuController`, `ExerciseClass`, `SerialSubtractionTask`, `CognitiveTaskLogger`.
Editor: top-level Mode menu (Exercise/Replay/Prediction) with transition animations; arithmetic overlay UI.

**M4 — Live HUD + avatar mirror.**
Code: `SensorStatusHud` + widgets.
Editor: HUD canvas in `CompleteExercise` scene; bind widgets to sensors; confirm live patient avatar visible.

**M5 — Replay analysis modules.**
Code: `IAnalysisModule`, Skeleton/FSR/EEG modules, `AnalysisPanelController`, chart bindings.
Editor: per-modality panels in `Replay` scene; chart prefabs; toggle buttons; panel reveal animations.

**M6 — Prediction stub.**
Code: `PredictionStubController`.
Editor: `Prediction` scene from a capture-shell copy; placeholder "predicted class — model pending" UI.

**Animation** (req. #7) woven across: menu transitions (M3), HUD pulses/pressure bars (M4), replay panel reveals (M5); the replay avatar already animates.

---

## 7. Risks & open technical items
- **Unicorn P/Invoke signatures + exact channel order** must be verified against the installed Unicorn SDK version (high impact, blocks M2 verification).
- **EDF deferred** — CSV is the M2 deliverable; EDF converter optional later.
- **ZED/FSR rate drift** — nearest-timestamp join tolerance to be tuned during M5.
- **EEG file size** — minutes-long sessions only; long recordings may warrant binary later.
- **Manual scene/prefab wiring** — every milestone has an Editor step; nothing runs until wired.
- **Cognitive task type** defaulted to serial-subtraction; swap to Stroop/n-back is a localized change.
```
