#!/usr/bin/env python3
"""
PCB Defect Dataset Exploration Script
Analyzes the Peking University PCB defect dataset structure,
image counts, dimensions, and annotation format.
"""

import os
import xml.etree.ElementTree as ET
from pathlib import Path
from collections import defaultdict, Counter
from PIL import Image

def main():
    # Dataset paths
    data_root = Path("data/raw/pcb")
    images_dir = data_root / "JPEGImages"
    annotations_dir = data_root / "Annotations"
    
    print("=== PCB Defect Dataset Exploration ===\n")
    
    # Check if directories exist
    if not images_dir.exists():
        print(f"ERROR: Images directory not found: {images_dir}")
        return
    if not annotations_dir.exists():
        print(f"ERROR: Annotations directory not found: {annotations_dir}")
        return
    
    # Get all image and annotation files
    image_files = list(images_dir.glob("*.jpg")) + list(images_dir.glob("*.jpeg")) + list(images_dir.glob("*.png"))
    annotation_files = list(annotations_dir.glob("*.xml"))
    
    print(f"Total image files found: {len(image_files)}")
    print(f"Total annotation files found: {len(annotation_files)}")
    
    # Analyze defect classes from filenames
    defect_classes = defaultdict(int)
    image_dimensions = []
    
    # Known defect types
    defect_types = ["missing_hole", "mouse_bite", "open_circuit", "short", "spur", "spurious_copper"]
    
    print(f"\n=== Image Analysis ===")
    
    # Count images by defect type
    for img_file in image_files:
        filename = img_file.stem.lower()
        for defect_type in defect_types:
            if defect_type in filename:
                defect_classes[defect_type] += 1
                break
        else:
            defect_classes["unknown"] += 1
    
    # Sample a few images for dimension analysis
    sample_images = image_files[:10] if len(image_files) >= 10 else image_files
    
    print(f"Analyzing dimensions of {len(sample_images)} sample images...")
    
    for img_file in sample_images:
        try:
            with Image.open(img_file) as img:
                image_dimensions.append((img.width, img.height))
        except Exception as e:
            print(f"Error reading {img_file}: {e}")
    
    # Print defect class distribution
    print(f"\n=== Defect Class Distribution ===")
    for defect_type in sorted(defect_classes.keys()):
        print(f"{defect_type}: {defect_classes[defect_type]} images")
    
    # Print image dimensions
    print(f"\n=== Sample Image Dimensions ===")
    if image_dimensions:
        dimension_counter = Counter(image_dimensions)
        for (width, height), count in dimension_counter.most_common():
            print(f"{width}x{height}: {count} samples")
        
        avg_width = sum(dim[0] for dim in image_dimensions) / len(image_dimensions)
        avg_height = sum(dim[1] for dim in image_dimensions) / len(image_dimensions)
        print(f"Average dimensions: {avg_width:.0f}x{avg_height:.0f}")
    
    # Analyze annotation format
    print(f"\n=== Annotation Format Analysis ===")
    if annotation_files:
        sample_annotation = annotation_files[0]
        print(f"Annotation format: XML")
        print(f"Sample annotation file: {sample_annotation.name}")
        
        # Parse sample annotation
        try:
            tree = ET.parse(sample_annotation)
            root = tree.getroot()
            
            print(f"\nSample annotation structure:")
            print(f"Root element: {root.tag}")
            
            # Find key elements
            filename_elem = root.find('filename')
            size_elem = root.find('size')
            objects = root.findall('object')
            
            if filename_elem is not None:
                print(f"Filename: {filename_elem.text}")
            
            if size_elem is not None:
                width = size_elem.find('width')
                height = size_elem.find('height')
                depth = size_elem.find('depth')
                if width is not None and height is not None:
                    print(f"Image size: {width.text}x{height.text}")
                if depth is not None:
                    print(f"Channels: {depth.text}")
            
            print(f"Number of objects: {len(objects)}")
            
            if objects:
                obj = objects[0]
                name_elem = obj.find('name')
                bbox_elem = obj.find('bndbox')
                
                if name_elem is not None:
                    print(f"Object class: {name_elem.text}")
                
                if bbox_elem is not None:
                    xmin = bbox_elem.find('xmin')
                    ymin = bbox_elem.find('ymin')
                    xmax = bbox_elem.find('xmax')
                    ymax = bbox_elem.find('ymax')
                    
                    if all(elem is not None for elem in [xmin, ymin, xmax, ymax]):
                        print(f"Bounding box: ({xmin.text}, {ymin.text}, {xmax.text}, {ymax.text})")
            
            print(f"\nFull sample annotation content:")
            print("-" * 50)
            with open(sample_annotation, 'r', encoding='utf-8') as f:
                content = f.read()
                # Print first 500 characters
                print(content[:500] + ("..." if len(content) > 500 else ""))
            print("-" * 50)
            
        except Exception as e:
            print(f"Error parsing annotation file: {e}")
    
    # Summary
    print(f"\n=== Dataset Summary ===")
    print(f"Dataset format: PASCAL VOC")
    print(f"Total images: {len(image_files)}")
    print(f"Total annotations: {len(annotation_files)}")
    print(f"Defect classes: {len([k for k in defect_classes.keys() if k != 'unknown'])}")
    print(f"Annotation format: XML (PASCAL VOC)")
    
    if image_dimensions:
        most_common_dim = dimension_counter.most_common(1)[0][0]
        print(f"Most common image size: {most_common_dim[0]}x{most_common_dim[1]}")

if __name__ == "__main__":
    main()