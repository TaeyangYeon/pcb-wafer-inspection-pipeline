"""
PatchCore: Industrial anomaly detection using patch-based features.

This package implements PatchCore, a state-of-the-art anomaly detection method
for industrial quality inspection, based on:
"Towards Total Recall in Industrial Anomaly Detection" (Roth et al., 2022)

Main components:
- PatchCore: High-level scikit-learn-style API
- PatchCoreFeatureExtractor: Feature extraction from pre-trained CNNs
- CoresetSampler: Greedy coreset subsampling for memory efficiency
- MemoryBank: k-NN search for anomaly scoring

Example usage:
    from patchcore import PatchCore
    
    # Training
    model = PatchCore(backbone="wide_resnet50_2", coreset_ratio=0.01)
    model.fit(normal_image_paths, category="transistor")
    model.save("models/transistor_model")
    
    # Inference
    model = PatchCore.load("models/transistor_model")
    score, anomaly_map = model.predict(test_image)
"""

# Note: PatchCore class requires PatchCoreFeatureExtractor to be implemented first
# from .patchcore import PatchCore
# from .feature_extractor import PatchCoreFeatureExtractor

from .coreset import CoresetSampler
from .memory_bank import MemoryBank

__all__ = [
    # "PatchCore",              # Commented until feature_extractor is implemented
    # "PatchCoreFeatureExtractor", 
    "CoresetSampler",
    "MemoryBank"
]

__version__ = "1.0.0"