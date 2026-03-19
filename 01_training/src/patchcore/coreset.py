"""
Coreset subsampling for PatchCore anomaly detection.

Implements greedy farthest-point selection algorithm from:
"Towards Total Recall in Industrial Anomaly Detection" (Roth et al., 2022)

The coreset reduces memory bank size while preserving feature space coverage
for efficient k-NN scoring at inference time.
"""

import numpy as np
from typing import Optional
from tqdm import tqdm


class CoresetSampler:
    """
    Greedy farthest-point coreset subsampling for anomaly detection.
    
    Selects a representative subset of features that maximizes coverage
    of the feature space using greedy farthest-point selection.
    
    Algorithm:
    1. Start with a randomly selected point as initial coreset
    2. For each remaining candidate, compute minimum distance to any
       point already in the coreset
    3. Select the candidate with MAXIMUM minimum distance
    4. Repeat until target coreset size is reached
    
    Time complexity: O(N * coreset_size * D) where N is total features,
    coreset_size is target size, D is feature dimension.
    """
    
    def __init__(self, ratio: float = 0.01, seed: int = 42):
        """
        Initialize coreset sampler.
        
        Args:
            ratio: Fraction of total patches to keep in coreset (0.01 to 0.1)
            seed: Random seed for reproducible initial point selection
        """
        if not (0.0 < ratio <= 1.0):
            raise ValueError(f"ratio must be in (0, 1], got {ratio}")
        
        self.ratio = ratio
        self.seed = seed
        self.rng = np.random.RandomState(seed)
    
    def fit(self, features: np.ndarray) -> np.ndarray:
        """
        Perform coreset subsampling using greedy farthest-point selection.
        
        Args:
            features: Feature vectors of shape (N, D)
            
        Returns:
            Coreset features of shape (coreset_size, D) where
            coreset_size = max(1, int(N * ratio))
        """
        if features.ndim != 2:
            raise ValueError(f"features must be 2D array, got shape {features.shape}")
        
        n_samples, feature_dim = features.shape
        coreset_size = max(1, int(n_samples * self.ratio))
        
        if coreset_size >= n_samples:
            return features.copy()
        
        # Convert to float32 for memory efficiency and numerical stability
        features = features.astype(np.float32)
        
        # Initialize coreset with random point
        selected_indices = [self.rng.randint(0, n_samples)]
        remaining_indices = list(range(n_samples))
        remaining_indices.remove(selected_indices[0])
        
        # Greedy farthest-point selection
        with tqdm(total=coreset_size-1, desc="Coreset selection") as pbar:
            while len(selected_indices) < coreset_size:
                # Get current coreset
                selected_features = features[selected_indices]
                
                # Compute distances from remaining candidates to selected points
                remaining_features = features[remaining_indices]
                distances = self._compute_distances(remaining_features, selected_features)
                
                # Select candidate with maximum minimum distance
                farthest_idx = np.argmax(distances)
                selected_indices.append(remaining_indices[farthest_idx])
                remaining_indices.pop(farthest_idx)
                
                pbar.update(1)
        
        return features[selected_indices]
    
    def _compute_distances(self, 
                          candidates: np.ndarray, 
                          selected: np.ndarray) -> np.ndarray:
        """
        Compute minimum distance from each candidate to nearest selected point.
        
        Uses efficient batch computation with squared distances for speed.
        
        Args:
            candidates: Candidate features of shape (N_candidates, D)
            selected: Already selected features of shape (N_selected, D)
            
        Returns:
            Minimum distances of shape (N_candidates,)
        """
        if candidates.size == 0:
            return np.array([])
        
        # Use squared L2 distance for computational efficiency
        # (monotonic, so argmax behavior is preserved)
        
        # Compute all pairwise squared distances efficiently
        # ||a - b||^2 = ||a||^2 + ||b||^2 - 2<a,b>
        candidates_norm = np.sum(candidates**2, axis=1, keepdims=True)  # (N_candidates, 1)
        selected_norm = np.sum(selected**2, axis=1, keepdims=True)      # (N_selected, 1)
        cross_term = np.dot(candidates, selected.T)                     # (N_candidates, N_selected)
        
        # Broadcast to get squared distances
        squared_distances = (candidates_norm + 
                           selected_norm.T - 
                           2 * cross_term)  # (N_candidates, N_selected)
        
        # Find minimum distance for each candidate
        min_squared_distances = np.min(squared_distances, axis=1)
        
        # Return actual distances (not squared)
        return np.sqrt(np.maximum(min_squared_distances, 0.0))  # Avoid negative due to numerical errors


def test_coreset_sampler():
    """Basic test for CoresetSampler functionality."""
    # Test with synthetic data
    np.random.seed(0)
    features = np.random.randn(1000, 128).astype(np.float32)
    
    sampler = CoresetSampler(ratio=0.1, seed=42)
    coreset = sampler.fit(features)
    
    expected_size = int(1000 * 0.1)
    assert coreset.shape == (expected_size, 128), f"Expected {(expected_size, 128)}, got {coreset.shape}"
    assert coreset.dtype == np.float32, f"Expected float32, got {coreset.dtype}"
    
    print("CoresetSampler test passed!")
    return coreset


if __name__ == "__main__":
    test_coreset_sampler()