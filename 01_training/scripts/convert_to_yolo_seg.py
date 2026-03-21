#!/usr/bin/env python3
"""
Convert Pascal VOC format PCB dataset to YOLO-seg format.
Converts bounding boxes to 4-point polygons for segmentation training.

Dataset structure:
  Input:  data/raw/pcb/{class}/images/*.jpg, data/raw/pcb/{class}/Annotations/*.xml
  Output: data/processed/pcb_seg/images/{train,val}/*.jpg
          data/processed/pcb_seg/labels/{train,val}/*.txt

YOLO-seg format: class_id x1 y1 x2 y2 x3 y3 x4 y4 (normalized 0.0-1.0)
"""

import os
import shutil
import xml.etree.ElementTree as ET
from pathlib import Path
from collections import defaultdict, Counter
from typing import List, Tuple, Dict, Optional
import random
from tqdm import tqdm


def parse_xml_annotation(xml_path: Path) -> Tuple[Optional[Tuple[int, int]], List[Tuple[str, Tuple[int, int, int, int]]]]:
    """
    Parse Pascal VOC XML annotation file.
    
    Returns:
        (image_size, objects) where:
        - image_size: (width, height) or None if parsing fails
        - objects: List of (class_name, (xmin, ymin, xmax, ymax))
    """
    try:
        tree = ET.parse(xml_path)
        root = tree.getroot()
        
        # Get image dimensions
        size_elem = root.find('size')
        if size_elem is None:
            return None, []
        
        width = int(size_elem.find('width').text)
        height = int(size_elem.find('height').text)
        
        # Parse objects
        objects = []
        for obj in root.findall('object'):
            class_name = obj.find('name').text
            
            bndbox = obj.find('bndbox')
            xmin = int(bndbox.find('xmin').text)
            ymin = int(bndbox.find('ymin').text)
            xmax = int(bndbox.find('xmax').text)
            ymax = int(bndbox.find('ymax').text)
            
            # Validate bounding box
            if xmax > xmin and ymax > ymin:
                objects.append((class_name, (xmin, ymin, xmax, ymax)))
        
        return (width, height), objects
    
    except Exception as e:
        print(f"Error parsing {xml_path}: {e}")
        return None, []


def bbox_to_polygon_normalized(bbox: Tuple[int, int, int, int], img_width: int, img_height: int) -> List[float]:
    """
    Convert bounding box to normalized 4-point polygon (clockwise from top-left).
    
    Args:
        bbox: (xmin, ymin, xmax, ymax) in pixel coordinates
        img_width, img_height: Image dimensions
    
    Returns:
        [x1, y1, x2, y2, x3, y3, x4, y4] normalized to [0.0, 1.0]
    """
    xmin, ymin, xmax, ymax = bbox
    
    # Convert to normalized coordinates (0.0 to 1.0)
    x1 = xmin / img_width   # top-left
    y1 = ymin / img_height
    x2 = xmax / img_width   # top-right
    y2 = ymin / img_height
    x3 = xmax / img_width   # bottom-right
    y3 = ymax / img_height
    x4 = xmin / img_width   # bottom-left
    y4 = ymax / img_height
    
    return [x1, y1, x2, y2, x3, y3, x4, y4]


def get_class_mapping() -> Dict[str, int]:
    """Get class name to index mapping (alphabetical order for consistency)."""
    return {
        'missing_hole': 0,
        'mouse_bite': 1,
        'open_circuit': 2,
        'short': 3,
        'spur': 4,
        'spurious_copper': 5
    }


def extract_class_from_filename(filename: str) -> Optional[str]:
    """Extract class name from PCB dataset filename."""
    # Example: l_light_01_missing_hole_01_1_600.jpg -> missing_hole
    parts = filename.split('_')
    for i, part in enumerate(parts):
        if part in ['missing', 'mouse', 'open', 'short', 'spur', 'spurious']:
            if part == 'missing' and i + 1 < len(parts) and parts[i + 1] == 'hole':
                return 'missing_hole'
            elif part == 'mouse' and i + 1 < len(parts) and parts[i + 1] == 'bite':
                return 'mouse_bite'
            elif part == 'open' and i + 1 < len(parts) and parts[i + 1] == 'circuit':
                return 'open_circuit'
            elif part == 'spurious' and i + 1 < len(parts) and parts[i + 1] == 'copper':
                return 'spurious_copper'
            elif part in ['short', 'spur']:
                return part
    return None


def stratified_split(file_pairs: List[Tuple[Path, Path]], train_ratio: float = 0.8, seed: int = 42) -> Tuple[List[Tuple[Path, Path]], List[Tuple[Path, Path]]]:
    """
    Perform stratified split based on class distribution.
    
    Args:
        file_pairs: List of (image_path, xml_path) tuples
        train_ratio: Fraction for training set
        seed: Random seed for reproducibility
    
    Returns:
        (train_pairs, val_pairs)
    """
    random.seed(seed)
    
    # Group by class (extract from filename)
    class_groups = defaultdict(list)
    for img_path, xml_path in file_pairs:
        class_name = extract_class_from_filename(img_path.name)
        if class_name:
            class_groups[class_name].append((img_path, xml_path))
        else:
            print(f"Warning: Could not extract class from filename: {img_path.name}")
    
    train_pairs = []
    val_pairs = []
    
    for class_name, pairs in class_groups.items():
        pairs_shuffled = pairs.copy()
        random.shuffle(pairs_shuffled)
        
        train_count = int(len(pairs_shuffled) * train_ratio)
        train_pairs.extend(pairs_shuffled[:train_count])
        val_pairs.extend(pairs_shuffled[train_count:])
    
    return train_pairs, val_pairs


