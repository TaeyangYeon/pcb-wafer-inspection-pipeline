"""
2-Stage Inspection Pipeline Runner for FastAPI server.

Implements the core architectural feature of the portfolio project:
a production-quality 2-stage ML pipeline combining anomaly detection
and segmentation for comprehensive PCB defect inspection.

Pipeline Logic:
1. Stage 1 (PatchCore): Fast anomaly filter
   - Score < threshold: PASS (OK) immediately  
   - Score >= threshold: SUSPECT, proceed to Stage 2

2. Stage 2 (YOLOv8-seg): Detailed defect classification
   - Has detections: NG (known defect with details)
   - No detections: ANOMALY (unknown defect type)

This mirrors real-world inspection logic: fast screening first,
detailed analysis second, optimizing both speed and accuracy.
"""
from typing import Dict, Any, Literal

from .patchcore_runner import PatchCoreRunner
from .yolo_runner import YoloSegRunner


class PipelineRunner:
    """
    2-Stage ML inspection pipeline combining PatchCore and YOLOv8-seg.
    
    Implements production-quality inspection logic with configurable
    execution modes for different use cases and performance requirements.
    """
    
    def __init__(self, patchcore_runner: PatchCoreRunner, yolo_runner: YoloSegRunner):
        """
        Initialize pipeline with pre-configured model runners.
        
        Args:
            patchcore_runner: Configured PatchCore anomaly detection runner
            yolo_runner: Configured YOLOv8-seg segmentation runner
        """
        if not isinstance(patchcore_runner, PatchCoreRunner):
            raise TypeError("patchcore_runner must be PatchCoreRunner instance")
        
        if not isinstance(yolo_runner, YoloSegRunner):
            raise TypeError("yolo_runner must be YoloSegRunner instance")
        
        self._patchcore_runner = patchcore_runner
        self._yolo_runner = yolo_runner
    
    def run(self, image_path: str, mode: str = "pipeline") -> Dict[str, Any]:
        """
        Execute inspection pipeline based on specified mode.
        
        Args:
            image_path: Full path to image file
            mode: Execution mode - "anomaly", "segment", or "pipeline"
            
        Returns:
            Complete inspection result dictionary:
            {
                "final_result": "OK" | "NG" | "ANOMALY",
                "stage_used": "Anomaly" | "Segment" | "Pipeline", 
                "anomaly_score": float,
                "inference_time_ms": float,
                "anomaly_map": list[float] | None,
                "detections": list[dict]
            }
            
        Pipeline Logic:
            mode="anomaly": Stage 1 only (PatchCore), early return
            mode="segment": Stage 2 only (YOLOv8-seg), skip Stage 1
            mode="pipeline": Full 2-stage inspection (default)
            
        Final Result Mapping:
            Stage 1 pass (score < threshold)     -> "OK", "Anomaly"
            Stage 2 has detections               -> "NG", "Pipeline" 
            Stage 2 no detections               -> "ANOMALY", "Pipeline"
        """
        if mode not in ["anomaly", "segment", "pipeline"]:
            raise ValueError(f"Invalid mode: {mode}. Must be 'anomaly', 'segment', or 'pipeline'")
        
        total_inference_time = 0.0
        anomaly_score = 0.0
        anomaly_map = None
        detections = []
        
        # Mode: Anomaly only (Stage 1 only)
        if mode == "anomaly":
            patchcore_result = self._patchcore_runner.run(image_path)
            
            return {
                "final_result": "ANOMALY" if patchcore_result["is_anomalous"] else "OK",
                "stage_used": "Anomaly",
                "anomaly_score": patchcore_result["anomaly_score"],
                "inference_time_ms": patchcore_result["inference_time_ms"],
                "anomaly_map": patchcore_result["anomaly_map"],
                "detections": []  # Empty for anomaly-only mode
            }
        
        # Mode: Segment only (Stage 2 only)
        if mode == "segment":
            yolo_result = self._yolo_runner.run(image_path)
            
            return {
                "final_result": "NG" if yolo_result["detection_count"] > 0 else "OK",
                "stage_used": "Segment", 
                "anomaly_score": 0.0,  # Not computed in segment-only mode
                "inference_time_ms": yolo_result["inference_time_ms"],
                "anomaly_map": None,  # Not available in segment-only mode
                "detections": yolo_result["detections"]
            }
        
        # Mode: Pipeline (Full 2-stage)
        if mode == "pipeline":
            # Stage 1: PatchCore anomaly detection
            patchcore_result = self._patchcore_runner.run(image_path)
            anomaly_score = patchcore_result["anomaly_score"] 
            anomaly_map = patchcore_result["anomaly_map"]
            total_inference_time += patchcore_result["inference_time_ms"]
            
            # Decision point: proceed to Stage 2?
            if not patchcore_result["is_anomalous"]:
                # Stage 1 PASS: anomaly score below threshold
                # Return immediately with OK result
                return {
                    "final_result": "OK",
                    "stage_used": "Anomaly",  # Only Stage 1 was needed
                    "anomaly_score": anomaly_score,
                    "inference_time_ms": total_inference_time,
                    "anomaly_map": anomaly_map,
                    "detections": []  # No Stage 2, so no detections
                }
            
            # Stage 1 SUSPECT: anomaly score above threshold
            # Proceed to Stage 2: YOLOv8-seg detailed analysis
            yolo_result = self._yolo_runner.run(image_path)
            detections = yolo_result["detections"]
            total_inference_time += yolo_result["inference_time_ms"]
            
            # Stage 2 decision: known defect vs unknown anomaly
            if yolo_result["detection_count"] > 0:
                # Known defect detected with classification details
                final_result = "NG"
            else:
                # No known defects found, but anomaly score was high
                # This indicates an unknown/novel defect type
                final_result = "ANOMALY"
            
            return {
                "final_result": final_result,
                "stage_used": "Pipeline",  # Both stages were executed
                "anomaly_score": anomaly_score,
                "inference_time_ms": total_inference_time,
                "anomaly_map": anomaly_map,
                "detections": detections
            }
        
        # This should never be reached due to validation above
        raise RuntimeError(f"Unhandled mode: {mode}")
    
    @property
    def anomaly_threshold(self) -> float:
        """Get current PatchCore anomaly threshold."""
        return self._patchcore_runner.threshold
    
    @anomaly_threshold.setter
    def anomaly_threshold(self, value: float):
        """Set PatchCore anomaly threshold."""
        self._patchcore_runner.threshold = value
    
    @property
    def confidence_threshold(self) -> float:
        """Get current YOLO confidence threshold."""
        return self._yolo_runner.conf_threshold
    
    @confidence_threshold.setter 
    def confidence_threshold(self, value: float):
        """Set YOLO confidence threshold."""
        self._yolo_runner.conf_threshold = value