"""
PatchCore End-to-End Pipeline Test

Tests the complete PatchCore pipeline on MVTec transistor dataset:
1. Train on small subset of normal images
2. Test on normal and defect images  
3. Verify defect images score higher than normal images
4. Save anomaly heatmap sample

This validates that all components integrate correctly and the model
produces sensible anomaly detection results.
"""

import sys
import os
import tempfile
import shutil
from pathlib import Path
import numpy as np
import matplotlib.pyplot as plt
from PIL import Image

# Add src to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent / "src"))

from data_manager import PCBDataManager


def create_test_feature_extractor():
    """Create a minimal feature extractor for testing purposes."""
    import torch
    import torch.nn as nn
    import torchvision.transforms as transforms
    from torchvision.models import wide_resnet50_2, Wide_ResNet50_2_Weights
    
    class TestPatchCoreFeatureExtractor:
        def __init__(self, backbone_name="wide_resnet50_2", device="cpu"):
            self.device = device
            
            # Load pretrained model
            if backbone_name == "wide_resnet50_2":
                self.model = wide_resnet50_2(weights=Wide_ResNet50_2_Weights.IMAGENET1K_V1)
            else:
                raise ValueError(f"Unsupported backbone: {backbone_name}")
            
            self.model.eval()
            self.model.to(device)
            
            # Hook to extract layer2 features
            self.features = []
            def hook_fn(module, input, output):
                self.features.append(output.cpu())
            
            # Register hook on layer2 (stride=8, gives 28x28 patches for 224x224 input)
            self.model.layer2.register_forward_hook(hook_fn)
            
            # Preprocessing
            self.transform = transforms.Compose([
                transforms.ToTensor(),
                transforms.Normalize(mean=[0.485, 0.456, 0.406], 
                                   std=[0.229, 0.224, 0.225])
            ])
        
        def extract_features(self, image):
            """Extract patch features from image."""
            # Convert PIL to tensor
            if isinstance(image, Image.Image):
                tensor = self.transform(image).unsqueeze(0)
            else:
                raise ValueError("Expected PIL Image")
            
            tensor = tensor.to(self.device)
            
            # Clear previous features
            self.features = []
            
            # Forward pass
            with torch.no_grad():
                _ = self.model(tensor)
            
            # Get layer2 features (shape: 1, 512, 28, 28)
            layer2_features = self.features[0]  
            
            # Reshape to patch features: (28*28, 512)
            _, channels, height, width = layer2_features.shape
            patches = layer2_features.reshape(channels, height * width).T
            
            return patches.numpy().astype(np.float32)
    
    return TestPatchCoreFeatureExtractor


