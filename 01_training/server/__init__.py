"""
FastAPI inference server for PCB/wafer inspection pipeline.

This server provides ML model inference endpoints for:
- PatchCore anomaly detection  
- YOLOv8 segmentation
- Combined pipeline inference

Models are loaded once at startup for optimal performance.
"""