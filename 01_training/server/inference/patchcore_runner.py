"""
PatchCore inference runner for FastAPI server.

Wraps PatchCore anomaly detection for efficient server use with proper error handling,
timing, and format conversion for C# client compatibility.
"""
import time
from pathlib import Path
from typing import Dict, Any

import numpy as np
from PIL import Image

# Import PatchCore from src directory
import sys
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent.parent.parent / "src"))

from patchcore.patchcore import PatchCore


class PatchCoreRunner:
    """
    Wrapper for PatchCore inference optimized for FastAPI server use.
    
    Takes a pre-loaded PatchCore instance and provides a clean interface
    for single image inference with timing and format conversion.
    """
    
    def __init__(self, patchcore: PatchCore, threshold: float = 0.5):
        """
        Initialize runner with loaded PatchCore instance.
        
        Args:
            patchcore: Pre-loaded PatchCore model instance
            threshold: Anomaly threshold for binary classification (default: 0.5)
        """
        if not isinstance(patchcore, PatchCore):
            raise TypeError("patchcore must be a PatchCore instance")
        
        if not patchcore.is_trained:
            raise ValueError("PatchCore instance must be trained/loaded before use")
        
        self._patchcore = patchcore
        self._threshold = float(threshold)
    
    @property
    def threshold(self) -> float:
        """Get current anomaly threshold."""
        return self._threshold
    
    @threshold.setter 
    def threshold(self, value: float):
        """Set anomaly threshold."""
        if value < 0.0 or value > 1.0:
            raise ValueError("Threshold must be between 0.0 and 1.0")
        self._threshold = float(value)
    
    def run(self, image_path: str) -> Dict[str, Any]:
        """
        Run PatchCore inference on a single image.
        
        Args:
            image_path: Full path to image file
            
        Returns:
            Dictionary with inference results:
            {
                "anomaly_score": float,           # Anomaly score 0.0-1.0
                "anomaly_map": list[float],       # Flattened 28x28 anomaly map (784 values)
                "is_anomalous": bool,             # True if score > threshold
                "inference_time_ms": float        # Inference time in milliseconds
            }
            
        Raises:
            FileNotFoundError: If image file doesn't exist
            ValueError: If image cannot be opened or processed
            RuntimeError: If PatchCore inference fails
        """
        start_time = time.time()
        
        # Validate image file exists
        image_file = Path(image_path)
        if not image_file.exists():
            raise FileNotFoundError(f"Image file not found: {image_path}")
        
        try:
            # Load image with PIL
            image = Image.open(image_file)
            
            # Ensure RGB format (PatchCore expects RGB)
            if image.mode != 'RGB':
                image = image.convert('RGB')
                
        except Exception as e:
            raise ValueError(f"Failed to load image {image_path}: {str(e)}")
        
        try:
            # Run PatchCore inference
            anomaly_score, anomaly_map = self._patchcore.predict(image)
            
            # Validate outputs
            if not isinstance(anomaly_score, (int, float)):
                raise RuntimeError(f"Invalid anomaly_score type: {type(anomaly_score)}")
                
            if not isinstance(anomaly_map, np.ndarray):
                raise RuntimeError(f"Invalid anomaly_map type: {type(anomaly_map)}")
                
            if anomaly_map.shape != (28, 28):
                raise RuntimeError(f"Invalid anomaly_map shape: {anomaly_map.shape}, expected (28, 28)")
            
        except Exception as e:
            raise RuntimeError(f"PatchCore inference failed for {image_path}: {str(e)}")
        
        # Calculate inference time
        inference_time_ms = (time.time() - start_time) * 1000
        
        # Normalize anomaly map to 0.0-1.0 range and flatten
        # PatchCore outputs can have varying ranges, normalize for consistent visualization
        anomaly_map_normalized = anomaly_map.copy()
        
        # Handle edge case where map is all zeros or constant
        map_min = anomaly_map_normalized.min()
        map_max = anomaly_map_normalized.max()
        
        if map_max > map_min:
            anomaly_map_normalized = (anomaly_map_normalized - map_min) / (map_max - map_min)
        else:
            # If map is constant, set to zero
            anomaly_map_normalized = np.zeros_like(anomaly_map_normalized)
        
        # Flatten to list for JSON serialization (28*28 = 784 floats)
        anomaly_map_flat = anomaly_map_normalized.flatten().tolist()
        
        # Binary anomaly classification
        is_anomalous = float(anomaly_score) > self._threshold
        
        return {
            "anomaly_score": float(anomaly_score),
            "anomaly_map": anomaly_map_flat,
            "is_anomalous": is_anomalous,
            "inference_time_ms": inference_time_ms
        }