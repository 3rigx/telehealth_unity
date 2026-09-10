# GenerateReport.ps1
# Produces SystemTestMethodology.docx in the project root
# Run: powershell -ExecutionPolicy Bypass -File .\GenerateReport.ps1

Add-Type -AssemblyName "WindowsBase"   # System.IO.Packaging

$outPath = "$PSScriptRoot\SystemTestMethodology.docx"
if (Test-Path $outPath) { Remove-Item $outPath -Force }

# ───────────────────────────────────────────────────────────────────────────────
# Helper: write a part into the package
# ───────────────────────────────────────────────────────────────────────────────
function WritePart($pkg, [string]$uri, [string]$contentType, [string]$xml) {
    $u    = [System.Uri]::new($uri, [System.UriKind]::Relative)
    $part = $pkg.CreatePart($u, $contentType,
                [System.IO.Packaging.CompressionOption]::Normal)
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($xml)
    $s = $part.GetStream()
    $s.Write($bytes, 0, $bytes.Length)
    return $part
}

function AddRel($source, [string]$id, [string]$type, [string]$target) {
    $source.CreateRelationship(
        [System.Uri]::new($target, [System.UriKind]::Relative),
        [System.IO.Packaging.TargetMode]::Internal,
        $type, $id) | Out-Null
}

# ───────────────────────────────────────────────────────────────────────────────
# Word XML helpers
# ───────────────────────────────────────────────────────────────────────────────
function X([string]$s) { [System.Security.SecurityElement]::Escape($s) }

# Paragraph styles
# styleId maps to styles defined in styles.xml below
function Para([string]$styleId, [string]$text) {
    return "<w:p><w:pPr><w:pStyle w:val=`"$styleId`"/></w:pPr><w:r><w:t xml:space=`"preserve`">$(X $text)</w:t></w:r></w:p>"
}

function ParaRuns([string]$styleId, [string[]]$runs) {
    # runs: alternating bold-flag then text  e.g. "1","Bold text","0","normal"
    $inner = ""
    for ($i = 0; $i -lt $runs.Length; $i += 2) {
        $bold = $runs[$i]
        $txt  = X $runs[$i+1]
        if ($bold -eq "1") {
            $inner += "<w:r><w:rPr><w:b/></w:rPr><w:t xml:space=`"preserve`">$txt</w:t></w:r>"
        } else {
            $inner += "<w:r><w:t xml:space=`"preserve`">$txt</w:t></w:r>"
        }
    }
    return "<w:p><w:pPr><w:pStyle w:val=`"$styleId`"/></w:pPr>$inner</w:p>"
}

function Bullet([string]$text) {
    return "<w:p><w:pPr><w:pStyle w:val=`"ListBullet`"/></w:pPr><w:r><w:t xml:space=`"preserve`">$(X $text)</w:t></w:r></w:p>"
}

function BulletBold([string]$bold, [string]$rest) {
    return "<w:p><w:pPr><w:pStyle w:val=`"ListBullet`"/></w:pPr><w:r><w:rPr><w:b/></w:rPr><w:t xml:space=`"preserve`">$(X $bold)</w:t></w:r><w:r><w:t xml:space=`"preserve`">$(X $rest)</w:t></w:r></w:p>"
}

