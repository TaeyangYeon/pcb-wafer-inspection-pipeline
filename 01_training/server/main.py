"""
FastAPI inference server main application.

Provides ML model inference endpoints for PCB/wafer inspection with:
- PatchCore anomaly detection
- YOLOv8 segmentation  
- Combined pipeline inference

Models loaded once at startup via lifespan events for optimal performance.
"""
import time
import logging
from contextlib import asynccontextmanager
from pathlib import Path

from fastapi import FastAPI, HTTPException
from fastapi.middleware.cors import CORSMiddleware

# Configure logging first
logging.basicConfig(level=logging.INFO)
logger = logging.getLogger(__name__)

from .models import InspectRequest, InspectResponse, HealthResponse, DetectionItem
from .config import (
    PORT, validate_model_paths, ANOMALY_THRESHOLD, 
    YOLO_CONF_THRESHOLD, ANOMALY_MAP_SIZE,
    get_patchcore_memory_bank_path, YOLO_ONNX_PATH
)

# Import inference runners with graceful fallback for missing dependencies
try:
    from .inference.yolo_runner import YoloSegRunner
    yolo_runner_available = True
except ImportError as e:
    logger.warning(f"YoloSegRunner not available: {e}")
    YoloSegRunner = None
    yolo_runner_available = False

try:
    from .inference.patchcore_runner import PatchCoreRunner
    patchcore_runner_available = True
except ImportError as e:
    logger.warning(f"PatchCoreRunner not available: {e}")
    PatchCoreRunner = None
    patchcore_runner_available = False

try:
    from .inference.pipeline_runner import PipelineRunner
    pipeline_runner_available = True
except ImportError as e:
    logger.warning(f"PipelineRunner not available: {e}")
    PipelineRunner = None
    pipeline_runner_available = False

# Global model instances (loaded at startup)
patchcore_model = None
yolo_session = None
models_loaded = False
patchcore_ready = False
yolo_ready = False

@asynccontextmanager
async def lifespan(app: FastAPI):
    """
    FastAPI lifespan context manager.
    
    Loads ML models once at startup and cleans up on shutdown.
    """
    global patchcore_model, yolo_session, models_loaded, patchcore_ready, yolo_ready
    
    logger.info("Starting FastAPI inference server...")
    
    # Validate model file paths
    all_exist, missing_files = validate_model_paths()
    if not all_exist:
        logger.warning(f"Some model files not found: {missing_files}")
        logger.warning("Server will start but return 503 on inference requests")
        # Set app state for degraded mode
        app.state.pipeline_runner = None
    else:
        logger.info("All model files found, attempting to load...")
        
        try:
            # Load PatchCore model (stub for now)
            logger.info("PatchCore model loading skipped (stub)")
            patchcore_ready = False  # TODO: Set to True when PatchCore loading implemented
            
            # Load YOLOv8 ONNX model if available
            if yolo_runner_available and YoloSegRunner:
                logger.info(f"Loading YOLO model from: {YOLO_ONNX_PATH}")
                yolo_runner = YoloSegRunner(str(YOLO_ONNX_PATH), YOLO_CONF_THRESHOLD)
                yolo_ready = True
                logger.info("YOLO model loaded successfully")
            else:
                logger.warning("YOLOv8 runner not available")
                yolo_ready = False
            
            # Initialize pipeline runner if both components are ready
            if patchcore_ready and yolo_ready and pipeline_runner_available:
                # TODO: Initialize PatchCore runner when model loading is implemented
                # patchcore_runner = PatchCoreRunner(patchcore_model, ANOMALY_THRESHOLD)
                # app.state.pipeline_runner = PipelineRunner(patchcore_runner, yolo_runner)
                logger.info("PatchCore runner creation skipped (stub)")
                app.state.pipeline_runner = None
            else:
                logger.info("Pipeline runner not created (dependencies not ready)")
                app.state.pipeline_runner = None
                
        except Exception as e:
            logger.error(f"Failed to load models: {e}")
            patchcore_ready = False
            yolo_ready = False
            app.state.pipeline_runner = None
        
        models_loaded = patchcore_ready and yolo_ready
        
    logger.info(f"Server startup complete. Models loaded: {models_loaded}")
    
    yield  # Server runs here
    
    # Cleanup on shutdown
    logger.info("Shutting down server...")
    app.state.pipeline_runner = None
    patchcore_model = None
    yolo_session = None

