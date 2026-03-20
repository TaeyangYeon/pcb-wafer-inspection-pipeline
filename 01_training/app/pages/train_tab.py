import streamlit as st
import threading
import time
from pathlib import Path
import sys

# Add src to path for imports
BASE_PATH = Path(__file__).parent.parent.parent
sys.path.insert(0, str(BASE_PATH / "src"))

from patchcore.patchcore import PatchCore
from data_manager import PCBDataManager


def render_train_tab():
    """Render the PatchCore training interface."""
    
    st.header("PatchCore Training Interface")
    
    # Section 1: Training Mode Notice
    st.info("""
    **Training Mode Guide:**
    - **Local mode**: Use max 20-30 images for quick validation (CPU ~3min)
    - **Full training**: Use Google Colab notebook for complete dataset training
    """)
    
    st.link_button(
        "Open in Google Colab",
        "https://colab.research.google.com/github/example/patchcore-notebook",
        help="Full training with GPU acceleration"
    )
    
    st.markdown("---")
    
    # Section 2: PatchCore Training Configuration
    st.subheader("PatchCore Training Configuration")
    
    with st.form("patchcore_config"):
        col1, col2 = st.columns(2)
        
        with col1:
            category = st.selectbox(
                "Category",
                ["transistor", "grid"],
                help="PCB defect category to train on"
            )
            
            backbone = st.selectbox(
                "Backbone",
                ["wide_resnet50_2"],
                help="Feature extraction backbone"
            )
            
            coreset_ratio = st.slider(
                "Coreset Ratio",
                0.01, 0.25, 0.01, 0.01,
                help="Fraction of patches to keep in memory bank"
            )
        
        with col2:
            max_training_images = st.number_input(
                "Max Training Images",
                0, 1000, 0,
                help="0 = use all images, set small number for local CPU testing"
            )
            
            output_directory = st.text_input(
                "Output Directory",
                "01_training/models/patchcore/",
                help="Directory to save trained model"
            )
        
        st.markdown("---")
        
        # Training buttons
        col_quick, col_full = st.columns(2)
        
        with col_quick:
            quick_test = st.form_submit_button(
                "Quick Test (20 images)",
                type="primary",
                help="Train on 20 images for local CPU validation"
            )
        
        with col_full:
            full_train = st.form_submit_button(
                "Full Local Train",
                help="Train on all images (may take >30min on CPU)"
            )
    
    # Handle form submission
    if quick_test or full_train:
        # Set max images for quick test
        if quick_test:
            max_images = 20
            st.warning("Quick test mode: Using 20 images for validation")
        else:
            max_images = max_training_images if max_training_images > 0 else None
            if max_images is None:
                st.warning("Full training: This may take >30 minutes on CPU")
        
        # Store configuration in session state
        st.session_state.training_config = {
            "category": category,
            "backbone": backbone,
            "coreset_ratio": coreset_ratio,
            "max_training_images": max_images,
            "output_directory": output_directory
        }
        
        # Start training
        start_training()
    
    st.markdown("---")
    
    # Section 3: Training Progress
    if st.session_state.get("training_status") == "running":
        st.subheader("Training Progress")
        
        # Progress bar
        progress_placeholder = st.empty()
        
        # Status text
        status_placeholder = st.empty()
        
        # Update progress display
        update_training_progress(progress_placeholder, status_placeholder)
    
    # Section 4: Training Results
    if st.session_state.get("training_status") == "completed":
        st.subheader("Training Results")
        
        results = st.session_state.get("training_results", {})
        
        # Summary table
        col1, col2, col3 = st.columns(3)
        with col1:
            st.metric("Images Processed", results.get("images_processed", "N/A"))
        with col2:
            st.metric("Memory Bank Size", results.get("memory_bank_size", "N/A"))
        with col3:
            st.metric("Training Time", f"{results.get('training_time', 0):.1f}s")
        
        st.success(f"Model saved to {results.get('output_path', 'N/A')}")
        st.balloons()


def start_training():
    """Start training in a separate thread."""
    st.session_state.training_status = "running"
    st.session_state.training_progress = 0
    st.session_state.training_step = "Initializing..."
    
    # Start training thread
    training_thread = threading.Thread(target=train_patchcore_thread)
    training_thread.daemon = True
    training_thread.start()


def train_patchcore_thread():
    """Training function that runs in a separate thread."""
    try:
        config = st.session_state.training_config
        
        # Update progress: Data loading
        st.session_state.training_step = "Loading dataset..."
        st.session_state.training_progress = 10
        
        # Initialize data manager
        data_root = BASE_PATH / "data" / "raw" / "pcb"
        if not data_root.exists():
            raise FileNotFoundError(f"PCB data not found at {data_root}")
        
        data_manager = PCBDataManager(data_root)
        
        # Get training images for the specified category
        all_images = data_manager.get_image_paths("train")
        
        # Filter by category if needed (for now use all images)
        training_images = all_images
        
        # Limit number of images if specified
        if config["max_training_images"]:
            training_images = training_images[:config["max_training_images"]]
        
        # Update progress: Model initialization
        st.session_state.training_step = "Initializing PatchCore model..."
        st.session_state.training_progress = 20
        
        # Initialize PatchCore
        patchcore = PatchCore(
            backbone=config["backbone"],
            coreset_ratio=config["coreset_ratio"],
            device="cpu"  # For local training
        )
        
        # Update progress: Training
        st.session_state.training_step = "Training PatchCore model..."
        st.session_state.training_progress = 30
        
        # Train the model
        start_time = time.time()
        patchcore.fit(training_images, category=config["category"])
        training_time = time.time() - start_time
        
        # Update progress: Saving
        st.session_state.training_step = "Saving model..."
        st.session_state.training_progress = 90
        
        # Create output directory
        output_dir = BASE_PATH / config["output_directory"] / config["category"]
        output_dir.mkdir(parents=True, exist_ok=True)
        
        # Save model
        patchcore.save(output_dir)
        
        # Update progress: Complete
        st.session_state.training_step = "Training complete!"
        st.session_state.training_progress = 100
        
        # Store results
        st.session_state.training_results = {
            "images_processed": len(training_images),
            "memory_bank_size": patchcore.memory_bank_size,
            "training_time": training_time,
            "output_path": str(output_dir)
        }
        
        # Mark as completed
        st.session_state.training_status = "completed"
        
    except Exception as e:
        st.session_state.training_status = "error"
        st.session_state.training_error = str(e)


def update_training_progress(progress_placeholder, status_placeholder):
    """Update the training progress display."""
    progress = st.session_state.get("training_progress", 0)
    step = st.session_state.get("training_step", "Training...")
    status = st.session_state.get("training_status", "running")
    
    if status == "running":
        progress_placeholder.progress(progress / 100)
        status_placeholder.write(f"**Current step:** {step}")
        
        # Auto-refresh every 2 seconds
        time.sleep(2)
        st.rerun()
        
    elif status == "error":
        progress_placeholder.empty()
        error_msg = st.session_state.get("training_error", "Unknown error")
        status_placeholder.error(f"Training failed: {error_msg}")
        
    elif status == "completed":
        progress_placeholder.progress(1.0)
        status_placeholder.success("Training completed successfully!")


# Initialize session state
if "training_status" not in st.session_state:
    st.session_state.training_status = "idle"
if "training_progress" not in st.session_state:
    st.session_state.training_progress = 0
if "training_step" not in st.session_state:
    st.session_state.training_step = ""