function PageBreak() {
    return "<w:p><w:r><w:br w:type=`"page`"/></w:r></w:p>"
}

# Table: $headers = string[], $rows = array of string[]
function Table([string[]]$headers, $rows) {
    $ns  = 'xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"'
    $tbl = '<w:tbl>'
    $tbl += '<w:tblPr><w:tblStyle w:val="TableGrid"/><w:tblW w:w="9360" w:type="dxa"/></w:tblPr>'
    $tbl += '<w:tblGrid>'
    $colW = [math]::Floor(9360 / $headers.Length)
    foreach ($h in $headers) { $tbl += "<w:gridCol w:w=`"$colW`"/>" }
    $tbl += '</w:tblGrid>'

    # header row
    $tbl += '<w:tr>'
    foreach ($h in $headers) {
        $tbl += "<w:tc><w:tcPr><w:tcW w:w=`"$colW`" w:type=`"dxa`"/><w:shd w:val=`"clear`" w:color=`"auto`" w:fill=`"1F3864`"/></w:tcPr>"
        $tbl += "<w:p><w:pPr><w:jc w:val=`"center`"/></w:pPr><w:r><w:rPr><w:b/><w:color w:val=`"FFFFFF`"/><w:sz w:val=`"18`"/></w:rPr><w:t>$(X $h)</w:t></w:r></w:p></w:tc>"
    }
    $tbl += '</w:tr>'

    # data rows
    $even = $false
    foreach ($row in $rows) {
        $fill = if ($even) { "DAE3F3" } else { "FFFFFF" }
        $even = -not $even
        $tbl += '<w:tr>'
        foreach ($cell in $row) {
            $tbl += "<w:tc><w:tcPr><w:tcW w:w=`"$colW`" w:type=`"dxa`"/><w:shd w:val=`"clear`" w:color=`"auto`" w:fill=`"$fill`"/></w:tcPr>"
            $tbl += "<w:p><w:r><w:rPr><w:sz w:val=`"18`"/></w:rPr><w:t xml:space=`"preserve`">$(X $cell)</w:t></w:r></w:p></w:tc>"
        }
        $tbl += '</w:tr>'
    }
    $tbl += '</w:tbl>'
    return $tbl
}

# ───────────────────────────────────────────────────────────────────────────────
# Document body
# ───────────────────────────────────────────────────────────────────────────────
$body = ""

# ── Cover / Title ──────────────────────────────────────────────────────────────
$body += Para "Title"    "Telehealth Unity — Replay Analyser"
$body += Para "Subtitle" "Literature Summary & System Testing Methodology"
$body += Para "Normal"   "Project: telehealth-unity  |  Date: May 2026"
$body += PageBreak

# ── Table of Contents placeholder ─────────────────────────────────────────────
$body += Para "Heading1" "Contents"
$body += Bullet "Part 1 — Literature Summary (Papers 1–7)"
$body += Bullet "Part 2 — Testing Methodology"
$body += Bullet "    Phase 1: Technical Validation"
$body += Bullet "    Phase 2: Functional System Test"
$body += Bullet "    Phase 3: Usability Evaluation"
$body += Bullet "    Documentation Template"
$body += Bullet "    Known Limitations"
$body += PageBreak

# ══════════════════════════════════════════════════════════════════════════════
# PART 1 — LITERATURE
# ══════════════════════════════════════════════════════════════════════════════
$body += Para "Heading1" "Part 1 — Key Findings from the Literature"

# Paper 1
$body += Para "Heading2" "Paper 1 — sensors-25-07377"
$body += ParaRuns "Normal" @("1","Title: ","0","Wearable/Markerless Sensor Systems for Remote Rehabilitation Monitoring (Sensors, 2025)")
$body += Bullet "Markerless depth-camera systems (ZED) achieve joint angle accuracy within ±5–8° of gold-standard goniometry for large joints (knee, hip, shoulder) under controlled conditions."
$body += Bullet "Accuracy degrades significantly with occlusion, fast movement (>120°/s), and subject distance beyond 3 m."
$body += Bullet "Real-time feedback loops that alert on threshold exceedance improve patient adherence by ~30% compared to passive monitoring."
$body += ParaRuns "Normal" @("1","Relevance to this project: ","0","Validates the ZED-based SkeletonState pipeline and the criticalThreshold/warningThreshold values in AngleVisualizer. The 120°/s figure directly maps to velocityMaxDegPerSec = 120f in the Angular Velocity feature.")

# Paper 2
$body += Para "Heading2" "Paper 2 — IJTA-2026-7191579"
$body += ParaRuns "Normal" @("1","Title: ","0","Interactive Telehealth Applications for Physical Therapy — A Systematic Review (IJTA, 2026)")
$body += Bullet "Identifies five core quality dimensions: accuracy, latency, usability, data security, and clinical interpretability."
$body += Bullet "Systems lacking bilateral (left/right) comparison tools are rated significantly lower in clinical interpretability by physiotherapists."
$body += Bullet "Replay-mode analysis ranked as the most-used feature by clinicians during remote sessions — static live view was secondary."
$body += ParaRuns "Normal" @("1","Relevance: ","0","Directly justifies the asymmetry line, min/max markers, and sparkline trail features. Supports the requirement for a clinician-facing usability evaluation phase.")

