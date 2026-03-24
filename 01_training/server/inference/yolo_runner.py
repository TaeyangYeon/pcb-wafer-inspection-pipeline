"""
YOLOv8-seg inference runner for FastAPI server.

Uses Ultralytics YOLO class for reliable ONNX model loading and inference
with proper format conversion for C# client compatibility.
"""
import time
from pathlib import Path
from typing import Dict, List, Any

import numpy as np
from ultralytics import YOLO


class YoloSegRunner:
    """
    Wrapper for YOLOv8-seg inference optimized for FastAPI server use.
    
    Uses Ultralytics YOLO class to load and run ONNX models with
    detection parsing and format conversion for C# integration.
    """
    
    # PCB defect class names (must match training order)
    CLASS_NAMES = [
        'missing_hole',
        'mouse_bite', 
        'open_circuit',
        'short',
        'spur',
        'spurious_copper'
    ]
    
    def __init__(self, onnx_path: str, conf_threshold: float = 0.25):
        """
        Initialize runner with ONNX model.
        
        Args:
            onnx_path: Path to YOLOv8-seg ONNX model file
            conf_threshold: Confidence threshold for detections (default: 0.25)
        """
        onnx_file = Path(onnx_path)
        if not onnx_file.exists():
            raise FileNotFoundError(f"ONNX model not found: {onnx_path}")
        
        if not (0.0 <= conf_threshold <= 1.0):
            raise ValueError("conf_threshold must be between 0.0 and 1.0")
        
        try:
            # Load YOLO model from ONNX file
            self._model = YOLO(str(onnx_file))
            self._conf_threshold = float(conf_threshold)
            self._onnx_path = str(onnx_file)
            
        except Exception as e:
            raise RuntimeError(f"Failed to load YOLO model from {onnx_path}: {str(e)}")
    
    @property
    def conf_threshold(self) -> float:
        """Get current confidence threshold."""
        return self._conf_threshold
    
    @conf_threshold.setter
    def conf_threshold(self, value: float):
        """Set confidence threshold."""
        if not (0.0 <= value <= 1.0):
            raise ValueError("conf_threshold must be between 0.0 and 1.0")
        self._conf_threshold = float(value)
    
    def run(self, image_path: str) -> Dict[str, Any]:
        """
        Run YOLOv8-seg inference on a single image.
        
        Args:
            image_path: Full path to image file
            
        Returns:
            Dictionary with inference results:
            {
                "detections": list[dict],     # List of DetectionItem-compatible dicts
                "inference_time_ms": float,   # Inference time in milliseconds
                "detection_count": int        # Number of detections found
            }
            
        Raises:
            FileNotFoundError: If image file doesn't exist
            RuntimeError: If YOLO inference fails
        """
        start_time = time.time()
        
        # Validate image file exists
        image_file = Path(image_path)
        if not image_file.exists():
            raise FileNotFoundError(f"Image file not found: {image_path}")
        
        try:
            # Run YOLO inference with confidence threshold
            results = self._model.predict(
                source=str(image_file),
                conf=self._conf_threshold,
                save=False,
                verbose=False
            )
            
            # Parse results into DetectionItem format
            detections = self._parse_results(results)
            
        except Exception as e:
            raise RuntimeError(f"YOLO inference failed for {image_path}: {str(e)}")
        
        # Calculate inference time
        inference_time_ms = (time.time() - start_time) * 1000
        
        return {
            "detections": detections,
            "inference_time_ms": inference_time_ms,
            "detection_count": len(detections)
        }
    
    def _parse_results(self, results) -> List[Dict[str, Any]]:
        """
        Parse Ultralytics YOLO results into DetectionItem-compatible format.
        
        Args:
            results: Ultralytics Results object list
            
        Returns:
            List of detection dictionaries with DetectionItem fields:
            {
                "class_name": str,
                "confidence": float,
                "bbox_x": int, "bbox_y": int, "bbox_w": int, "bbox_h": int,
                "mask_area": int
            }
        """
        detections = []
        
        # Handle case where no results or empty results
        if not results or len(results) == 0:
            return detections
        
        # Get first result (single image inference)
        result = results[0]
        
        # Check if we have boxes (detections found)
        if result.boxes is None or len(result.boxes) == 0:
            return detections
        
        # Extract detection data
        boxes = result.boxes.xyxy.cpu().numpy()  # xyxy format
        confidences = result.boxes.conf.cpu().numpy()
        class_ids = result.boxes.cls.cpu().numpy().astype(int)
        
        # Extract masks if available
        masks = None
        if hasattr(result, 'masks') and result.masks is not None:
            masks = result.masks.data.cpu().numpy()  # Shape: (N, H, W)
        
        # Process each detection
        for i in range(len(boxes)):
            class_id = class_ids[i]
            confidence = float(confidences[i])
            
            # Get class name (validate class_id is within bounds)
            if 0 <= class_id < len(self.CLASS_NAMES):
                class_name = self.CLASS_NAMES[class_id]
            else:
                class_name = f"unknown_{class_id}"
            
            # Convert bbox from xyxy to xywh format
            x1, y1, x2, y2 = boxes[i]
            bbox_x = int(x1)
            bbox_y = int(y1) 
            bbox_w = int(x2 - x1)
            bbox_h = int(y2 - y1)
            
            # Calculate mask area
            mask_area = 0
            if masks is not None and i < len(masks):
                mask = masks[i]
                mask_area = int(np.count_nonzero(mask))
            
            detection = {
                "class_name": class_name,
                "confidence": confidence,
                "bbox_x": bbox_x,
                "bbox_y": bbox_y,
                "bbox_w": bbox_w,
                "bbox_h": bbox_h,
                "mask_area": mask_area
            }
            
            detections.append(detection)
        
        return detections