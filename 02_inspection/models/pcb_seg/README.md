# PCB Defect Segmentation Model

This directory contains the trained YOLOv8-seg model for PCB defect detection.

## Expected Files

Place the following file here after Colab training completes:

- `best.onnx` - YOLOv8n-seg model exported with opset=21
  - Input: [1, 3, 640, 640] (NCHW format)
  - Output: Detection boxes + segmentation masks
  - Classes: missing_hole, mouse_bite, open_circuit, short, spur, spurious_copper

## Training Source

This model is trained using:
- Notebook: `01_training/notebooks/train_yolov8_seg.ipynb`
- Dataset: PCB defect dataset (17,366 train + 4,298 val objects)
- Format: Pascal VOC XML converted to YOLO-seg polygon format

## Usage

The FastAPI server will load `best.onnx` from this directory for inference.
The C# inspection system will call the FastAPI endpoints for defect detection.

## File Size

Expected model size: ~6-12 MB (YOLOv8n is lightweight)