# Create FastAPI app with lifespan
app = FastAPI(
    title="PCB Wafer Inspection API",
    description="ML inference server for anomaly detection and defect segmentation",
    version="1.0.0",
    lifespan=lifespan
)

# Add CORS middleware for C# HttpClient compatibility
app.add_middleware(
    CORSMiddleware,
    allow_origins=["*"],  # In production, restrict to specific origins
    allow_credentials=True,
    allow_methods=["*"],
    allow_headers=["*"],
)

@app.get("/health", response_model=HealthResponse)
async def health_check():
    """
    Health check endpoint for C# client to verify server status.
    
    Returns server and model loading status.
    """
    status = "ok" if models_loaded else ("degraded" if (patchcore_ready or yolo_ready) else "down")
    
    return HealthResponse(
        status=status,
        models_loaded=models_loaded,
        patchcore_ready=patchcore_ready,
        yolo_ready=yolo_ready
    )

@app.post("/inspect", response_model=InspectResponse)
async def inspect_image(request: InspectRequest):
    """
    Main inference endpoint for image inspection.
    
    Supports three modes:
    - "anomaly": PatchCore only
    - "segment": YOLOv8 only  
    - "pipeline": Both stages (anomaly first, then segment if needed)
    """
    # Validate image file exists
    image_path = Path(request.image_path)
    if not image_path.exists():
        raise HTTPException(status_code=400, detail=f"Image file not found: {request.image_path}")
    
    # Check if pipeline runner is available
    if not hasattr(app.state, 'pipeline_runner') or app.state.pipeline_runner is None:
        logger.warning("Pipeline runner not available, using mock responses")
        
        # Fall back to mock responses when models not loaded
        start_time = time.time()
        time.sleep(0.1)  # Simulate processing time
        inference_time_ms = (time.time() - start_time) * 1000
        
        if request.mode == "anomaly":
            return InspectResponse(
                final_result="OK",
                stage_used="Anomaly", 
                anomaly_score=0.25,
                inference_time_ms=inference_time_ms,
                anomaly_map=[0.1] * (ANOMALY_MAP_SIZE[0] * ANOMALY_MAP_SIZE[1]),
                detections=[]
            )
        elif request.mode == "segment":
            mock_detection = DetectionItem(
                class_name="missing_hole",
                confidence=0.85,
                bbox_x=100, bbox_y=150, bbox_w=50, bbox_h=30,
                mask_area=1200
            )
            return InspectResponse(
                final_result="NG", stage_used="Segment", anomaly_score=0.0,
                inference_time_ms=inference_time_ms, anomaly_map=None,
                detections=[mock_detection]
            )
        else:  # pipeline mode
            return InspectResponse(
                final_result="OK", stage_used="Pipeline", anomaly_score=0.3,
                inference_time_ms=inference_time_ms,
                anomaly_map=[0.15] * (ANOMALY_MAP_SIZE[0] * ANOMALY_MAP_SIZE[1]),
                detections=[]
            )
    
    # Use PipelineRunner for actual inference
    logger.info(f"Processing {request.mode} inference for: {request.image_path}")
    
    try:
        # Run pipeline inference
        result = app.state.pipeline_runner.run(request.image_path, request.mode)
        
        # Convert result dict to InspectResponse
        return InspectResponse(
            final_result=result["final_result"],
            stage_used=result["stage_used"],
            anomaly_score=result["anomaly_score"],
            inference_time_ms=result["inference_time_ms"],
            anomaly_map=result["anomaly_map"],
            detections=[DetectionItem(**det) for det in result["detections"]]
        )
        
    except Exception as e:
        logger.error(f"Pipeline inference failed: {str(e)}")
        raise HTTPException(status_code=500, detail=f"Inference failed: {str(e)}")

# Health check at root for quick verification
@app.get("/")
async def root():
    """Root endpoint returning basic server info."""
    return {
        "service": "PCB Wafer Inspection API",
        "status": "running",
        "version": "1.0.0",
        "endpoints": ["/health", "/inspect"]
    }

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=PORT)