# PCB & Wafer Vision Inspection Pipeline - Context Note
## Why Every Decision Was Made This Way

This file exists so that Claude (or any AI assistant) can re-read it and
immediately understand the direction, constraints, and reasoning behind every
decision. If context is lost, upload this file first.

---

## Who Is Building This and Why

- 3-year machine vision engineer at an automation equipment company
- No deep learning project experience at work (traditional vision only)
- Building personal portfolio projects to pivot into AI-applied roles
- Target companies: Samsung/SK semiconductor vendors, SFA, Hanwha, AI vision startups
- This is the second portfolio project (Project 1 is complete)
- Development machine: Intel Mac
- Training environment: Google Colab T4 GPU (no local GPU)

---

## What Project 1 Already Demonstrated

Project 1 (vision-inspection-portfolio) covered:
- YOLOv8 fine-tuning (supervised object detection)
- Custom GradCAM with PyTorch hooks (no external library)
- ONNX export + C# ONNX Runtime inference
- Streamlit training UI (5 tabs)
- Avalonia UI desktop app
- SOLID + MVVM + DI architecture in C#
- NUnit TDD (150 tests, all passing)
- MVTec AD dataset (bottle + tile categories)

Everything in Project 2 must be visibly different from Project 1.
A recruiter should see two projects and conclude:
"This person knows both models AND systems. They can build AND operate."

---

## Why This Project Is Structured as a Pipeline (Not a Single App)

Project 1 was a self-contained app: load image → run ONNX → show result.
That demonstrates model integration skill, not system design skill.

Real manufacturing inspection systems are pipelines:
  camera trigger → preprocessing → inference server → result routing → DB logging → dashboard → alarm

Project 2 simulates that pipeline structure:
  File Watcher (camera sim) → FastAPI server (inference) → C# client (UI + DB + dashboard)

This separation matters because:
1. It shows understanding of service decomposition
2. It demonstrates ability to think about operational concerns (what if server is down?)
3. The 2-stage pipeline (anomaly first, segmentation second) mirrors real production logic
4. It gives a concrete architecture diagram that is easy to explain in interviews

The File Watcher simulates a camera trigger because:
- We have no physical camera
- A folder watch is a standard industrial pattern (many SCADA systems work this way)
- It is demonstrable in a video without special hardware

---

## Why PatchCore Was Chosen for Anomaly Detection

Options considered:
1. PatchCore (chosen)
2. FastFlow
3. EfficientAD
4. Simple autoencoder

PatchCore was chosen because:
- It is the most cited method on MVTec AD benchmark
- It is conceptually explainable in an interview: "patch-level feature memory bank"
- It requires NO training in the traditional sense - only normal images needed
- The implementation requires real understanding: timm backbone, coreset subsampling,
  k-NN distance scoring, anomaly map upsampling
- It produces both a score AND a pixel-level heatmap (anomaly map), which is visually
  compelling in a demo
- It is implementable without GPU at inference time (memory bank lookup is CPU-friendly)

The key interview point: "I trained PatchCore from the paper, not from a library like anomalib.
I implemented the coreset subsampling myself using greedy farthest-point selection."

Why NOT anomalib library:
- anomalib would hide the implementation, same problem as using Ultralytics for everything
- We want to show paper implementation skill, not library usage skill
- However, we can reference anomalib results for benchmark comparison in README

---

## Why YOLOv8-seg Was Chosen for Segmentation

Options considered:
1. YOLOv8-seg (chosen)
2. Mask R-CNN
3. SAM (Segment Anything Model)

YOLOv8-seg was chosen because:
- Directly comparable to Project 1's YOLOv8 detection (same family, clearly different task)
- ONNX export is well-supported (same proven export path as Project 1)
- Fast enough for real-time use
- Pixel mask output enables defect area measurement (interview talking point)
- Familiar training pipeline (already know Ultralytics from Project 1)

The key upgrade from Project 1: output is a pixel mask, not just a bounding box.
This means we can report: "defect area = 234 pixels (0.3% of total surface)"
That is a meaningful quality metric that bounding boxes cannot provide.

---

## Why the 2-Stage Pipeline Logic

Stage 1: PatchCore anomaly detection (fast, no label needed)
Stage 2: YOLOv8-seg (only runs if Stage 1 flags SUSPECT)

This mirrors real production thinking:
- In a real line, you run the cheapest check first
- If it passes, move on (no wasted compute)
- If it fails, escalate to detailed analysis

Interview explanation:
"The anomaly detector acts as a gate. It catches both known and unknown defect types.
If it flags something, the segmentation model identifies the specific defect class and
measures the affected area. This two-stage approach minimizes false negatives while
keeping compute cost manageable."