def convert_to_yolo_seg():
    """Main conversion function."""
    
    # Setup paths
    base_dir = Path(__file__).parent.parent
    input_dir = base_dir / 'data' / 'raw' / 'pcb'
    output_dir = base_dir / 'data' / 'processed' / 'pcb_seg'
    
    print(f"Input directory: {input_dir}")
    print(f"Output directory: {output_dir}")
    
    # Create output directories
    for split in ['train', 'val']:
        (output_dir / 'images' / split).mkdir(parents=True, exist_ok=True)
        (output_dir / 'labels' / split).mkdir(parents=True, exist_ok=True)
    
    # Get class mapping
    class_mapping = get_class_mapping()
    print(f"Class mapping: {class_mapping}")
    
    # Find all image-annotation pairs
    file_pairs = []
    images_dir = input_dir / 'JPEGImages'
    annotations_dir = input_dir / 'Annotations'
    
    if not images_dir.exists() or not annotations_dir.exists():
        print(f"Error: Missing JPEGImages or Annotations directory in {input_dir}")
        return
    
    print(f"Images directory: {images_dir}")
    print(f"Annotations directory: {annotations_dir}")
    
    # Match image and XML files
    for img_path in images_dir.glob('*'):
        if img_path.suffix.lower() not in ['.jpg', '.jpeg', '.png']:
            continue
            
        xml_path = annotations_dir / f"{img_path.stem}.xml"
        if xml_path.exists():
            file_pairs.append((img_path, xml_path))
        else:
            print(f"Warning: No annotation found for {img_path.name}")
    
    print(f"Found {len(file_pairs)} image-annotation pairs")
    
    # Stratified split
    train_pairs, val_pairs = stratified_split(file_pairs, train_ratio=0.8, seed=42)
    print(f"Split: {len(train_pairs)} train, {len(val_pairs)} val")
    
    # Conversion statistics
    stats = {
        'train': defaultdict(int),
        'val': defaultdict(int),
        'skipped': 0,
        'total_objects': 0
    }
    
    # Process splits
    splits = [('train', train_pairs), ('val', val_pairs)]
    
    for split_name, pairs in splits:
        print(f"\nProcessing {split_name} split...")
        
        for img_path, xml_path in tqdm(pairs, desc=f"Converting {split_name}"):
            # Parse annotation
            image_size, objects = parse_xml_annotation(xml_path)
            
            if image_size is None or len(objects) == 0:
                stats['skipped'] += 1
                continue
            
            img_width, img_height = image_size
            
            # Copy image
            output_img_path = output_dir / 'images' / split_name / img_path.name
            shutil.copy2(img_path, output_img_path)
            
            # Create YOLO-seg label file
            label_lines = []
            for class_name, bbox in objects:
                if class_name not in class_mapping:
                    print(f"Warning: Unknown class '{class_name}' in {xml_path.name}")
                    continue
                
                class_id = class_mapping[class_name]
                polygon_coords = bbox_to_polygon_normalized(bbox, img_width, img_height)
                
                # Format: class_id x1 y1 x2 y2 x3 y3 x4 y4
                line = f"{class_id} " + " ".join(f"{coord:.6f}" for coord in polygon_coords)
                label_lines.append(line)
                
                stats[split_name][class_name] += 1
                stats['total_objects'] += 1
            
            # Write label file
            if label_lines:
                label_path = output_dir / 'labels' / split_name / f"{img_path.stem}.txt"
                with open(label_path, 'w') as f:
                    f.write('\n'.join(label_lines) + '\n')
    
    # Print summary
    print("\n" + "="*60)
    print("CONVERSION SUMMARY")
    print("="*60)
    
    # Class distribution table
    print(f"{'Class':<20} {'Train':<10} {'Val':<10} {'Total':<10}")
    print("-" * 50)
    
    total_train = 0
    total_val = 0
    
    for class_name in sorted(class_mapping.keys()):
        train_count = stats['train'][class_name]
        val_count = stats['val'][class_name]
        total_count = train_count + val_count
        
        total_train += train_count
        total_val += val_count
        
        print(f"{class_name:<20} {train_count:<10} {val_count:<10} {total_count:<10}")
    
    print("-" * 50)
    print(f"{'TOTAL':<20} {total_train:<10} {total_val:<10} {total_train + total_val:<10}")
    
    print(f"\nFiles processed: {len(train_pairs) + len(val_pairs)}")
    print(f"Files skipped: {stats['skipped']}")
    print(f"Total objects converted: {stats['total_objects']}")
    
    # Check output directory size
    try:
        total_size = sum(f.stat().st_size for f in output_dir.rglob('*') if f.is_file())
        size_mb = total_size / (1024 * 1024)
        print(f"Output directory size: {size_mb:.1f} MB")
    except:
        print("Could not calculate output directory size")
    
    print(f"Output saved to: {output_dir}")
    print("Conversion complete!")


if __name__ == "__main__":
    convert_to_yolo_seg()