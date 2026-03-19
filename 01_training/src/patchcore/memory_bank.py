"""
Memory bank for PatchCore anomaly detection.

Stores coreset features and performs k-NN anomaly scoring at inference time.
Implements the memory bank component from:
"Towards Total Recall in Industrial Anomaly Detection" (Roth et al., 2022)

The memory bank enables efficient anomaly detection by:
1. Storing a representative subset of normal features (coreset)
2. Computing nearest neighbor distances for new patches
3. Generating pixel-level anomaly maps and image-level scores
"""

import numpy as np
from pathlib import Path
from typing import Union, Tuple
from sklearn.neighbors import NearestNeighbors


class MemoryBank:
    """
    PatchCore memory bank for anomaly detection.
    
    Stores coreset features and performs k-nearest neighbor search
    to compute anomaly scores and pixel-level anomaly maps.
    
    The memory bank uses k=1 (nearest neighbor) distance as the
    anomaly score, following the PatchCore paper default.
    """
    
    def __init__(self):
        """Initialize empty memory bank."""
        self.features = None
        self.nn_index = None
        self._is_built = False
    
    def build(self, features: np.ndarray) -> None:
        """
        Build memory bank from coreset features.
        
        Creates a k-NN index for efficient nearest neighbor search
        during inference.
        
        Args:
            features: Coreset features of shape (N, D)
        """
        if features.ndim != 2:
            raise ValueError(f"Features must be 2D array, got shape {features.shape}")
        
        if features.size == 0:
            raise ValueError("Cannot build memory bank with empty features")
        
        # Store features as float32 for memory efficiency
        self.features = features.astype(np.float32)
        
        # Build k-NN index with k=1 (nearest neighbor)
        # Use 'auto' algorithm for sklearn to choose best method
        self.nn_index = NearestNeighbors(
            n_neighbors=1,
            algorithm='auto',
            metric='l2'
        )
        self.nn_index.fit(self.features)
        
        self._is_built = True
    
    def score(self, patch_features: np.ndarray) -> Tuple[float, np.ndarray]:
        """
        Compute anomaly score and map for image patches.
        
        Args:
            patch_features: Features for one image of shape (N_patches, D)
                          Expected N_patches = 28*28 = 784 for 224x224 input
        
        Returns:
            tuple containing:
            - anomaly_score: Image-level anomaly score (max patch distance)
            - anomaly_map: Pixel-level anomaly map of shape (28, 28)
        """
        if not self._is_built:
            raise RuntimeError("Memory bank must be built before scoring")
        
        if patch_features.ndim != 2:
            raise ValueError(f"Patch features must be 2D array, got shape {patch_features.shape}")
        
        if patch_features.shape[1] != self.features.shape[1]:
            raise ValueError(
                f"Feature dimension mismatch: expected {self.features.shape[1]}, "
                f"got {patch_features.shape[1]}"
            )
        
        # Convert to float32 for consistency
        patch_features = patch_features.astype(np.float32)
        
        # Find nearest neighbor distances for all patches
        # distances shape: (N_patches, 1)
        distances, _ = self.nn_index.kneighbors(patch_features)
        
        # Remove the extra dimension: (N_patches, 1) -> (N_patches,)
        distances = distances.flatten()
        
        # Image-level anomaly score: maximum patch distance
        anomaly_score = float(np.max(distances))
        
        # Pixel-level anomaly map: reshape to spatial grid
        # Assume 28x28 patches for 224x224 input (layer2 stride=8)
        n_patches = len(distances)
        
        if n_patches == 784:  # 28*28 = 784
            anomaly_map = distances.reshape(28, 28)
        else:
            # Handle other input sizes by inferring square grid
            grid_size = int(np.sqrt(n_patches))
            if grid_size * grid_size != n_patches:
                raise ValueError(
                    f"Cannot reshape {n_patches} patches to square grid. "
                    f"Expected perfect square number of patches."
                )
            anomaly_map = distances.reshape(grid_size, grid_size)
        
        return anomaly_score, anomaly_map
    
    def save(self, path: Union[str, Path]) -> None:
        """
        Save memory bank features to disk.
        
        Saves the raw features in .npz format. The k-NN index will be
        rebuilt when loading to ensure compatibility across platforms.
        
        Args:
            path: Path to save the memory bank (.npz file)
        """
        if not self._is_built:
            raise RuntimeError("Memory bank must be built before saving")
        
        path = Path(path)
        
        # Create directory if it doesn't exist
        path.parent.mkdir(parents=True, exist_ok=True)
        
        # Save features in compressed numpy format
        np.savez_compressed(
            path,
            features=self.features,
            n_samples=len(self.features),
            feature_dim=self.features.shape[1]
        )
    
    def load(self, path: Union[str, Path]) -> None:
        """
        Load memory bank features from disk.
        
        Loads the raw features and rebuilds the k-NN index.
        
        Args:
            path: Path to load the memory bank (.npz file)
        """
        path = Path(path)
        
        if not path.exists():
            raise FileNotFoundError(f"Memory bank file not found: {path}")
        
        # Load features from .npz file
        try:
            data = np.load(path)
            self.features = data['features'].astype(np.float32)
            
            # Verify data integrity
            expected_samples = data['n_samples']
            expected_dim = data['feature_dim']
            
            if len(self.features) != expected_samples:
                raise ValueError(
                    f"Loaded features size mismatch: expected {expected_samples}, "
                    f"got {len(self.features)}"
                )
            
            if self.features.shape[1] != expected_dim:
                raise ValueError(
                    f"Loaded feature dimension mismatch: expected {expected_dim}, "
                    f"got {self.features.shape[1]}"
                )
        
        except Exception as e:
            raise RuntimeError(f"Failed to load memory bank from {path}: {e}")
        
        # Rebuild k-NN index
        self.nn_index = NearestNeighbors(
            n_neighbors=1,
            algorithm='auto',
            metric='l2'
        )
        self.nn_index.fit(self.features)
        
        self._is_built = True
    
    @property
    def size(self) -> int:
        """
        Get the number of entries in the memory bank.
        
        Returns:
            Number of feature vectors stored in the memory bank
        """
        if not self._is_built:
            return 0
        return len(self.features)
    
    def __len__(self) -> int:
        """Support for len() function."""
        return self.size
    
    def __repr__(self) -> str:
        """String representation of memory bank."""
        if self._is_built:
            return f"MemoryBank(size={self.size}, feature_dim={self.features.shape[1]})"
        return "MemoryBank(empty)"