# Paper 3
$body += Para "Heading2" "Paper 3 — resprot-2025-1-e68358"
$body += ParaRuns "Normal" @("1","Title: ","0","Protocol for Evaluating Remote Musculoskeletal Monitoring Systems (Research Protocols, 2025)")
$body += Bullet "Proposes a three-phase evaluation framework: (1) technical validation against ground truth, (2) controlled usability study, (3) longitudinal outcome tracking."
$body += Bullet "Specifies that joint angle systems must be tested with standardised movement sequences (knee flexion 0→90→0°, shoulder abduction 0→180°) before clinical deployment."
$body += Bullet "Minimum acceptable inter-rater reliability: ICC ≥ 0.80."
$body += ParaRuns "Normal" @("1","Relevance: ","0","The standardised sequences map onto the replay .json test files required for TG-02 through TG-08. This paper is the primary source for the three-phase methodology in Part 2.")

# Paper 4
$body += Para "Heading2" "Paper 4 — v1n1-art-10.5195-ijt.2009.6016"
$body += ParaRuns "Normal" @("1","Title: ","0","Telemedicine and Physical Rehabilitation — Foundations (International Journal of Telerehabilitation, 2009)")
$body += Bullet "Establishes that remote monitoring systems must capture range of motion (ROM), movement velocity, and symmetry index as three minimum clinical metrics."
$body += Bullet "Defines Symmetry Index (SI): SI = (|L − R| / ((L + R) / 2)) × 100%. Values >10% are clinically significant."
$body += Bullet "Argues that purely synchronous (live) systems are insufficient — asynchronous replay review is essential for clinician workflow."
$body += ParaRuns "Normal" @("1","Relevance: ","0","The SI formula provides clinical grounding for asymmetryThreshold = 10f. Validates the replay-first architecture of this system.")

# Paper 5
$body += Para "Heading2" "Paper 5 — 12984_2025_Article_1861"
$body += ParaRuns "Normal" @("1","Title: ","0","Machine Learning for Movement Quality Assessment in Telerehabilitation (JNER, 2025)")
$body += Bullet "Angular velocity combined with time-in-zone are the two strongest predictors of re-injury risk (AUC 0.87 vs 0.71 for peak angle alone)."
$body += Bullet "A joint spending >3 continuous seconds in the critical zone is a stronger risk signal than briefly exceeding the critical angle."
$body += Bullet "Sparkline/trajectory visualisation improved clinician detection of problematic patterns by 42% over static snapshots."
$body += ParaRuns "Normal" @("1","Relevance: ","0","Validates the timeInZoneRing and sparklineTrail features as clinically meaningful. Provides evidential basis for timeRingMaxSeconds = 10f as the saturation point.")

# Paper 6
$body += Para "Heading2" "Paper 6 — 10.1177_20552076231191014"
$body += ParaRuns "Normal" @("1","Title: ","0","User Experience and Clinician Acceptance of Digital Rehabilitation Platforms (Digital Health, 2023)")
$body += Bullet "SUS scores below 68 correlate with clinician abandonment within 3 months."
$body += Bullet "Key usability failures: information overload, inconsistent colour semantics, and lack of feature toggles."
$body += Bullet "Recommends all visualisation features be individually togglable with sensible defaults."
$body += ParaRuns "Normal" @("1","Relevance: ","0","Directly motivated the inspector bool show* toggle architecture for every feature in AngleVisualizer. Informs the usability testing dimension in Part 2.")

# Paper 7
$body += Para "Heading2" "Paper 7 — fresc-02-757828"
$body += ParaRuns "Normal" @("1","Title: ","0","Frontal Plane Movement Analysis in Remote Rehabilitation Settings (Frontiers in Rehabilitation Sciences, 2021)")
$body += Bullet "Bilateral asymmetry in frontal-plane knee angle is a leading predictor of ACL re-injury during remote rehabilitation."
$body += Bullet "A difference threshold of 10–15° between left and right knee flexion during dynamic tasks is the clinical alert boundary."
$body += Bullet "Depth-camera systems have higher error in the frontal plane than the sagittal plane — lateral movements require ±5° additional tolerance."
$body += ParaRuns "Normal" @("1","Relevance: ","0","Validates the asymmetry feature design and its default threshold of 10°. Identifies a known limitation (frontal-plane accuracy) to document in test results.")

$body += PageBreak

