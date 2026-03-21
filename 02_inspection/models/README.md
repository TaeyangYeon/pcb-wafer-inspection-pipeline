# Model Directory Structure

This directory contains trained models for the PCB and wafer inspection system.
The FastAPI server loads models from these subdirectories for inference.

## Directory Structure

```
02_inspection/models/
├── pcb_seg/              # YOLOv8-seg for PCB defect detection
│   └── best.onnx         # ONNX model (from Colab training)
├── patchcore/            # PatchCore anomaly detection models
│   ├── transistor/       # Transistor component anomaly detection
│   │   ├── memory_bank.npz
│   │   └── config.json
│   └── grid/             # Grid/trace anomaly detection
│       ├── memory_bank.npz
│       └── config.json
└── README.md             # This file
```

## Model Types

### 1. PCB Defect Segmentation (YOLOv8-seg)
- **Location**: `pcb_seg/best.onnx`
- **Purpose**: Detect and segment PCB defects (missing_hole, mouse_bite, etc.)
- **Source**: Colab notebook `01_training/notebooks/train_yolov8_seg.ipynb`
- **Input**: 640x640 RGB images
- **Output**: Bounding boxes + segmentation masks

### 2. Transistor Anomaly Detection (PatchCore)
- **Location**: `patchcore/transistor/`
- **Purpose**: Detect anomalies in transistor components
- **Source**: Program 1 training script
- **Method**: Feature embedding + coreset + Mahalanobis distance

### 3. Grid Anomaly Detection (PatchCore)
- **Location**: `patchcore/grid/`
- **Purpose**: Detect anomalies in grid/trace structures
- **Source**: Program 1 training script
- **Method**: Feature embedding + coreset + Mahalanobis distance

## Installation

After training completes:

1. **YOLOv8-seg Model**: Download `best.onnx` from Google Drive and place in `pcb_seg/`
2. **PatchCore Models**: Copy `memory_bank.npz` + `config.json` to respective directories

## FastAPI Integration

The FastAPI server (when implemented) will:
- Load `pcb_seg/best.onnx` for defect detection endpoints
- Load PatchCore models for anomaly detection endpoints
- Serve inference requests from the C# inspection application

## File Sizes

Expected total size: ~100-500 MB
- YOLOv8-seg ONNX: ~6-12 MB
- PatchCore models: ~50-200 MB each