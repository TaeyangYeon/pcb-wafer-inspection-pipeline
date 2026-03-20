import streamlit as st
import json
import time
import os
from pathlib import Path
from PIL import Image
import numpy as np
from typing import Dict, Optional, List
import sys

# Add src to path for imports
BASE_PATH = Path(__file__).parent.parent.parent
sys.path.insert(0, str(BASE_PATH / "src"))

try:
    from patchcore.patchcore import PatchCore
    IMPORTS_AVAILABLE = True
except ImportError as e:
    IMPORTS_AVAILABLE = False
    IMPORT_ERROR = str(e)


def render_export_tab():
    """Render the PatchCore model export interface."""
    
    st.header("Model Export & Deployment")
    
    # Check if imports are available
    if not IMPORTS_AVAILABLE:
        st.error(f"PatchCore dependencies not available: {IMPORT_ERROR}")
        st.info("This tab requires complete PatchCore implementation. The interface is ready but dependencies are missing.")
        return
    
    # Section 1: Model Status + Export Notice
    st.subheader("Model Status & Architecture")
    
    model_info = check_model_status()
    if not model_info:
        st.warning("No trained model found. Go to Train tab first.")
        return
    
    # Display model information
    col1, col2, col3 = st.columns(3)
    with col1:
        st.metric("Category", model_info.get("category", "Unknown"))
    with col2:
        st.metric("Memory Bank Size", f"{model_info.get('memory_bank_size', 0):,} features")
    with col3:
        st.metric("Feature Dimension", model_info.get("feature_dim", "Unknown"))
    
    st.text(f"Trained at: {model_info.get('trained_at', 'Unknown')}")
    st.text(f"Model path: {model_info.get('model_path', 'Unknown')}")
    
    # Export architecture notice
    st.info("""
    **PatchCore Deployment Architecture:**
    
    PatchCore runs in Python FastAPI server, not C# directly.
    The memory bank is loaded by the FastAPI server at startup.
    No ONNX conversion needed (unlike YOLOv8 in Project 1).
    
    **Export Package Contents:**
    - memory_bank.npz: Coreset features for k-NN scoring
    - config.json: Model metadata + calibrated threshold
    - export_info.json: Export metadata and file information
    """)
    
    st.markdown("---")
    
    # Section 2: Threshold Configuration
    st.subheader("Threshold Configuration")
    
    # Get threshold from eval tab session state
    eval_threshold = st.session_state.get("threshold")
    
    if eval_threshold is not None:
        st.success(f"Threshold from Eval tab: {eval_threshold:.3f}")
        default_threshold = eval_threshold
        threshold_source = "Calibrated from test data"
    else:
        st.warning("No threshold set from Eval tab. Using default value. Run evaluation first for optimal results.")
        default_threshold = 0.5
        threshold_source = "Default (not calibrated)"
    
    # Allow manual override
    export_threshold = st.number_input(
        "Export Threshold",
        min_value=0.0,
        max_value=10.0,
        value=default_threshold,
        step=0.01,
        help="Anomaly score threshold for classification. Values above this are defective."
    )
    
    st.caption(f"Source: {threshold_source}")
    
    st.markdown("---")
    
    # Section 3: Export Package
    st.subheader("Export Model Package")
    
    st.write("**Export Configuration:**")
    col_config1, col_config2 = st.columns(2)
    
    with col_config1:
        st.write(f"- Category: {model_info.get('category', 'Unknown')}")
        st.write(f"- Threshold: {export_threshold:.3f}")
        st.write(f"- Memory Bank: {model_info.get('memory_bank_size', 0):,} features")
    
    with col_config2:
        st.write(f"- Feature Dim: {model_info.get('feature_dim', 'Unknown')}")
        st.write(f"- Input Size: {model_info.get('input_size', [224, 224])}")
        st.write(f"- Backbone: {model_info.get('backbone', 'Unknown')}")
    
    # Export button
    if st.button("Export Model Package", type="primary"):
        export_model_package(model_info, export_threshold)
    
    # Show existing exports
    show_exported_models()
    
    st.markdown("---")
    
    # Section 4: Inference Speed Benchmark
    st.subheader("Inference Speed Benchmark")
    
    st.write("""
    **Performance Notes:**
    - Full training speed will be higher on Colab GPU
    - CPU inference speed shown here reflects deployment conditions
    - FastAPI server will cache model loading for better throughput
    """)
    
    if st.button("Run Local Benchmark (10 images)"):
        run_inference_benchmark(model_info)