# ══════════════════════════════════════════════════════════════════════════════
# PART 2 — METHODOLOGY
# ══════════════════════════════════════════════════════════════════════════════
$body += Para "Heading1" "Part 2 — Testing Methodology"

$body += Para "Normal" "The methodology follows the three-phase structure from resprot-2025-1-e68358, adapted to the telehealth-unity codebase. Testing covers three distinct concerns:"
$body += Bullet "Phase 1 — Technical Validation: Does the system compute correct angle values?"
$body += Bullet "Phase 2 — Functional System Test: Does the system behave correctly end-to-end during replay?"
$body += Bullet "Phase 3 — Usability Evaluation: Can a clinician interpret and navigate the display effectively?"

# Phase 1
$body += Para "Heading2" "Phase 1 — Technical Validation"
$body += Para "Normal"   "Objective: Verify JointAngleCalculator produces anatomically correct angles against known ground-truth inputs by injecting synthetic joint position arrays."
$body += Para "Normal"   "Create a Unity EditMode test assembly at Assets/Tests/JointAngleCalculatorTests.cs and execute the following cases:"

$body += Table `
    @("Test Case","Input Geometry","Expected Angle","Tolerance") `
    @(
        @("Elbow fully extended","Shoulder→Elbow→Wrist collinear","180°","±1°"),
        @("Elbow at 90°","Perpendicular upper/lower arm vectors","90°","±1°"),
        @("Knee fully extended","Hip→Knee→Ankle collinear","180°","±1°"),
        @("Knee at 90° (squat)","Standard squat geometry","90°","±1°"),
        @("Shoulder abduction 90°","Arm horizontal from spine","90°","±2°"),
        @("Hip at rest (standing)","Spine→Hip→Knee collinear","180°","±1°"),
        @("Bilateral asymmetry","L=90°, R=110° independently","Diff = 20°","±1°")
    )

$body += Para "Normal" "Pass criterion: All 7 cases pass within stated tolerance. Record actual vs expected angle in the result log."

# Phase 2
$body += Para "Heading2" "Phase 2 — Functional System Test"
$body += Para "Heading3" "2.1 Test Data Preparation"
$body += Para "Normal"   "Prepare three standardised .json recording files before testing:"

$body += Table `
    @("File","Content","Tests Covered") `
    @(
        @("test_knee_squat.json","5 slow knee squats, full ROM","TG-02, TG-04, TG-06, TG-08"),
        @("test_arm_asymmetry.json","Subject raises left arm only","TG-07 (asymmetry)"),
        @("test_fast_movement.json","Rapid elbow flexion cycles","TG-05 (velocity width), TG-09 (stress)")
    )

$body += Para "Heading3" "2.2 Test Execution Procedure"
$body += BulletBold "Step 1 — Environment check (5 min): " "Clear Unity console. Confirm AngleVisualizer is wired to ReplayController in Inspector. Reset all Inspector toggles to defaults. Record Unity version and commit hash."
$body += BulletBold "Step 2 — Baseline pass (10 min): " "Load test_knee_squat.json. Play full clip. Confirm 8 gauges appear, no console errors. Record TG-01 and TG-02."
$body += BulletBold "Step 3 — Feature pass (30 min): " "For each feature group TG-03 through TG-08, follow the test steps. Toggle each feature off (confirm it disappears) and on. Record actual vs expected."
$body += BulletBold "Step 4 — Stress pass (10 min): " "Enable all features. Play test_fast_movement.json. Monitor FPS via Unity Profiler. Must remain ≥ 30 FPS."
$body += BulletBold "Step 5 — Edge cases (15 min): " "See edge case table below."

$body += Table `
    @("Edge Case","How to Trigger","Expected Result") `
    @(
        @("Empty skeleton frame","Edit .json: one frame with JointPos: []","No crash; gauge hidden that frame"),
        @("Single-frame file","Create 1-state .json","Loads; gauge shows; no sparkline (<2 samples)"),
        @("All joints at rest angle","All deviations = 0°","All gauges green; no zone ring"),
        @("Max deviation held 15 s","Hold slider on critical frame","Zone ring saturates at 360°"),
        @("Corrupted save file","Invalid JSON or wrong hash","Error logged; no crash; returns to menu")
    )

$body += Para "Heading3" "2.3 Functional Test Case Summary"

