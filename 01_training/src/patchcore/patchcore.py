"""
PatchCore: Main facade class for anomaly detection.

Integrates feature extraction, coreset sampling, and memory bank operations
into a simple scikit-learn-style fit/predict API.

Based on:
"Towards Total Recall in Industrial Anomaly Detection" (Roth et al., 2022)

This class provides a high-level interface for PatchCore anomaly detection,
hiding the complexity of the underlying components.
"""

import json
import time
import cv2
import numpy as np
from pathlib import Path
from typing import Union, Tuple, List, Optional
from PIL import Image
from tqdm import tqdm

from .feature_extractor import PatchCoreFeatureExtractor
from .coreset import CoresetSampler
from .memory_bank import MemoryBank


class PatchCore:
    """
    PatchCore anomaly detection model.
    
    A scikit-learn-style interface for training and inference with PatchCore.
    Combines feature extraction, coreset sampling, and k-NN memory bank
    into a unified API.
    
    Example usage:
        # Training
        patchcore = PatchCore(backbone="wide_resnet50_2", coreset_ratio=0.01)
        patchcore.fit(normal_image_paths, category="transistor")
        patchcore.save("models/transistor_patchcore")
        
        # Inference
        patchcore = PatchCore.load("models/transistor_patchcore")
        score, anomaly_map = patchcore.predict(test_image)
    """
    
    def __init__(self,
                 backbone: str = "wide_resnet50_2",
                 coreset_ratio: float = 0.01,
                 device: str = "cpu"):
        """
        Initialize PatchCore model.
        
        Args:
            backbone: Feature extractor backbone (e.g., "wide_resnet50_2")
            coreset_ratio: Fraction of patches to keep in coreset (0.01 to 0.1)
            device: Device to run on ("cpu" or "cuda")
        """
        self.backbone = backbone
        self.coreset_ratio = coreset_ratio
        self.device = device
        
        # Components (initialized in fit())
        self.feature_extractor = None
        self.memory_bank = None
        
        # Training metadata
        self.input_size = (224, 224)  # Standard PatchCore input size
        self.feature_dim = None
        self.memory_bank_size = None
        self.category = "unknown"
        self.trained_at = None
        
        # State
        self._is_trained = False
    
    def fit(self, 
            normal_image_paths: List[Path],
            category: str = "unknown") -> None:
        """
        Train PatchCore model on normal images.
        
        Extracts features from all normal images, performs coreset subsampling,
        and builds the memory bank for anomaly detection.
        
        Args:
            normal_image_paths: List of paths to normal training images
            category: Category name for this model (e.g., "transistor")
        """
        if not normal_image_paths:
            raise ValueError("No training images provided")
        
        self.category = category
        start_time = time.time()
        
        print(f"Training PatchCore on {len(normal_image_paths)} normal images...")
        print(f"Backbone: {self.backbone}, Coreset ratio: {self.coreset_ratio}")
        
        # Initialize feature extractor
        self.feature_extractor = PatchCoreFeatureExtractor(
            backbone_name=self.backbone,
            device=self.device
        )
        
        # Extract features from all training images
        print("Extracting features...")
        all_features = []
        
        with tqdm(normal_image_paths, desc="Feature extraction") as pbar:
            for image_path in pbar:
                try:
                    # Load and preprocess image
                    image = Image.open(image_path).convert('RGB')
                    image = image.resize(self.input_size, Image.LANCZOS)
                    
                    # Extract patch features
                    features = self.feature_extractor.extract_features(image)
                    all_features.append(features)
                    
                    pbar.set_postfix({
                        'patches': features.shape[0],
                        'features': features.shape[1]
                    })
                    
                except Exception as e:
                    print(f"Warning: Failed to process {image_path}: {e}")
                    continue
        
        if not all_features:
            raise RuntimeError("No features extracted from training images")
        
        # Concatenate all features
        print("Concatenating features...")
        all_features = np.vstack(all_features)
        
        print(f"Total patches: {len(all_features):,}")
        print(f"Feature dimension: {all_features.shape[1]}")
        
        # Store feature dimension
        self.feature_dim = all_features.shape[1]
        
        # Perform coreset subsampling
        print("Performing coreset subsampling...")
        sampler = CoresetSampler(ratio=self.coreset_ratio, seed=42)
        coreset_features = sampler.fit(all_features)
        
        print(f"Coreset size: {len(coreset_features):,}")
        
        # Build memory bank
        print("Building memory bank...")
        self.memory_bank = MemoryBank()
        self.memory_bank.build(coreset_features)
        
        # Store training metadata
        self.memory_bank_size = self.memory_bank.size
        self.trained_at = time.strftime("%Y-%m-%d %H:%M:%S")
        self._is_trained = True
        
        # Training summary
        training_time = time.time() - start_time
        print(f"\nTraining complete!")
        print(f"  Images processed: {len(normal_image_paths)}")
        print(f"  Total patches: {len(all_features):,}")
        print(f"  Coreset size: {len(coreset_features):,}")
        print(f"  Memory bank size: {self.memory_bank_size}")
        print(f"  Training time: {training_time:.2f}s")
    
    def predict(self, image: Image.Image) -> Tuple[float, np.ndarray]:
        """
        Predict anomaly score and map for a single image.
        
        Args:
            image: PIL Image to analyze
            
        Returns:
            tuple containing:
            - anomaly_score: Image-level anomaly score (higher = more anomalous)
            - anomaly_map: Pixel-level anomaly map of shape (28, 28)
        """
        if not self.is_trained:
            raise RuntimeError("Model must be trained before prediction")
        
        # Resize image to standard input size
        image_resized = image.resize(self.input_size, Image.LANCZOS)
        
        # Extract patch features
        patch_features = self.feature_extractor.extract_features(image_resized)
        
        # Score using memory bank
        anomaly_score, anomaly_map = self.memory_bank.score(patch_features)
        
        return anomaly_score, anomaly_map
    
    def predict_with_heatmap(self, 
                           image: Image.Image,
                           original_size: Optional[Tuple[int, int]] = None
                           ) -> Tuple[float, np.ndarray]:
        """
        Predict with anomaly heatmap upsampled to original size.
        
        Args:
            image: PIL Image to analyze
            original_size: Target size (width, height) for anomaly map.
                          If None, returns (28, 28) map.
        
        Returns:
            tuple containing:
            - anomaly_score: Image-level anomaly score
            - anomaly_heatmap: Upsampled anomaly map
        """
        # Get base prediction
        anomaly_score, anomaly_map = self.predict(image)
        
        # Upsample if requested
        if original_size is not None:
            width, height = original_size
            # OpenCV expects (height, width) order
            anomaly_heatmap = cv2.resize(
                anomaly_map,
                (width, height),
                interpolation=cv2.INTER_LINEAR
            )
        else:
            anomaly_heatmap = anomaly_map
        
        return anomaly_score, anomaly_heatmap
    
    def save(self, output_dir: Union[str, Path]) -> None:
        """
        Save trained model to directory.
        
        Saves both the memory bank (.npz) and model configuration (.json).
        
        Args:
            output_dir: Directory to save model files
        """
        if not self.is_trained:
            raise RuntimeError("Model must be trained before saving")
        
        output_dir = Path(output_dir)
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Save memory bank
        memory_bank_path = output_dir / "memory_bank.npz"
        self.memory_bank.save(memory_bank_path)
        
        # Save configuration
        config = {
            "backbone": self.backbone,
            "coreset_ratio": self.coreset_ratio,
            "device": self.device,
            "input_size": self.input_size,
            "feature_dim": self.feature_dim,
            "memory_bank_size": self.memory_bank_size,
            "category": self.category,
            "trained_at": self.trained_at
        }
        
        config_path = output_dir / "config.json"
        with open(config_path, 'w', encoding='utf-8') as f:
            json.dump(config, f, indent=2, ensure_ascii=False)
        
        print(f"Model saved to: {output_dir}")
        print(f"  Memory bank: {memory_bank_path}")
        print(f"  Config: {config_path}")
    
    @classmethod
    def load(cls, model_dir: Union[str, Path]) -> "PatchCore":
        """
        Load trained model from directory.
        
        Args:
            model_dir: Directory containing saved model files
            
        Returns:
            Loaded PatchCore model ready for inference
        """
        model_dir = Path(model_dir)
        
        if not model_dir.exists():
            raise FileNotFoundError(f"Model directory not found: {model_dir}")
        
        # Load configuration
        config_path = model_dir / "config.json"
        if not config_path.exists():
            raise FileNotFoundError(f"Config file not found: {config_path}")
        
        try:
            with open(config_path, 'r', encoding='utf-8') as f:
                config = json.load(f)
        except Exception as e:
            raise RuntimeError(f"Failed to load config: {e}")
        
        # Create model instance
        model = cls(
            backbone=config["backbone"],
            coreset_ratio=config["coreset_ratio"],
            device=config.get("device", "cpu")
        )
        
        # Restore metadata
        model.input_size = tuple(config["input_size"])
        model.feature_dim = config["feature_dim"]
        model.memory_bank_size = config["memory_bank_size"]
        model.category = config["category"]
        model.trained_at = config["trained_at"]
        
        # Initialize feature extractor
        model.feature_extractor = PatchCoreFeatureExtractor(
            backbone_name=model.backbone,
            device=model.device
        )
        
        # Load memory bank
        memory_bank_path = model_dir / "memory_bank.npz"
        if not memory_bank_path.exists():
            raise FileNotFoundError(f"Memory bank file not found: {memory_bank_path}")
        
        model.memory_bank = MemoryBank()
        model.memory_bank.load(memory_bank_path)
        
        model._is_trained = True
        
        print(f"Model loaded from: {model_dir}")
        print(f"  Category: {model.category}")
        print(f"  Backbone: {model.backbone}")
        print(f"  Memory bank size: {model.memory_bank_size}")
        print(f"  Trained at: {model.trained_at}")
        
        return model
    
    @property
    def is_trained(self) -> bool:
        """Check if model has been trained."""
        return self._is_trained and self.memory_bank is not None
    
    def __repr__(self) -> str:
        """String representation of PatchCore model."""
        if self.is_trained:
            return (f"PatchCore(backbone={self.backbone}, "
                   f"category={self.category}, "
                   f"memory_bank_size={self.memory_bank_size})")
        return f"PatchCore(backbone={self.backbone}, untrained)"