@st.cache_data
def check_model_status() -> Optional[Dict]:
    """Check if a trained PatchCore model exists and return its info."""
    model_dir = BASE_PATH / "models" / "patchcore"
    
    # Look for model directories (category-specific)
    potential_dirs = []
    if model_dir.exists():
        for item in model_dir.iterdir():
            if item.is_dir() and (item / "config.json").exists():
                potential_dirs.append(item)
    
    if not potential_dirs:
        return None
    
    # Use the most recent model
    latest_model_dir = max(potential_dirs, key=lambda x: x.stat().st_mtime)
    
    # Read config
    config_path = latest_model_dir / "config.json"
    try:
        with open(config_path, 'r') as f:
            config = json.load(f)
        
        config["model_path"] = latest_model_dir
        return config
    except Exception:
        return None


def export_model_package(model_info: Dict, threshold: float):
    """Export model package with metadata."""
    try:
        model_path = model_info["model_path"]
        
        # Create export info
        export_info = create_export_info(model_info, threshold)
        
        # Update config.json with threshold
        update_config_with_threshold(model_path, threshold)
        
        # Create export_info.json
        export_info_path = model_path / "export_info.json"
        with open(export_info_path, 'w') as f:
            json.dump(export_info, f, indent=2)
        
        # Show export results
        st.success("Model package exported successfully!")
        
        # Display file tree
        show_export_file_tree(model_path, export_info)
        
        # Clear any cached data
        st.cache_data.clear()
        
    except Exception as e:
        st.error(f"Export failed: {e}")


def create_export_info(model_info: Dict, threshold: float) -> Dict:
    """Create export information metadata."""
    model_path = model_info["model_path"]
    
    # Get file sizes
    memory_bank_path = model_path / "memory_bank.npz"
    config_path = model_path / "config.json"
    
    memory_bank_size = memory_bank_path.stat().st_size if memory_bank_path.exists() else 0
    config_size = config_path.stat().st_size if config_path.exists() else 0
    
    export_info = {
        "export_timestamp": time.strftime("%Y-%m-%d %H:%M:%S"),
        "package_version": "1.0.0",
        "model_info": {
            "category": model_info.get("category", "unknown"),
            "backbone": model_info.get("backbone", "unknown"),
            "memory_bank_size": model_info.get("memory_bank_size", 0),
            "feature_dim": model_info.get("feature_dim", 0),
            "trained_at": model_info.get("trained_at", "unknown"),
            "export_threshold": threshold
        },
        "files": {
            "memory_bank.npz": {
                "size_bytes": memory_bank_size,
                "size_mb": round(memory_bank_size / (1024 * 1024), 2),
                "description": "Coreset features for k-NN anomaly scoring"
            },
            "config.json": {
                "size_bytes": config_size,
                "size_mb": round(config_size / (1024 * 1024), 4),
                "description": "Model configuration and threshold"
            },
            "export_info.json": {
                "description": "Export metadata and file information"
            }
        },
        "deployment_notes": {
            "target_system": "FastAPI Python server",
            "onnx_required": False,
            "reason": "PatchCore uses memory bank lookup, not neural network inference",
            "loading_method": "PatchCore.load(model_dir) in FastAPI startup"
        }
    }
    
    return export_info


def update_config_with_threshold(model_path: Path, threshold: float):
    """Update config.json with the selected threshold."""
    config_path = model_path / "config.json"
    
    if not config_path.exists():
        raise FileNotFoundError(f"Config file not found: {config_path}")
    
    # Read existing config
    with open(config_path, 'r') as f:
        config = json.load(f)
    
    # Add threshold and export info
    config["export_threshold"] = threshold
    config["exported_at"] = time.strftime("%Y-%m-%d %H:%M:%S")
    config["package_version"] = "1.0.0"
    
    # Write updated config
    with open(config_path, 'w') as f:
        json.dump(config, f, indent=2)


def show_export_file_tree(model_path: Path, export_info: Dict):
    """Show exported files with sizes."""
    st.write("**Exported Files:**")
    
    file_tree = f"""
    📁 {model_path.name}/
    ├── 📄 memory_bank.npz ({export_info['files']['memory_bank.npz']['size_mb']} MB)
    ├── 📄 config.json ({export_info['files']['config.json']['size_mb']} MB)  
    └── 📄 export_info.json (metadata)
    """
    
    st.code(file_tree, language=None)
    
    # File details
    st.write("**File Details:**")
    for filename, info in export_info['files'].items():
        if 'size_mb' in info:
            st.write(f"- **{filename}**: {info['description']} ({info['size_mb']} MB)")
        else:
            st.write(f"- **{filename}**: {info['description']}")