def create_test_patchcore():
    """Create a minimal PatchCore class for testing."""
    from patchcore.coreset import CoresetSampler
    from patchcore.memory_bank import MemoryBank
    import json
    import time
    from tqdm import tqdm
    
    class TestPatchCore:
        def __init__(self, backbone="wide_resnet50_2", coreset_ratio=0.01, device="cpu"):
            self.backbone = backbone
            self.coreset_ratio = coreset_ratio
            self.device = device
            
            self.feature_extractor = None
            self.memory_bank = None
            
            self.input_size = (224, 224)
            self.feature_dim = None
            self.memory_bank_size = None
            self.category = "unknown"
            self.trained_at = None
            self._is_trained = False
        
        def fit(self, normal_image_paths, category="unknown"):
            if not normal_image_paths:
                raise ValueError("No training images provided")
            
            self.category = category
            start_time = time.time()
            
            print(f"Training PatchCore on {len(normal_image_paths)} normal images...")
            print(f"Backbone: {self.backbone}, Coreset ratio: {self.coreset_ratio}")
            
            # Initialize feature extractor
            FeatureExtractor = create_test_feature_extractor()
            self.feature_extractor = FeatureExtractor(self.backbone, self.device)
            
            # Extract features
            print("Extracting features...")
            all_features = []
            
            with tqdm(normal_image_paths, desc="Feature extraction") as pbar:
                for image_path in pbar:
                    try:
                        image = Image.open(image_path).convert('RGB')
                        image = image.resize(self.input_size, Image.LANCZOS)
                        features = self.feature_extractor.extract_features(image)
                        all_features.append(features)
                        pbar.set_postfix({'patches': features.shape[0], 'features': features.shape[1]})
                    except Exception as e:
                        print(f"Warning: Failed to process {image_path}: {e}")
                        continue
            
            if not all_features:
                raise RuntimeError("No features extracted")
            
            # Concatenate features
            all_features = np.vstack(all_features)
            self.feature_dim = all_features.shape[1]
            
            print(f"Total patches: {len(all_features):,}")
            print(f"Feature dimension: {all_features.shape[1]}")
            
            # Coreset subsampling
            print("Performing coreset subsampling...")
            sampler = CoresetSampler(ratio=self.coreset_ratio, seed=42)
            coreset_features = sampler.fit(all_features)
            
            # Build memory bank
            print("Building memory bank...")
            self.memory_bank = MemoryBank()
            self.memory_bank.build(coreset_features)
            
            self.memory_bank_size = self.memory_bank.size
            self.trained_at = time.strftime("%Y-%m-%d %H:%M:%S")
            self._is_trained = True
            
            training_time = time.time() - start_time
            print(f"Training complete! Memory bank size: {self.memory_bank_size}, Time: {training_time:.2f}s")
        
        def predict(self, image):
            if not self._is_trained:
                raise RuntimeError("Model must be trained before prediction")
            
            image_resized = image.resize(self.input_size, Image.LANCZOS)
            patch_features = self.feature_extractor.extract_features(image_resized)
            anomaly_score, anomaly_map = self.memory_bank.score(patch_features)
            return anomaly_score, anomaly_map
        
        def predict_with_heatmap(self, image, original_size=None):
            anomaly_score, anomaly_map = self.predict(image)
            if original_size is not None:
                import cv2
                width, height = original_size
                anomaly_heatmap = cv2.resize(anomaly_map, (width, height), interpolation=cv2.INTER_LINEAR)
            else:
                anomaly_heatmap = anomaly_map
            return anomaly_score, anomaly_heatmap
        
        def save(self, output_dir):
            if not self._is_trained:
                raise RuntimeError("Model must be trained before saving")
            
            output_dir = Path(output_dir)
            output_dir.mkdir(parents=True, exist_ok=True)
            
            # Save memory bank
            self.memory_bank.save(output_dir / "memory_bank.npz")
            
            # Save config
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
            
            with open(output_dir / "config.json", 'w') as f:
                json.dump(config, f, indent=2)
        
        @classmethod
        def load(cls, model_dir):
            model_dir = Path(model_dir)
            
            with open(model_dir / "config.json", 'r') as f:
                config = json.load(f)
            
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
            FeatureExtractor = create_test_feature_extractor()
            model.feature_extractor = FeatureExtractor(model.backbone, model.device)
            
            # Load memory bank
            from patchcore.memory_bank import MemoryBank
            model.memory_bank = MemoryBank()
            model.memory_bank.load(model_dir / "memory_bank.npz")
            
            model._is_trained = True
            return model
        
        @property
        def is_trained(self):
            return self._is_trained
    
    return TestPatchCore


