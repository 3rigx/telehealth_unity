# Joint Angle Visualization System

This system adds real-time joint angle visualization to the replay mode, showing meaningful movement analysis from resting positions.

## Features

- **Real-time angle calculation** for major joints (elbows, knees, shoulders, hips)
- **Visual overlays** showing current angles and deviation from rest position
- **Color-coded warnings** based on angle thresholds
- **Configurable display options** for which angles to show
- **Keyboard controls** for easy interaction

## Setup

1. **Add AngleVisualizer component** to your ReplayController GameObject
2. **Configure the ReplayController**:
   - Assign the AngleVisualizer reference
   - Enable `EnableAngleVisualization`
   - Set `UseFirstFrameAsRestPosition` to use first frame as baseline

## Controls

### Keyboard Shortcuts
- **A**: Toggle angle visualization on/off
- **R**: Set current frame as rest position
- **T**: Reset rest position to default values

### Existing Controls
- **Space**: Toggle avatar visibility
- **WASD + QE**: Camera movement
- **Arrow Keys + Page Up/Down**: Skeleton positioning
- **Shift + Mouse**: Camera rotation

## Configuration Options

### AngleVisualizer Settings
- **Angle Display Prefab**: Custom prefab for angle display (optional)
- **Offset Distance**: Distance of angle text from joint position
- **Warning Threshold**: Angle deviation that triggers yellow warning (default: 30°)
- **Critical Threshold**: Angle deviation that triggers red warning (default: 60°)
- **Display Options**: Toggle current angle, deviation, and rest angle display
- **Angle Selection**: Choose which specific joints to display

### ReplayController Settings
- **Enable Angle Visualization**: Master toggle for the system
- **Use First Frame As Rest Position**: Automatically use first frame as baseline

## Angle Information Displayed

For each joint, the system shows:
- **Joint Name** (e.g., "Left Elbow")
- **Current Angle**: Real-time angle in degrees
- **Deviation from Rest**: Difference from baseline position
- **Color Coding**: 
  - White: Normal range
  - Yellow: Warning range (>30° deviation)
  - Red: Critical range (>60° deviation)

## Supported Joints

- **Left/Right Elbow**: Flexion/extension angle
- **Left/Right Knee**: Flexion/extension angle  
- **Left/Right Shoulder**: Abduction/flexion angle
- **Left/Right Hip**: Flexion/extension angle

## API Usage

```csharp
// Toggle angle visualization
replayController.ToggleAngleVisualization();

// Set current frame as rest position
replayController.SetCurrentAsRestPosition();

// Reset to default rest position
replayController.ResetRestPosition();

// Configure which angles to display
replayController.ConfigureAngleDisplay(
    leftElbow: true, 
    rightElbow: false, 
    leftKnee: true,
    rightKnee: true,
    leftShoulder: false,
    rightShoulder: false,
    leftHip: true,
    rightHip: true
);

// Get current angle values
JointAngleCalculator.JointAngle[] angles = replayController.GetCurrentAngles();
```

## Technical Details

### Angle Calculation
Angles are calculated using three-point geometry:
- For elbow: shoulder → elbow → wrist
- For knee: hip → knee → ankle
- For shoulder: spine → shoulder → elbow
- For hip: spine → hip → knee

### Rest Position
- If `UseFirstFrameAsRestPosition` is enabled, the first frame is automatically used as baseline
- Manual rest position can be set with 'R' key or `SetCurrentAsRestPosition()`
- Default rest angles are used if no rest position is set

### Performance
- Angle calculations are performed during the Refresh() loop
- Display objects are pooled and reused
- Minimal performance impact when disabled

## Troubleshooting

**Angles not showing:**
- Check that `EnableAngleVisualization` is enabled
- Ensure AngleVisualizer reference is assigned
- Verify skeleton data is available

**Incorrect angles:**
- Check that rest position is set appropriately
- Verify joint mapping matches skeleton format
- Ensure skeleton offset is applied correctly

**Performance issues:**
- Disable unused angle displays
- Reduce update frequency if needed
- Use custom prefab for optimized rendering
