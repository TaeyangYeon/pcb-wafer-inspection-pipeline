# PCB Wafer Inspection Pipeline
A production-grade ML vision system with PatchCore anomaly detection and YOLOv8-seg classification for semiconductor quality control.

## Overview

This system implements a complete operational ML pipeline for PCB defect inspection, targeting semiconductor and electronics manufacturing. Unlike typical detection-only projects, this system implements a full operational pipeline: from unsupervised anomaly detection to segmentation classification, persistent logging, and statistical drift monitoring. The architecture demonstrates enterprise-ready patterns with server-client separation, ORM persistence, and real-time monitoring capabilities essential for industrial deployment.

## Architecture

```
[Image Folder] ---> [File Watcher] ---> [FastAPI Server]
                                            |
                               [PatchCore] + [YOLOv8-seg]
                                            |
                                  [C# Avalonia Client]
                                            |
                        [SQLite DB] + [Dashboard] + [Drift Monitor]
```

## Key Technical Highlights

### 1. PatchCore from Paper Implementation
Implemented PatchCore anomaly detection from the original SPADE paper rather than using pre-built libraries like anomalib. This demonstrates deep understanding of the algorithm internals, memory bank construction, and nearest-neighbor search optimization. The implementation handles feature extraction, coreset selection, and anomaly scoring with full control over hyperparameters.

### 2. Two-Stage Inspection Pipeline  
Designed a cascaded inspection system where PatchCore performs initial anomaly screening, followed by YOLOv8-seg for precise defect classification and localization. This approach mirrors real industrial workflows where fast screening precedes detailed analysis, optimizing both speed and accuracy for production environments.

### 3. Enterprise Data Management + Drift Monitoring
Implemented Entity Framework Core with repository pattern for robust data persistence, including audit trails and inspection history. Statistical drift monitoring using Kolmogorov-Smirnov tests detects model degradation over time, a critical requirement for maintaining quality in production manufacturing systems.

### 4. Production-Grade C# Architecture
Built with SOLID principles, MVVM pattern, dependency injection, and comprehensive NUnit testing (92 tests, 0 failures). The Avalonia UI provides cross-platform desktop deployment while maintaining separation of concerns between presentation, business logic, and data layers.

## Tech Stack

| Component | Technology |
|-----------|------------|
| **Python ML Pipeline** | |
| Anomaly Detection | PatchCore (from paper) |
| Segmentation | YOLOv8-seg, ONNX |
| Server Framework | FastAPI, Uvicorn |
| ML Libraries | PyTorch, OpenCV, scikit-learn |
| **C# Application** | |
| UI Framework | Avalonia (.NET 10) |
| Database | Entity Framework Core, SQLite |
| Architecture | MVVM, Repository Pattern, DI |
| Testing | NUnit, MOQ |

## Training Results

### YOLOv8-seg Performance (PCB Dataset)
| Metric | Score |
|--------|-------|
| Box mAP50 | 0.9609 |
| Box mAP50-95 | 0.5167 |
| Mask mAP50 | 0.9147 |
| Mask mAP50-95 | 0.3741 |
| Inference Speed | 5.0ms (T4 GPU) |
| Model Size | 6.5MB |

**Per-Class Mask mAP50:**
- missing_hole: 0.969
- mouse_bite: 0.905  
- open_circuit: 0.887
- short: 0.924
- spur: 0.865
- spurious_copper: 0.939

### PatchCore Performance
| Metric | Score |
|--------|-------|
| AUROC | TBD (Colab training in progress) |
| Memory Usage | TBD |
| Inference Speed | TBD |

## Test Coverage

- **Test Suite:** 92 NUnit tests, 0 failures
- **Core Coverage:** 70.5% 
- **Integration Scenarios:** 5 end-to-end workflows
- **Test Categories:** Unit, Integration, Repository, ViewModel, Edge Cases

## Project Structure

```
pcb-wafer-inspection-pipeline/
├── 01_training/
│   ├── streamlit_app.py
│   ├── server/
│   └── models/
├── 02_inspection/
│   ├── InspectionPipeline.Core/
│   ├── InspectionPipeline.UI/
│   └── InspectionPipeline.Tests/
└── datasets/
```

## How to Run

### Program 1 - Training Pipeline
```bash
cd 01_training
pip install -r requirements.txt
streamlit run streamlit_app.py
```

### FastAPI Inference Server
```bash
cd 01_training
uvicorn server.main:app --reload --port 8000
```

### Program 2 - Inspection System
```bash
cd 02_inspection
dotnet restore
dotnet run --project InspectionPipeline.UI
```

## Key Design Decisions

### 1. PatchCore from Paper Implementation
Chose to implement PatchCore from scratch rather than using anomalib to demonstrate algorithmic understanding and maintain full control over memory bank optimization and feature extraction processes.

### 2. Two-Stage Pipeline Architecture
Designed PatchCore as a fast anomaly screener followed by YOLOv8-seg for classification, mirroring industrial workflows where initial screening precedes detailed analysis for optimal throughput.

### 3. FastAPI + C# Separation
Separated Python ML inference from C# application logic to leverage each language's strengths: Python for ML ecosystem access, C# for robust desktop application development and enterprise patterns.

### 4. Entity Framework Core + Repository Pattern
Implemented full ORM with repository abstraction to enable testability, maintainability, and potential database migration without business logic changes.

### 5. KS-Test Drift Monitoring
Chose Kolmogorov-Smirnov statistical tests for model drift detection as they provide non-parametric distribution comparison suitable for varied defect types in manufacturing environments.

## Comparison with Project 1

| Dimension | Project 1 (vision-inspection-portfolio) | Project 2 (pcb-wafer-inspection-pipeline) |
|-----------|------------------------------------------|-------------------------------------------|
| **ML Task** | YOLOv8 object detection | PatchCore anomaly + YOLOv8-seg |
| **Architecture** | Single monolithic C# app | Server-client pipeline |
| **Persistence** | Basic file logging | EF Core ORM + Repository |
| **Monitoring** | Manual inspection | Statistical drift detection |
| **Input Trigger** | Manual file selection | Automated file watcher |

## Target Applications

- **Semiconductor Wafer Inspection:** Automated defect detection in wafer fabrication processes
- **PCB Quality Control:** Surface mount and trace defect identification in electronics manufacturing  
- **Industrial Vision Systems:** Template for production-grade ML vision deployments requiring both anomaly detection and classification capabilities