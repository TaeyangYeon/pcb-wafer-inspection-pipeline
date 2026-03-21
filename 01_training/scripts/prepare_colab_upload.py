#!/usr/bin/env python3
"""
Prepare PCB dataset for Google Colab upload.

Creates a zip file of the processed YOLO-seg dataset for easy upload to
Google Drive. The zip file can then be uploaded to Drive and extracted
in Colab for training.

Usage:
    python 01_training/scripts/prepare_colab_upload.py

Output:
    01_training/outputs/pcb_seg_dataset.zip
"""

import os
import zipfile
from pathlib import Path
from tqdm import tqdm


def get_directory_size(path: Path) -> int:
    """Calculate total size of directory in bytes."""
    total_size = 0
    for dirpath, dirnames, filenames in os.walk(path):
        for filename in filenames:
            filepath = Path(dirpath) / filename
            try:
                total_size += filepath.stat().st_size
            except (OSError, FileNotFoundError):
                # Skip files that can't be accessed
                continue
    return total_size


def create_dataset_zip():
    """Create zip file of processed PCB dataset."""
    
    # Setup paths
    base_dir = Path(__file__).parent.parent
    dataset_dir = base_dir / 'data' / 'processed' / 'pcb_seg'
    output_dir = base_dir / 'outputs'
    zip_path = output_dir / 'pcb_seg_dataset.zip'
    
    print("=== PCB Dataset Zip Creation ===")
    print(f"Source: {dataset_dir}")
    print(f"Output: {zip_path}")
    
    # Verify dataset exists
    if not dataset_dir.exists():
        print(f"Error: Dataset directory not found: {dataset_dir}")
        print("Run the convert_to_yolo_seg.py script first")
        return
    
    # Create output directory
    output_dir.mkdir(parents=True, exist_ok=True)
    
    # Get dataset structure info
    images_train = dataset_dir / 'images' / 'train'
    images_val = dataset_dir / 'images' / 'val'
    labels_train = dataset_dir / 'labels' / 'train'
    labels_val = dataset_dir / 'labels' / 'val'
    yaml_file = dataset_dir / 'dataset.yaml'
    
    train_images = len(list(images_train.glob('*.jpg'))) if images_train.exists() else 0
    val_images = len(list(images_val.glob('*.jpg'))) if images_val.exists() else 0
    train_labels = len(list(labels_train.glob('*.txt'))) if labels_train.exists() else 0
    val_labels = len(list(labels_val.glob('*.txt'))) if labels_val.exists() else 0
    
    print(f"\nDataset structure:")
    print(f"  Train: {train_images} images, {train_labels} labels")
    print(f"  Val:   {val_images} images, {val_labels} labels")
    print(f"  YAML:  {'present' if yaml_file.exists() else 'missing'}")
    
    # Calculate original size
    original_size = get_directory_size(dataset_dir)
    original_size_mb = original_size / 1024 / 1024
    print(f"  Size:  {original_size_mb:.1f} MB")
    
    # Collect all files to zip
    files_to_zip = []
    
    print(f"\nScanning files...")
    for root, dirs, files in os.walk(dataset_dir):
        for file in files:
            file_path = Path(root) / file
            # Create relative path within the pcb_seg folder
            rel_path = file_path.relative_to(dataset_dir.parent)
            files_to_zip.append((file_path, rel_path))
    
    print(f"Found {len(files_to_zip)} files to zip")
    
    # Create zip file with progress bar
    print(f"\nCreating zip file...")
    try:
        with zipfile.ZipFile(zip_path, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as zipf:
            with tqdm(total=len(files_to_zip), desc="Zipping", unit="files") as pbar:
                for file_path, arcname in files_to_zip:
                    try:
                        zipf.write(file_path, arcname)
                        pbar.update(1)
                    except Exception as e:
                        print(f"Warning: Could not zip {file_path}: {e}")
                        continue
        
        # Get final zip size
        zip_size = zip_path.stat().st_size
        zip_size_mb = zip_size / 1024 / 1024
        compression_ratio = (1 - zip_size / original_size) * 100
        
        print(f"\n=== Zip Creation Complete ===")
        print(f"Original size:    {original_size_mb:.1f} MB")
        print(f"Zip size:         {zip_size_mb:.1f} MB")
        print(f"Compression:      {compression_ratio:.1f}%")
        print(f"Zip location:     {zip_path}")
        
        print(f"\n=== Google Drive Upload Instructions ===")
        print(f"1. Open Google Drive in web browser")
        print(f"2. Upload {zip_path.name} to your Drive root or specific folder")
        print(f"3. In Colab, run the 'Load dataset from Google Drive' cell")
        print(f"4. The dataset will be extracted to /content/ for training")
        
        print(f"\n=== Colab Cell Code ===")
        print(f"# Load dataset from Google Drive")
        print(f"import shutil")
        print(f"from pathlib import Path")
        print(f"drive_zip = '/content/drive/MyDrive/{zip_path.name}'")
        print(f"if Path(drive_zip).exists():")
        print(f"    print('Loading dataset from Google Drive...')")
        print(f"    shutil.copy(drive_zip, '/content/{zip_path.name}')")
        print(f"    !unzip -q /content/{zip_path.name} -d /content/pcb-wafer-inspection-pipeline/01_training/data/processed/")
        print(f"    print('Dataset loaded from Drive')")
        print(f"else:")
        print(f"    print('Drive zip not found, check upload path')")
        
    except Exception as e:
        print(f"Error creating zip file: {e}")
        return
    
    print(f"\nDataset zip ready for Google Drive upload!")


if __name__ == "__main__":
    create_dataset_zip()