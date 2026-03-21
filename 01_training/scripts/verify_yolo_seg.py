#!/usr/bin/env python3
"""
Verify YOLO-seg dataset conversion by checking format and generating visual validation.

Validates:
1. Label file format (class_id + 8 coordinates per line)
2. Coordinate normalization (values 0.0 to 1.0)  
3. Class IDs are valid (0-5)
4. Visual reconstruction of polygons on images

Outputs verification images to: outputs/verify_seg/
"""

import os
from pathlib import Path
from collections import defaultdict, Counter
from typing import Dict, List, Tuple, Optional
import random
from PIL import Image, ImageDraw
import glob


def get_class_mapping() -> Dict[int, str]:
    """Get class index to name mapping."""
    return {
        0: 'missing_hole',
        1: 'mouse_bite', 
        2: 'open_circuit',
        3: 'short',
        4: 'spur',
        5: 'spurious_copper'
    }


def parse_yolo_seg_line(line: str) -> Optional[Tuple[int, List[float]]]:
    """
    Parse a YOLO-seg format line.
    
    Args:
        line: String like "0 0.4 0.67 0.47 0.67 0.47 0.73 0.4 0.73"
    
    Returns:
        (class_id, [x1, y1, x2, y2, x3, y3, x4, y4]) or None if invalid
    """
    try:
        parts = line.strip().split()
        if len(parts) != 9:  # class_id + 8 coordinates
            return None
            
        class_id = int(parts[0])
        coords = [float(x) for x in parts[1:]]
        
        # Validate class_id
        if class_id < 0 or class_id > 5:
            return None
            
        # Validate coordinates are normalized (0.0 to 1.0)
        if not all(0.0 <= coord <= 1.0 for coord in coords):
            return None
            
        return class_id, coords
        
    except (ValueError, IndexError):
        return None


def extract_class_from_filename(filename: str) -> Optional[str]:
    """Extract class name from filename (same logic as conversion script)."""
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


def count_files_by_class(labels_dir: Path) -> Dict[str, int]:
    """Count label files by class based on filename."""
    class_counts = defaultdict(int)
    class_mapping = get_class_mapping()
    
    for label_file in labels_dir.glob('*.txt'):
        class_name = extract_class_from_filename(label_file.name)
        if class_name:
            class_counts[class_name] += 1
    
    return class_counts


def get_sample_files_by_class(labels_dir: Path, samples_per_class: int = 2) -> Dict[str, List[Path]]:
    """Get sample label files for each class."""
    class_mapping = get_class_mapping()
    class_files = {class_name: [] for class_name in class_mapping.values()}
    
    # Group files by class using same extraction logic
    for label_file in labels_dir.glob('*.txt'):
        class_name = extract_class_from_filename(label_file.name)
        if class_name and class_name in class_files:
            class_files[class_name].append(label_file)
    
    # Sample files from each class
    random.seed(42)
    samples = {}
    for class_name, files in class_files.items():
        if files:
            sample_count = min(samples_per_class, len(files))
            samples[class_name] = random.sample(files, sample_count)
        else:
            samples[class_name] = []
    
    return samples


def draw_polygon_on_image(image_path: Path, coords: List[float], class_name: str) -> Image.Image:
    """
    Draw polygon overlay on image using PIL.
    
    Args:
        image_path: Path to image file
        coords: [x1, y1, x2, y2, x3, y3, x4, y4] normalized coordinates
        class_name: Class name for color selection
    
    Returns:
        PIL Image with polygon overlay
    """
    # Load image
    img = Image.open(image_path).convert('RGB')
    draw = ImageDraw.Draw(img)
    
    # Get image dimensions
    img_width, img_height = img.size
    
    # Convert normalized coordinates to pixel coordinates
    pixel_coords = []
    for i in range(0, len(coords), 2):
        x = coords[i] * img_width
        y = coords[i + 1] * img_height
        pixel_coords.extend([x, y])
    
    # Color mapping for classes
    colors = {
        'missing_hole': 'red',
        'mouse_bite': 'blue', 
        'open_circuit': 'green',
        'short': 'yellow',
        'spur': 'magenta',
        'spurious_copper': 'cyan'
    }
    
    color = colors.get(class_name, 'white')
    
    # Draw polygon outline
    draw.polygon(pixel_coords, outline=color, width=3)
    
    # Draw corner points
    for i in range(0, len(pixel_coords), 2):
        x, y = pixel_coords[i], pixel_coords[i + 1]
        draw.ellipse([x-3, y-3, x+3, y+3], fill=color)
    
    return img