def show_exported_models():
    """Show list of existing exported models."""
    model_dir = BASE_PATH / "models" / "patchcore"
    
    if not model_dir.exists():
        return
    
    exported_models = []
    for item in model_dir.iterdir():
        if item.is_dir() and (item / "export_info.json").exists():
            try:
                with open(item / "export_info.json", 'r') as f:
                    export_info = json.load(f)
                exported_models.append({
                    "name": item.name,
                    "path": item,
                    "info": export_info
                })
            except Exception:
                continue
    
    if exported_models:
        st.write("**Existing Exports:**")
        
        for model in exported_models:
            with st.expander(f"📦 {model['name']} (exported {model['info']['export_timestamp']})"):
                info = model['info']['model_info']
                files = model['info']['files']
                
                col1, col2 = st.columns(2)
                with col1:
                    st.write(f"Category: {info['category']}")
                    st.write(f"Threshold: {info['export_threshold']}")
                    st.write(f"Memory Bank: {info['memory_bank_size']:,} features")
                
                with col2:
                    st.write(f"Backbone: {info['backbone']}")
                    st.write(f"Feature Dim: {info['feature_dim']}")
                    total_size = sum(f.get('size_mb', 0) for f in files.values())
                    st.write(f"Total Size: {total_size:.2f} MB")


def run_inference_benchmark(model_info: Dict):
    """Run local inference speed benchmark."""
    try:
        progress_bar = st.progress(0)
        status_text = st.empty()
        
        # Load model
        status_text.text("Loading PatchCore model...")
        progress_bar.progress(10)
        
        model_path = model_info["model_path"]
        model = PatchCore.load(model_path)
        
        # Get test images
        status_text.text("Finding test images...")
        progress_bar.progress(20)
        
        test_images = find_benchmark_images()
        
        if len(test_images) < 10:
            st.warning(f"Only found {len(test_images)} test images. Need at least 10 for benchmark.")
            return
        
        # Use first 10 images for benchmark
        benchmark_images = test_images[:10]
        
        status_text.text("Running benchmark predictions...")
        progress_bar.progress(30)
        
        # Time predictions
        inference_times = []
        
        for i, img_path in enumerate(benchmark_images):
            try:
                # Load image
                image = Image.open(img_path).convert('RGB')
                
                # Time prediction
                start_time = time.time()
                score, anomaly_map = model.predict(image)
                end_time = time.time()
                
                inference_time_ms = (end_time - start_time) * 1000
                inference_times.append(inference_time_ms)
                
                progress = 30 + ((i + 1) / len(benchmark_images)) * 60
                progress_bar.progress(int(progress))
                
            except Exception as e:
                st.warning(f"Failed to process {img_path}: {e}")
        
        progress_bar.progress(100)
        status_text.text("Benchmark complete!")
        
        # Calculate statistics
        if inference_times:
            avg_time_ms = np.mean(inference_times)
            min_time_ms = np.min(inference_times)
            max_time_ms = np.max(inference_times)
            std_time_ms = np.std(inference_times)
            
            estimated_fps = 1000 / avg_time_ms if avg_time_ms > 0 else 0
            
            # Display results
            st.success("Benchmark Results:")
            
            col1, col2, col3, col4 = st.columns(4)
            with col1:
                st.metric("Avg Time", f"{avg_time_ms:.1f} ms")
            with col2:
                st.metric("Min Time", f"{min_time_ms:.1f} ms")
            with col3:
                st.metric("Max Time", f"{max_time_ms:.1f} ms")
            with col4:
                st.metric("Est. FPS", f"{estimated_fps:.1f}")
            
            st.write(f"**Statistics:** Mean ± Std: {avg_time_ms:.1f} ± {std_time_ms:.1f} ms")
            st.write(f"**Images processed:** {len(inference_times)}")
            
            # Performance notes
            st.info("""
            **Performance Notes:**
            - These are CPU inference times on local machine
            - Production FastAPI server may have different performance characteristics
            - Model loading time not included (done once at server startup)
            - GPU acceleration would significantly improve training but PatchCore inference is CPU-optimized
            """)
        else:
            st.error("No successful predictions in benchmark")
    
    except Exception as e:
        st.error(f"Benchmark failed: {e}")


def find_benchmark_images() -> List[Path]:
    """Find test images for benchmarking."""
    # Look for PCB test images first
    pcb_data_path = BASE_PATH / "data" / "raw" / "pcb" / "JPEGImages"
    
    test_images = []
    
    if pcb_data_path.exists():
        for img_file in pcb_data_path.iterdir():
            if img_file.suffix.lower() in ['.jpg', '.jpeg', '.png']:
                test_images.append(img_file)
                if len(test_images) >= 20:  # Get enough for benchmark
                    break
    
    # If no PCB images, look for MVTec
    if not test_images:
        mvtec_path = BASE_PATH / "data" / "raw" / "mvtec"
        if mvtec_path.exists():
            for category_dir in mvtec_path.iterdir():
                if category_dir.is_dir():
                    test_dir = category_dir / "test" / "good"
                    if test_dir.exists():
                        for img_file in test_dir.iterdir():
                            if img_file.suffix.lower() in ['.jpg', '.jpeg', '.png']:
                                test_images.append(img_file)
                                if len(test_images) >= 20:
                                    break
                    if len(test_images) >= 20:
                        break
    
    return test_images


# Initialize session state for export tracking
if "last_export_time" not in st.session_state:
    st.session_state.last_export_time = None