def test_patchcore():
    """Basic test for PatchCore functionality."""
    import tempfile
    import os
    
    # Create synthetic test images
    test_images = []
    for i in range(3):
        # Create random RGB image
        array = np.random.randint(0, 256, (224, 224, 3), dtype=np.uint8)
        image = Image.fromarray(array)
        
        # Save to temp file
        temp_file = tempfile.NamedTemporaryFile(suffix='.jpg', delete=False)
        image.save(temp_file.name)
        test_images.append(Path(temp_file.name))
    
    try:
        # Test training
        model = PatchCore(backbone="wide_resnet50_2", coreset_ratio=0.1)
        model.fit(test_images, category="test")
        
        assert model.is_trained, "Model should be trained after fit()"
        
        # Test prediction
        test_image = Image.fromarray(
            np.random.randint(0, 256, (224, 224, 3), dtype=np.uint8)
        )
        
        score, anomaly_map = model.predict(test_image)
        assert isinstance(score, float), f"Expected float score, got {type(score)}"
        assert anomaly_map.shape == (28, 28), f"Expected (28, 28) map, got {anomaly_map.shape}"
        
        # Test heatmap upsampling
        score2, heatmap = model.predict_with_heatmap(test_image, original_size=(256, 256))
        assert score == score2, "Scores should match"
        assert heatmap.shape == (256, 256), f"Expected (256, 256) heatmap, got {heatmap.shape}"
        
        # Test save/load
        with tempfile.TemporaryDirectory() as temp_dir:
            model.save(temp_dir)
            
            model2 = PatchCore.load(temp_dir)
            assert model2.is_trained, "Loaded model should be trained"
            
            score3, _ = model2.predict(test_image)
            assert abs(score - score3) < 1e-5, "Scores should match after save/load"
        
        print("PatchCore test passed!")
        
    finally:
        # Cleanup temp files
        for img_path in test_images:
            if os.path.exists(img_path):
                os.unlink(img_path)


if __name__ == "__main__":
    test_patchcore()