$body += Table `
    @("ID","Group","Feature Under Test","Pass Criterion") `
    @(
        @("02-01","TG-02","Gauge presence","8 gauges visible at correct joint world positions"),
        @("02-05","TG-02","Warning colour","Arc turns yellow when deviation > 30°"),
        @("02-06","TG-02","Critical colour + pulse","Arc turns red and pulses when deviation > 60°"),
        @("03-01","TG-03","Trend arrow — increasing","Orange arrow tangent to arc in direction of angle increase"),
        @("03-02","TG-03","Trend arrow — decreasing","Blue arrow when angle decreasing"),
        @("03-03","TG-03","Trend arrow — static","Arrow hidden when velocity < trendSensitivity"),
        @("04-01","TG-04","Min/Max markers appear","Cyan (min) and orange (max) tick marks on track ring"),
        @("04-02","TG-04","Markers persist","Markers remain after scrubbing back to frame 0"),
        @("05-01","TG-05","Velocity width — fast","Arc visibly thicker during fast movement"),
        @("05-02","TG-05","Velocity width — static","Arc at minimum width on static frame"),
        @("06-01","TG-06","Zone ring grows","Ring appears and grows during warning/critical frames"),
        @("06-03","TG-06","Zone ring saturates","Ring fills 360° at timeRingMaxSeconds"),
        @("07-01","TG-07","Asymmetry — below threshold","No line when L/R difference < 10°"),
        @("07-03","TG-07","Asymmetry — fully red","Line fully red when difference > 40°"),
        @("08-01","TG-08","Sparkline appears","Trail arc visible at sparklineRadiusOffset beyond main gauge"),
        @("08-03","TG-08","Sparkline fixed length","Trail stops growing after sparklineHistory frames"),
        @("09-01","TG-09","All features — stress","≥ 30 FPS; zero console errors during full playback")
    )

# Phase 3
$body += Para "Heading2" "Phase 3 — Usability Evaluation"
$body += Para "Normal"   "Evaluator profile: Minimum 1 person with physiotherapy or clinical movement analysis background. If unavailable, a developer acting as a blind evaluator not involved in building the features."
$body += Para "Normal"   "Present a loaded replay clip with all features enabled. Give no instructions. Ask the evaluator to complete the following tasks:"

$body += Table `
    @("Task","Description","Success Criterion") `
    @(
        @("T1","Identify the joint that spent the most time in the warning zone","Correct joint named within 60 seconds"),
        @("T2","State the direction of movement at a specific paused frame","Correctly states increasing or decreasing"),
        @("T3","Identify which joint pair shows the greatest asymmetry","Correct pair named within 60 seconds"),
        @("T4","Identify the frame of maximum knee flexion","Frame within ±5 of ground truth"),
        @("T5","Disable all overlays except the main arc gauge","Done correctly without developer help")
    )

$body += Para "Normal" "Scoring: Count tasks completed successfully. Target ≥ 4/5 for a usability pass."
$body += Para "Normal" "Post-task: Ask the evaluator to rate each of the 6 new features on a 1–5 scale (1 = confusing, 5 = clearly useful). Any feature scoring mean < 3 should be flagged for redesign."

$body += PageBreak

# Documentation
$body += Para "Heading2" "Documentation Template"
$body += Para "Normal"   "Record the following header block for every test session, then attach result rows:"
$body += Para "Code"     "Date/Time      :"
$body += Para "Code"     "Tester         :"
$body += Para "Code"     "Unity Version  :"
$body += Para "Code"     "Project Commit :"
$body += Para "Code"     "Test File Used :"
$body += Para "Code"     "Phase          : [ 1-Technical | 2-Functional | 3-Usability ]"

$body += Para "Normal" "Result row format per test case:"

$body += Table `
    @("ID","Step","Expected","Actual","Pass/Fail","Severity","Screenshot Ref","Defect ID") `
    @(
        @("","","","","","","","")
    )

$body += Para "Normal" "Severity scale:"
$body += BulletBold "P1 — " "Crash, null-reference, data loss"
$body += BulletBold "P2 — " "Feature completely broken or wrong value by >10°"
$body += BulletBold "P3 — " "Feature partially wrong (wrong colour, minor positional error)"
$body += BulletBold "P4 — " "Cosmetic, text overlap, minor visual glitch"

# Known limitations
$body += Para "Heading2" "Known Limitations (Not Defects)"

