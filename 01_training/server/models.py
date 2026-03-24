"""
Pydantic models for FastAPI request/response schemas.

These models ensure type safety and match exactly with the C# InspectionApiClient
request/response format for seamless integration.
"""
from typing import List, Literal, Optional
from pydantic import BaseModel, Field

class InspectRequest(BaseModel):
    """
    Request payload for POST /inspect endpoint.
    
    Matches C# InspectionApiClient.InspectAsync() request format.
    """
    image_path: str = Field(..., description="Full path to image file to inspect")
    mode: Literal["anomaly", "segment", "pipeline"] = Field(
        ..., 
        description="Inference mode: anomaly=PatchCore only, segment=YOLO only, pipeline=both stages"
    )

class DetectionItem(BaseModel):
    """
    Single detection result from YOLOv8 segmentation.
    
    Matches C# DetectionItem class structure exactly.
    """
    class_name: str = Field(..., description="PCB defect class name")
    confidence: float = Field(..., ge=0.0, le=1.0, description="Detection confidence score")
    bbox_x: int = Field(..., ge=0, description="Bounding box X coordinate")
    bbox_y: int = Field(..., ge=0, description="Bounding box Y coordinate") 
    bbox_w: int = Field(..., ge=0, description="Bounding box width")
    bbox_h: int = Field(..., ge=0, description="Bounding box height")
    mask_area: int = Field(..., ge=0, description="Segmentation mask area in pixels")

class InspectResponse(BaseModel):
    """
    Response payload for POST /inspect endpoint.
    
    Matches C# InspectionApiClient.InspectAsync() response format exactly.
    """
    final_result: Literal["OK", "ANOMALY", "NG"] = Field(
        ...,
        description="Overall inspection result: OK=pass, ANOMALY=unknown defect, NG=known defect"
    )
    stage_used: Literal["Anomaly", "Segment", "Pipeline"] = Field(
        ...,
        description="Which inference stage(s) were executed"
    )
    anomaly_score: float = Field(..., ge=0.0, description="PatchCore anomaly score")
    inference_time_ms: float = Field(..., ge=0.0, description="Total inference time in milliseconds")
    anomaly_map: Optional[List[float]] = Field(
        None, 
        description="Flattened anomaly heatmap (28x28=784 floats), null if not computed"
    )
    detections: List[DetectionItem] = Field(
        default_factory=list,
        description="List of YOLOv8 detections, empty if none found"
    )

class HealthResponse(BaseModel):
    """
    Response payload for GET /health endpoint.
    
    Provides server and model loading status for C# client health checks.
    """
    status: Literal["ok", "degraded", "down"] = Field(
        ...,
        description="Overall server status"
    )
    models_loaded: bool = Field(
        ...,
        description="True if all models loaded successfully at startup"
    )
    patchcore_ready: bool = Field(
        ...,
        description="True if PatchCore model is loaded and ready"
    )
    yolo_ready: bool = Field(
        ...,
        description="True if YOLOv8 ONNX model is loaded and ready"
    )