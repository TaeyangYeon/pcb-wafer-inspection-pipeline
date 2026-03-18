# -*- coding: utf-8 -*-
"""
PCB Defect Dataset Manager

Manages the PCB defect dataset from Peking University with PASCAL VOC format annotations.
Provides train/val splits and dataset metadata for training and analysis.
"""

import os
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Dict, List, Union
import random
from collections import defaultdict, Counter


class PCBDataManager:
    """
    Manages PCB defect dataset with PASCAL VOC format annotations.
    
    Provides stratified train/validation splits, class information, and dataset statistics.
    Follows single responsibility principle: only handles data organization and metadata.
    """
    
    def __init__(self, data_root: Union[str, Path]) -> None:
        """
        Initialize PCB dataset manager.
        
        Args:
            data_root: Path to dataset root directory containing JPEGImages and Annotations folders
            
        Raises:
            FileNotFoundError: If data_root doesn't exist or required subdirectories missing
            ValueError: If dataset structure is invalid
        """
        self.data_root = Path(data_root)
        
        # Validate dataset structure
        if not self.data_root.exists():
            raise FileNotFoundError(f"Dataset root not found: {self.data_root}")
        
        self.images_dir = self.data_root / "JPEGImages"
        self.annotations_dir = self.data_root / "Annotations"
        
        if not self.images_dir.exists():
            raise FileNotFoundError(f"Images directory not found: {self.images_dir}")
        if not self.annotations_dir.exists():
            raise FileNotFoundError(f"Annotations directory not found: {self.annotations_dir}")
        
        # Load and validate dataset
        self._load_dataset()
        
        # Create stratified train/val split (80/20)
        self._create_splits()
    
    def _load_dataset(self) -> None:
        """Load and validate dataset files."""
        # Get all image files
        image_extensions = {'.jpg', '.jpeg', '.png'}
        self._image_files = [
            f for f in self.images_dir.iterdir()
            if f.is_file() and f.suffix.lower() in image_extensions
        ]
        
        if not self._image_files:
            raise ValueError(f"No image files found in {self.images_dir}")
        
        # Get all annotation files
        self._annotation_files = [
            f for f in self.annotations_dir.iterdir()
            if f.is_file() and f.suffix.lower() == '.xml'
        ]
        
        if not self._annotation_files:
            raise ValueError(f"No annotation files found in {self.annotations_dir}")
        
        # Build image-annotation mapping
        self._build_image_annotation_mapping()
        
        # Extract class information
        self._extract_class_information()
    
    def _build_image_annotation_mapping(self) -> None:
        """Build mapping between image files and their annotations."""
        # Create mapping from stem name to full paths
        image_map = {img.stem: img for img in self._image_files}
        annotation_map = {ann.stem: ann for ann in self._annotation_files}
        
        # Find matching pairs
        self._valid_items = []
        self._image_to_annotation = {}
        
        for img_stem, img_path in image_map.items():
            if img_stem in annotation_map:
                ann_path = annotation_map[img_stem]
                self._valid_items.append(img_path)
                self._image_to_annotation[img_path] = ann_path
        
        if not self._valid_items:
            raise ValueError("No matching image-annotation pairs found")
        
        print(f"Found {len(self._valid_items)} valid image-annotation pairs")
    
    def _extract_class_information(self) -> None:
        """Extract class names and build class-to-images mapping."""
        self._class_to_images = defaultdict(list)
        self._image_to_classes = {}
        all_classes = set()
        
        for img_path in self._valid_items:
            ann_path = self._image_to_annotation[img_path]
            
            try:
                # Parse XML annotation
                tree = ET.parse(ann_path)
                root = tree.getroot()
                
                # Extract object classes
                objects = root.findall('object')
                image_classes = []
                
                for obj in objects:
                    name_elem = obj.find('name')
                    if name_elem is not None and name_elem.text:
                        class_name = name_elem.text.strip()
                        image_classes.append(class_name)
                        all_classes.add(class_name)
                
                # Store mapping (use first class for stratification if multiple)
                if image_classes:
                    primary_class = image_classes[0]
                    self._class_to_images[primary_class].append(img_path)
                    self._image_to_classes[img_path] = image_classes
                else:
                    print(f"Warning: No objects found in annotation {ann_path}")
                    
            except ET.ParseError as e:
                print(f"Warning: Failed to parse annotation {ann_path}: {e}")
            except Exception as e:
                print(f"Warning: Error processing annotation {ann_path}: {e}")
        
        self._class_names = sorted(list(all_classes))
        
        if not self._class_names:
            raise ValueError("No valid class names found in annotations")
        
        print(f"Found {len(self._class_names)} classes: {self._class_names}")
    
    def _create_splits(self, train_ratio: float = 0.8, random_seed: int = 42) -> None:
        """Create stratified train/validation splits."""
        random.seed(random_seed)
        
        self._train_images = []
        self._val_images = []
        
        # Stratified split by class
        for class_name, class_images in self._class_to_images.items():
            # Shuffle images for this class
            class_images_shuffled = class_images.copy()
            random.shuffle(class_images_shuffled)
            
            # Split by ratio
            split_idx = int(len(class_images_shuffled) * train_ratio)
            
            class_train = class_images_shuffled[:split_idx]
            class_val = class_images_shuffled[split_idx:]
            
            self._train_images.extend(class_train)
            self._val_images.extend(class_val)
            
            print(f"Class {class_name}: {len(class_train)} train, {len(class_val)} val")
        
        # Final shuffle
        random.shuffle(self._train_images)
        random.shuffle(self._val_images)
        
        print(f"Total split: {len(self._train_images)} train, {len(self._val_images)} val")
    
    def get_class_names(self) -> List[str]:
        """
        Get sorted list of all class names in the dataset.
        
        Returns:
            List of class names sorted alphabetically
        """
        return self._class_names.copy()
    
    def get_image_paths(self, split: str = "all") -> List[Path]:
        """
        Get image paths for specified split.
        
        Args:
            split: Dataset split to return ("train", "val", or "all")
            
        Returns:
            List of image file paths
            
        Raises:
            ValueError: If split is not one of the valid options
        """
        if split == "train":
            return self._train_images.copy()
        elif split == "val":
            return self._val_images.copy()
        elif split == "all":
            return self._valid_items.copy()
        else:
            raise ValueError(f"Invalid split '{split}'. Must be 'train', 'val', or 'all'")
    
    def get_split_counts(self) -> Dict[str, int]:
        """
        Get count of images in each split.
        
        Returns:
            Dictionary with keys "train", "val", "total" and their respective counts
        """
        return {
            "train": len(self._train_images),
            "val": len(self._val_images),
            "total": len(self._valid_items)
        }
    
    def get_class_distribution(self) -> Dict[str, int]:
        """
        Get distribution of classes across all images.
        
        Returns:
            Dictionary mapping class names to their counts
        """
        class_counts = {}
        for class_name in self._class_names:
            class_counts[class_name] = len(self._class_to_images[class_name])
        return class_counts
    
    def get_sample_items(self, n: int = 5) -> List[Dict[str, Union[Path, str]]]:
        """
        Get sample items for preview/testing.
        
        Args:
            n: Number of sample items to return
            
        Returns:
            List of dictionaries with keys:
            - "image_path": Path to image file
            - "class_name": Primary class name
            - "annotation_path": Path to annotation file
            
        Raises:
            ValueError: If n is negative or zero
        """
        if n <= 0:
            raise ValueError("Sample size must be positive")
        
        # Sample from available items
        sample_size = min(n, len(self._valid_items))
        sample_images = random.sample(self._valid_items, sample_size)
        
        samples = []
        for img_path in sample_images:
            ann_path = self._image_to_annotation[img_path]
            
            # Get primary class name
            image_classes = self._image_to_classes.get(img_path, [])
            primary_class = image_classes[0] if image_classes else "unknown"
            
            samples.append({
                "image_path": img_path,
                "class_name": primary_class,
                "annotation_path": ann_path
            })
        
        return samples
    
    def get_annotation_path(self, image_path: Path) -> Path:
        """
        Get annotation path for given image path.
        
        Args:
            image_path: Path to image file
            
        Returns:
            Path to corresponding annotation file
            
        Raises:
            KeyError: If image path not found in dataset
        """
        if image_path not in self._image_to_annotation:
            raise KeyError(f"Image path not found in dataset: {image_path}")
        
        return self._image_to_annotation[image_path]
    
    def get_split_class_distribution(self, split: str) -> Dict[str, int]:
        """
        Get class distribution for a specific split.
        
        Args:
            split: Dataset split ("train" or "val")
            
        Returns:
            Dictionary mapping class names to their counts in the split
            
        Raises:
            ValueError: If split is not "train" or "val"
        """
        if split not in ["train", "val"]:
            raise ValueError(f"Invalid split '{split}'. Must be 'train' or 'val'")
        
        split_images = self.get_image_paths(split)
        class_counts = Counter()
        
        for img_path in split_images:
            image_classes = self._image_to_classes.get(img_path, [])
            if image_classes:
                # Count primary class
                primary_class = image_classes[0]
                class_counts[primary_class] += 1
        
        return dict(class_counts)