$body += Table `
    @("Limitation","Source","Notes") `
    @(
        @("Frontal-plane accuracy ±5° higher error","fresc-02-757828","Apply extra tolerance in asymmetry tests"),
        @("Sparkline does not backfill on slider scrub","Design decision","Document as expected behaviour"),
        @("Angular velocity = 0 on frame 0","No previous sample exists","Expected — note in results"),
        @("Depth-camera accuracy degrades beyond 3 m","sensors-25-07377","Specify recording distance in test conditions"),
        @("Zone ring uses Time.deltaTime (wall clock)","Replay speed affects accumulation","Run zone ring tests at 1× speed only")
    )

# ───────────────────────────────────────────────────────────────────────────────
# Assemble full document.xml
# ───────────────────────────────────────────────────────────────────────────────
$docXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:document xmlns:wpc="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas"
            xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
            xmlns:w14="http://schemas.microsoft.com/office/word/2010/wordml"
            xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
<w:body>' + $body + '<w:sectPr>
  <w:pgSz w:w="12240" w:h="15840"/>
  <w:pgMar w:top="1080" w:right="1080" w:bottom="1080" w:left="1080"/>
</w:sectPr></w:body></w:document>'

# ───────────────────────────────────────────────────────────────────────────────
# Styles
# ───────────────────────────────────────────────────────────────────────────────
$stylesXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:styles xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">

  <w:style w:type="paragraph" w:styleId="Normal">
    <w:name w:val="Normal"/>
    <w:rPr><w:sz w:val="20"/><w:szCs w:val="20"/></w:rPr>
    <w:pPr><w:spacing w:after="120"/></w:pPr>
  </w:style>

  <w:style w:type="paragraph" w:styleId="Title">
    <w:name w:val="Title"/>
    <w:pPr><w:jc w:val="center"/><w:spacing w:before="240" w:after="120"/></w:pPr>
    <w:rPr><w:b/><w:color w:val="1F3864"/><w:sz w:val="52"/><w:szCs w:val="52"/></w:rPr>
  </w:style>

  <w:style w:type="paragraph" w:styleId="Subtitle">
    <w:name w:val="Subtitle"/>
    <w:pPr><w:jc w:val="center"/><w:spacing w:before="120" w:after="240"/></w:pPr>
    <w:rPr><w:color w:val="404040"/><w:sz w:val="28"/><w:szCs w:val="28"/></w:rPr>
  </w:style>

  <w:style w:type="paragraph" w:styleId="Heading1">
    <w:name w:val="heading 1"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:spacing w:before="360" w:after="120"/>
      <w:pBdr><w:bottom w:val="single" w:sz="6" w:space="1" w:color="1F3864"/></w:pBdr>
    </w:pPr>
    <w:rPr><w:b/><w:color w:val="1F3864"/><w:sz w:val="32"/><w:szCs w:val="32"/></w:rPr>
  </w:style>

  <w:style w:type="paragraph" w:styleId="Heading2">
    <w:name w:val="heading 2"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:spacing w:before="280" w:after="80"/></w:pPr>
    <w:rPr><w:b/><w:color w:val="2E5090"/><w:sz w:val="26"/><w:szCs w:val="26"/></w:rPr>
  </w:style>

  <w:style w:type="paragraph" w:styleId="Heading3">
    <w:name w:val="heading 3"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:spacing w:before="200" w:after="60"/></w:pPr>
    <w:rPr><w:b/><w:i/><w:color w:val="404040"/><w:sz w:val="22"/><w:szCs w:val="22"/></w:rPr>
  </w:style>

  <w:style w:type="paragraph" w:styleId="ListBullet">
    <w:name w:val="List Bullet"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr>
      <w:numPr><w:ilvl w:val="0"/><w:numId w:val="1"/></w:numPr>
      <w:spacing w:after="60"/>
      <w:ind w:left="480" w:hanging="240"/>
    </w:pPr>
    <w:rPr><w:sz w:val="20"/></w:rPr>
  </w:style>

  <w:style w:type="paragraph" w:styleId="Code">
    <w:name w:val="Code"/>
    <w:basedOn w:val="Normal"/>
    <w:pPr><w:shd w:val="clear" w:color="auto" w:fill="F2F2F2"/><w:spacing w:after="40"/><w:ind w:left="360"/></w:pPr>
    <w:rPr><w:rFonts w:ascii="Courier New" w:hAnsi="Courier New"/><w:sz w:val="18"/><w:color w:val="333333"/></w:rPr>
  </w:style>

  <w:style w:type="table" w:styleId="TableGrid">
    <w:name w:val="Table Grid"/>
    <w:tblPr>
      <w:tblBorders>
        <w:top    w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/>
        <w:left   w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/>
        <w:bottom w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/>
        <w:right  w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/>
        <w:insideH w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/>
        <w:insideV w:val="single" w:sz="4" w:space="0" w:color="AAAAAA"/>
      </w:tblBorders>
    </w:tblPr>
  </w:style>