This also handles a real problem:
- Segmentation model only knows the 6 PCB defect classes it was trained on
- Unknown defects would be missed by segmentation alone
- PatchCore catches anything that looks different from normal, including unknown defects

---

## Why FastAPI Instead of Direct ONNX in C#

In Project 1, inference ran directly in C# via ONNX Runtime. That was a valid choice there.
For Project 2, PatchCore cannot be easily ported to pure C# ONNX because:
- PatchCore requires a memory bank (numpy array, ~100MB)
- The k-NN scoring logic is non-trivial in C#
- The anomaly map upsampling uses scipy/cv2 operations

So the architecture is:
- PatchCore: Python FastAPI server (runs locally)
- YOLOv8-seg: ONNX in FastAPI server (could also be in C# but keeping consistent)
- C# client: HTTP calls to FastAPI, handles UI + DB + dashboard

This is also more realistic:
Real production systems often separate the inference server from the HMI (Human Machine Interface).
The C# app IS the HMI. The FastAPI server IS the inference engine.

Interview point: "I separated inference from the HMI layer because in real lines,
the vision computer and the operator console are often different machines.
This architecture reflects that separation."

---

## Why EF Core + SQLite

Project 1 used CSV export only. That is not a database. It cannot be queried.
Project 2 adds proper persistence because:
- Every inspection record should be queryable (filter by date, result, model version)
- The drift monitor needs historical score data to compute KS-test
- The dashboard needs aggregated statistics (NG rate per hour, throughput trend)
- CSV cannot support any of these without loading the entire file each time

Why SQLite (not PostgreSQL, not SQL Server):
- Runs embedded, no separate server process needed
- Portable: the .db file is the database, easy to demo
- EF Core supports it fully with migrations
- Appropriate for a single-machine inspection station (which is the use case)

Why EF Core (not Dapper, not raw ADO.NET):
- Code-first migrations: schema lives in C# code, version-controlled
- InMemory provider: enables proper unit testing without a real database file
- Repository pattern: IInspectionRepository can be mocked in ViewModel tests
- Demonstrates modern .NET data access, relevant to enterprise C# roles

The Repository Pattern is mandatory here:
- ViewModels must NEVER reference DbContext directly
- ViewModels depend on IInspectionRepository (interface)
- InspectionRepository wraps DbContext
- Tests mock IInspectionRepository
- This is the only way to test ViewModels without spinning up a real database

---

## Why Drift Monitoring Was Kept (But Simplified)

Auto-retraining was originally proposed but removed because:
- Requires persistent GPU server (we only have Colab, which has no persistent process)
- Cannot demo auto-retraining in a portfolio context convincingly

What was kept:
- KS-test on anomaly score distribution (detects score drift over time)
- "Retraining Recommended" alert in the UI when drift is detected
- README documents: "when this alert fires, run the Colab notebook to retrain"

Why this is still valuable:
- Drift monitoring is a real MLOps concept
- The KS-test implementation is non-trivial (statistical comparison of distributions)
- The alert + documentation shows the engineer understands the FULL lifecycle,
  even if the retraining step is manual
- In an interview: "I detect drift automatically. Retraining is a manual trigger
  because our GPU is on-demand, but in a production environment with a dedicated
  GPU server, this would trigger the training job automatically."

---

## Why Intel Mac Constraints Matter

- No local GPU: all heavy training on Google Colab T4
- Avalonia instead of WPF: WPF is Windows-only, Avalonia runs on Intel Mac
- OpenCvSharp4.runtime.osx.10.15-x64: Intel-specific runtime package
  (NOT the arm64 package, NOT the generic osx package)
- ONNX Runtime CPU-only: no CUDA, no DirectML, pure CPU inference
- This is documented as a feature: "optimized for CPU inference, deployable on
  inspection stations without GPU"

---

## Why TDD / NUnit Is Non-Negotiable

Project 1 ended with 150 NUnit tests. Project 2 targets 120+.
TDD is a core personal value demonstrated across both projects.

The discipline:
- Write the interface first
- Write the test against the interface (red)
- Implement the service (green)
- Refactor

This matters for the portfolio because:
- Shows software engineering discipline, not just ML hacking
- Target companies (SFA, Hanwha, semiconductor vendors) run critical equipment
  where software failures are costly
- "150 tests, 0 failures" is a concrete, verifiable claim

Test-specific rules:
- NEVER reference DbContext in ViewModel tests (use IInspectionRepository mock)
- ALWAYS use EF InMemory provider for Repository unit tests (not SQLite file)
- ALWAYS use real SQLite in a temp directory for integration tests
- HttpClient in InspectionApiClient must be injectable (MockHttpMessageHandler pattern)
- FileWatcherService must use an abstracted filesystem interface for testability

---

## Naming and Style Conventions

### C# Naming
- NUnit test methods: MethodName_StateUnderTest_ExpectedBehavior (same as Project 1)
- Interfaces: prefix I (IInspectionRepository, IDriftMonitor)
- Entity classes (EF Core): no suffix (InspectionRecord, DefectDetail)
- ViewModel classes: suffix ViewModel (DashboardViewModel)
- Service classes: suffix Service (InspectionApiClient is an exception - it is a client)

### Python Naming
- snake_case for all files and functions
- Classes: PascalCase
- NO emojis in any Python source file (causes UnicodeDecodeError with Claude Code)
- Always UTF-8 encoding explicitly

### UI Theme
- Dark theme: Catppuccin Mocha palette (same as Project 1 for visual consistency)
  Background: #1E1E2E / Surface: #181825 / Overlay: #313244
  Green (OK): #A6E3A1 / Red (NG): #F38BA8 / Yellow (SUSPECT): #F9E2AF
  Blue (accent): #89B4FA / Text: #CDD6F4

---

## Known Issues Inherited from Project 1 (Avoid These)

1. Python file encoding error: Never use emojis in Python source files
2. ONNX opset: Always export with opset=21 (not default 22)
   model.export(format='onnx', opset=21, simplify=True, imgsz=640)
3. OpenCvSharp4 runtime: Intel Mac needs osx.10.15-x64, NOT arm64
4. Mat construction: Use Mat.FromPixelData(), not Mat constructor
5. GetArray for 3-channel: Use GetArray<Vec3b>(), not GetArray<byte>()
6. NuGet DNS: macOS DNS must be 8.8.8.8 (not default ISP DNS)
7. Letterbox padding: fill value must be 114, image placed top-left
8. Preprocessing: raw pixels from ImDecode + BGR2RGB, NEVER encoded PNG bytes

---

## Interview Talking Points (Pre-loaded)

### On PatchCore
"I implemented PatchCore from the original paper without using anomalib.
The key insight is that you don't need defect labels - you only need normal images.
During training, you extract patch-level features from a pretrained backbone,
reduce them via greedy coreset subsampling to keep the memory bank manageable,
then at inference time you score each patch by its distance to the nearest
stored feature. The result is a pixel-level anomaly map."

### On 2-Stage Pipeline
"The first stage uses PatchCore as a gate. It is fast and catches both known and
unknown defect types. If it raises a flag, the second stage runs YOLOv8-seg to
identify the specific defect class and measure the affected pixel area.
This mirrors how a human inspector would work: first a quick visual scan,
then a detailed examination of suspect areas."

### On EF Core + SQLite
"Every inspection record is persisted to SQLite through Entity Framework Core.
I used the repository pattern so that the ViewModels depend on an interface,
not the DbContext directly. This makes ViewModel testing straightforward - I just
inject a mock repository. For database tests, I use EF Core's InMemory provider
for unit tests and a real SQLite file in a temp directory for integration tests."

### On Drift Monitoring
"The system accumulates anomaly scores in the database. Periodically, it runs a
KS-test comparing the current score distribution to the baseline established
during initial deployment. When the distributions diverge significantly, the UI
shows a retraining recommendation alert. In this portfolio, retraining is triggered
manually via Colab, but the architecture is designed so that a persistent GPU server
could replace that step with an automated training job."

### On Why Two Projects Are Different
"The first project demonstrates that I can build and deploy an AI model.
The second project demonstrates that I can design and operate an AI system.
The first project is model-centric. The second project is operations-centric.
Together they show the full lifecycle: build → deploy → monitor."

---

## What Claude Code Needs to Know When Starting Each Component

When giving Claude Code a prompt, always include:
1. The interface definition (copy from this plan)
2. The test that must pass (write the test first)
3. The constraint list (Intel Mac, ONNX opset 21, no emojis, UTF-8)
4. Reference to corresponding component in Project 1 if pattern should be reused

Claude Code works best with:
- One component per prompt
- Interface + test + implementation in one request
- Explicit "this follows the same pattern as X in Project 1"

---

## Repository Information

- GitHub user: TaeyangYeon
- Email: acrobatyeon@gmail.com
- Project 1 repo: https://github.com/TaeyangYeon/vision-inspection-portfolio
- Project 2 repo: to be created as vision-inspection-pipeline (or similar)
- Local path: ~/vision-inspection-pipeline/ (to be confirmed)

---

## Status Tracking Format (Daily Progress)

### Day N - STATUS
What was completed. What tests pass. What is the next step.

---

Last updated: Project start (planning phase complete)
Next: Day 1 - Python environment setup + PCB dataset download + data_manager.py