def verify_yolo_seg():
    """Main verification function."""
    
    # Setup paths
    base_dir = Path(__file__).parent.parent
    processed_dir = base_dir / 'data' / 'processed' / 'pcb_seg'
    output_dir = base_dir / 'outputs' / 'verify_seg'
    
    train_labels = processed_dir / 'labels' / 'train'
    val_labels = processed_dir / 'labels' / 'val'
    train_images = processed_dir / 'images' / 'train'
    
    print("=== YOLO-seg Conversion Verification ===")
    print(f"Dataset path: {processed_dir}")
    print(f"Output path: {output_dir}")
    
    # Create output directory
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Count files by class
    train_counts = count_files_by_class(train_labels)
    val_counts = count_files_by_class(val_labels)
    
    class_mapping = get_class_mapping()
    
    # Verification results
    verification_results = {}
    total_format_errors = 0
    total_coord_errors = 0
    
    # Get sample files for visual verification
    sample_files = get_sample_files_by_class(train_labels, samples_per_class=2)
    
    print(f"\n{'Class':<15} {'Train':<8} {'Val':<8} {'Format OK':<10} {'Coords OK'}")
    print("-" * 55)
    
    for class_id, class_name in class_mapping.items():
        train_count = train_counts[class_name]
        val_count = val_counts[class_name]
        
        # Check format and coordinates for sample files
        format_ok = True
        coords_ok = True
        
        samples = sample_files[class_name]
        for i, label_file in enumerate(samples):
            try:
                with open(label_file, 'r') as f:
                    lines = f.readlines()
                
                valid_lines = 0
                for line in lines:
                    result = parse_yolo_seg_line(line)
                    if result is None:
                        format_ok = False
                        total_format_errors += 1
                    else:
                        parsed_class_id, coords = result
                        if parsed_class_id != class_id:
                            continue  # Different class in multi-class file
                        
                        valid_lines += 1
                        
                        # Check coordinate bounds
                        if not all(0.0 <= coord <= 1.0 for coord in coords):
                            coords_ok = False
                            total_coord_errors += 1
                        
                        # Generate visual verification
                        img_file = train_images / f"{label_file.stem}.jpg"
                        if img_file.exists():
                            try:
                                verify_img = draw_polygon_on_image(img_file, coords, class_name)
                                output_path = output_dir / f"{class_name}_{label_file.stem}_{i+1}.png"
                                verify_img.save(output_path)
                            except Exception as e:
                                print(f"Warning: Could not create verification image for {img_file.name}: {e}")
                        
            except Exception as e:
                print(f"Warning: Error processing {label_file.name}: {e}")
                format_ok = False
        
        verification_results[class_name] = {
            'train': train_count,
            'val': val_count, 
            'format_ok': format_ok,
            'coords_ok': coords_ok
        }
        
        format_status = "YES" if format_ok else "NO"
        coords_status = "YES" if coords_ok else "NO"
        
        print(f"{class_name:<15} {train_count:<8} {val_count:<8} {format_status:<10} {coords_status}")
    
    # Summary
    print("-" * 55)
    total_train = sum(r['train'] for r in verification_results.values())
    total_val = sum(r['val'] for r in verification_results.values())
    
    print(f"{'Total:':<15} {total_train:<8} {total_val:<8}")
    print()
    
    # Overall status
    all_format_ok = all(r['format_ok'] for r in verification_results.values())
    all_coords_ok = all(r['coords_ok'] for r in verification_results.values())
    
    overall_status = "PASS" if (all_format_ok and all_coords_ok) else "FAIL"
    
    print(f"Format errors: {total_format_errors}")
    print(f"Coordinate errors: {total_coord_errors}")
    print(f"Verification images saved to: {output_dir}")
    print(f"Overall: {overall_status}")
    
    # List generated verification images
    verify_images = list(output_dir.glob('*.png'))
    print(f"\nGenerated {len(verify_images)} verification images:")
    for img in sorted(verify_images):
        print(f"  {img.name}")


if __name__ == "__main__":
    verify_yolo_seg()