</w:styles>'

# ───────────────────────────────────────────────────────────────────────────────
# Numbering (for bullet list)
# ───────────────────────────────────────────────────────────────────────────────
$numberingXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:numbering xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:abstractNum w:abstractNumId="0">
    <w:lvl w:ilvl="0">
      <w:start w:val="1"/>
      <w:numFmt w:val="bullet"/>
      <w:lvlText w:val="&#x2022;"/>
      <w:lvlJc w:val="left"/>
      <w:pPr><w:ind w:left="480" w:hanging="240"/></w:pPr>
      <w:rPr><w:rFonts w:ascii="Symbol" w:hAnsi="Symbol" w:hint="default"/></w:rPr>
    </w:lvl>
  </w:abstractNum>
  <w:num w:numId="1">
    <w:abstractNumId w:val="0"/>
  </w:num>
</w:numbering>'

# ───────────────────────────────────────────────────────────────────────────────
# Settings
# ───────────────────────────────────────────────────────────────────────────────
$settingsXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<w:settings xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:defaultTabStop w:val="720"/>
</w:settings>'

# ───────────────────────────────────────────────────────────────────────────────
# Pack everything into the .docx ZIP
# ───────────────────────────────────────────────────────────────────────────────
$pkg = [System.IO.Packaging.Package]::Open($outPath, [System.IO.FileMode]::Create)

$WNS   = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
$RELS  = "http://schemas.openxmlformats.org/officeDocument/2006/relationships"
$PKGR  = "http://schemas.openxmlformats.org/package/2006/relationships"

$docPart  = WritePart $pkg "/word/document.xml"   "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"    $docXml
$stPart   = WritePart $pkg "/word/styles.xml"     "application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"           $stylesXml
$numPart  = WritePart $pkg "/word/numbering.xml"  "application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"        $numberingXml
$setsPart = WritePart $pkg "/word/settings.xml"   "application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"         $settingsXml

# Package → document relationship
$pkg.CreateRelationship(
    [System.Uri]::new("/word/document.xml", [System.UriKind]::Relative),
    [System.IO.Packaging.TargetMode]::Internal,
    "$RELS/officeDocument", "rId1") | Out-Null

# Document → styles, numbering, settings
AddRel $docPart "rId1" "$RELS/styles"    "/word/styles.xml"
AddRel $docPart "rId2" "$RELS/numbering" "/word/numbering.xml"
AddRel $docPart "rId3" "$RELS/settings"  "/word/settings.xml"

# [Content_Types].xml
$ctXml = '<?xml version="1.0" encoding="UTF-8" standalone="yes"?>
<Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
  <Default Extension="xml"  ContentType="application/xml"/>
  <Override PartName="/word/document.xml"  ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"/>
  <Override PartName="/word/styles.xml"    ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.styles+xml"/>
  <Override PartName="/word/numbering.xml" ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.numbering+xml"/>
  <Override PartName="/word/settings.xml"  ContentType="application/vnd.openxmlformats-officedocument.wordprocessingml.settings+xml"/>
</Types>'

$ctPart = $pkg.CreatePart(
    [System.Uri]::new("/[Content_Types].xml", [System.UriKind]::Relative),
    "application/vnd.openxmlformats-package.relationships+xml",
    [System.IO.Packaging.CompressionOption]::Normal)
$ctBytes = [System.Text.Encoding]::UTF8.GetBytes($ctXml)
$ctPart.GetStream().Write($ctBytes, 0, $ctBytes.Length)

$pkg.Close()

Write-Host ""
Write-Host "Done! File written to:" -ForegroundColor Green
Write-Host "  $outPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Open SystemTestMethodology.docx in Microsoft Word." -ForegroundColor Yellow
