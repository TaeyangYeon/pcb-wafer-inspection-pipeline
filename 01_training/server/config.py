"""
Configuration settings for the FastAPI inference server.

Model paths, thresholds, and server settings configurable via environment variables.
"""
import os
from pathlib import Path

# Server configuration
PORT = int(os.getenv("INFERENCE_SERVER_PORT", "8502"))

# Model paths
PATCHCORE_MODEL_DIR = Path(os.getenv(
    "PATCHCORE_MODEL_DIR", 
    "models/patchcore/transistor/"
))

YOLO_ONNX_PATH = Path(os.getenv(
    "YOLO_ONNX_PATH",
    "../02_inspection/models/pcb_seg/best.onnx"
))

# Inference thresholds
ANOMALY_THRESHOLD = float(os.getenv("ANOMALY_THRESHOLD", "0.5"))
YOLO_CONF_THRESHOLD = float(os.getenv("YOLO_CONF_THRESHOLD", "0.25"))

# PatchCore configuration
PATCHCORE_MEMORY_BANK_FILE = "memory_bank.npz"
PATCHCORE_CONFIG_FILE = "config.json"

# YOLOv8 configuration
YOLO_INPUT_SIZE = (640, 640)  # Standard YOLOv8 input size
ANOMALY_MAP_SIZE = (28, 28)   # PatchCore output size

# PCB class names (must match training order)
PCB_CLASS_NAMES = [
    "missing_hole",
    "mouse_bite", 
    "open_circuit",
    "short",
    "spur",
    "spurious_copper"
]

def get_patchcore_memory_bank_path() -> Path:
    """Get full path to PatchCore memory bank file."""
    return PATCHCORE_MODEL_DIR / PATCHCORE_MEMORY_BANK_FILE

def get_patchcore_config_path() -> Path:
    """Get full path to PatchCore config file."""
    return PATCHCORE_MODEL_DIR / PATCHCORE_CONFIG_FILE

def validate_model_paths() -> tuple[bool, list[str]]:
    """
    Validate that all required model files exist.
    
    Returns:
        (all_exist, missing_files): True if all exist, list of missing files
    """
    missing_files = []
    
    # Check PatchCore files
    if not get_patchcore_memory_bank_path().exists():
        missing_files.append(str(get_patchcore_memory_bank_path()))
    
    if not get_patchcore_config_path().exists():
        missing_files.append(str(get_patchcore_config_path()))
    
    # Check YOLO ONNX file
    if not YOLO_ONNX_PATH.exists():
        missing_files.append(str(YOLO_ONNX_PATH))
    
    return len(missing_files) == 0, missing_files