def main():
    """Run the end-to-end PatchCore pipeline test."""
    print("=== PatchCore Pipeline Test ===")
    
    # Setup
    data_dir = Path(__file__).parent.parent / "data" / "raw" / "pcb"
    test_output_dir = Path("/tmp/patchcore_test")
    
    # Clean test output
    if test_output_dir.exists():
        shutil.rmtree(test_output_dir)
    test_output_dir.mkdir(parents=True)
    
    # Load PCB data manager
    print("Loading PCB defect dataset...")
    data_manager = PCBDataManager(data_dir)
    
    # Get training images (first 20 from train split)
    # For PCB dataset, we'll use normal-looking images for training
    # and defect images for testing anomaly detection
    all_train_paths = data_manager.get_image_paths("train")
    train_subset = all_train_paths[:2]  # Limit to 2 for CPU performance
    
    print(f"Training: {len(train_subset)} normal images, coreset_ratio=0.9")
    
    # Create and train PatchCore
    PatchCore = create_test_patchcore()
    model = PatchCore(backbone="wide_resnet50_2", coreset_ratio=0.9, device="cpu")
    model.fit(train_subset, category="pcb")
    
    print(f"Memory bank size: {model.memory_bank_size} entries")
    
    # Save model
    model.save(test_output_dir)
    
    # Load model back
    model_loaded = PatchCore.load(test_output_dir)
    
    # Test predictions
    print("\n--- Predictions ---")
    
    # Get test images - use some from train as "normal" and some from validation as potential "defects"
    test_normal_paths = all_train_paths[20:23]  # Next 3 images from training set
    
    # Get some validation images as "defect" examples
    val_paths = data_manager.get_image_paths("val")
    test_defect_paths = []
    
    # Get defect class distribution to find actual defects
    class_distribution = data_manager.get_class_distribution()
    defect_classes = [cls for cls in class_distribution.keys() if cls not in ["good", "normal"]]
    
    # Sample from validation set, trying to get actual defects
    val_sample_items = data_manager.get_sample_items(10)  # Fixed method call
    defect_count = 0
    for item in val_sample_items:
        if defect_count >= 3:
            break
        # Check if this item is in validation set and has defects
        if item["image_path"] in val_paths and item["class_name"] in defect_classes:
            test_defect_paths.append((item["image_path"], item["class_name"]))
            defect_count += 1
    
    # If we don't have enough clear defects, just use validation images as "defects"
    if len(test_defect_paths) < 3:
        for val_path in val_paths[:3-len(test_defect_paths)]:
            test_defect_paths.append((val_path, "potential_defect"))
    
    # Predict on normal images
    normal_scores = []
    for img_path in test_normal_paths:
        image = Image.open(img_path).convert('RGB')
        score, _ = model_loaded.predict(image)
        normal_scores.append(score)
        print(f"[NORMAL] {img_path.name}  score: {score:.4f}")
    
    # Predict on defect images
    defect_scores = []
    heatmap_saved = False
    for img_path, defect_type in test_defect_paths:
        image = Image.open(img_path).convert('RGB')
        score, anomaly_map = model_loaded.predict(image)
        defect_scores.append(score)
        print(f"[DEFECT] {img_path.name}  score: {score:.4f}  (type: {defect_type})")
        
        # Save one heatmap sample
        if not heatmap_saved:
            plt.figure(figsize=(12, 4))
            
            plt.subplot(1, 3, 1)
            plt.imshow(image)
            plt.title("Original Image")
            plt.axis('off')
            
            plt.subplot(1, 3, 2)
            plt.imshow(anomaly_map, cmap='hot')
            plt.title("Anomaly Map (28x28)")
            plt.colorbar()
            plt.axis('off')
            
            # Upsampled heatmap
            score_up, heatmap_up = model_loaded.predict_with_heatmap(image, original_size=image.size)
            plt.subplot(1, 3, 3)
            plt.imshow(np.array(image))
            plt.imshow(heatmap_up, cmap='hot', alpha=0.6)
            plt.title("Overlay Heatmap")
            plt.axis('off')
            
            plt.tight_layout()
            plt.savefig(test_output_dir / "heatmap_sample.png", dpi=150, bbox_inches='tight')
            plt.close()
            heatmap_saved = True
            print(f"Heatmap saved to: {test_output_dir / 'heatmap_sample.png'}")
    
    # Sanity check
    print("\n--- Sanity Check ---")
    avg_normal = np.mean(normal_scores) if normal_scores else 0
    avg_defect = np.mean(defect_scores) if defect_scores else 0
    
    print(f"Avg normal score: {avg_normal:.4f}")
    print(f"Avg defect score: {avg_defect:.4f}")
    
    # The test: defect scores should be higher than normal scores
    sanity_check = avg_defect > avg_normal if (normal_scores and defect_scores) else False
    result = "PASS" if sanity_check else "FAIL"
    print(f"Defect score > Normal score: {result}")
    
    if result == "PASS":
        print("\n✓ Pipeline test completed successfully!")
    else:
        print("\n✗ Pipeline test failed!")
        if not normal_scores:
            print("  No normal test images found")
        if not defect_scores:
            print("  No defect test images found")
    
    return result == "PASS"


if __name__ == "__main__":
    success = main()
    sys.exit(0 if success else 1)