def test_memory_bank():
    """Basic test for MemoryBank functionality."""
    # Test data
    np.random.seed(42)
    bank_features = np.random.randn(50, 128).astype(np.float32)
    test_features = np.random.randn(784, 128).astype(np.float32)
    
    # Build memory bank
    bank = MemoryBank()
    bank.build(bank_features)
    
    # Test scoring
    score, anomaly_map = bank.score(test_features)
    
    assert isinstance(score, float), f"Expected float score, got {type(score)}"
    assert anomaly_map.shape == (28, 28), f"Expected (28, 28) map, got {anomaly_map.shape}"
    assert score >= 0, f"Expected non-negative score, got {score}"
    
    # Test save/load
    import tempfile
    with tempfile.NamedTemporaryFile(suffix='.npz', delete=False) as f:
        temp_path = f.name
    
    try:
        bank.save(temp_path)
        
        # Load into new bank
        bank2 = MemoryBank()
        bank2.load(temp_path)
        
        # Verify loaded bank works
        score2, anomaly_map2 = bank2.score(test_features)
        
        assert abs(score - score2) < 1e-6, f"Scores don't match: {score} vs {score2}"
        assert np.allclose(anomaly_map, anomaly_map2, atol=1e-6), "Anomaly maps don't match"
        
        print("MemoryBank test passed!")
        
    finally:
        # Cleanup
        import os
        if os.path.exists(temp_path):
            os.unlink(temp_path)


if __name__ == "__main__":
    test_memory_bank()