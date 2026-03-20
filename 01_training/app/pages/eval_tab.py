import streamlit as st
import numpy as np
import plotly.graph_objects as go
import plotly.express as px
from pathlib import Path
from PIL import Image
import cv2
import json
import sys
from typing import List, Tuple, Dict, Optional
from sklearn.metrics import roc_curve, auc, confusion_matrix

# Add src to path for imports
BASE_PATH = Path(__file__).parent.parent.parent
sys.path.insert(0, str(BASE_PATH / "src"))

try:
    from patchcore.patchcore import PatchCore
    IMPORTS_AVAILABLE = True
except ImportError as e:
    IMPORTS_AVAILABLE = False
    IMPORT_ERROR = str(e)


def render_eval_tab():
    """Render the PatchCore evaluation interface."""
    
    st.header("PatchCore Model Evaluation")
    
    # Check if imports are available
    if not IMPORTS_AVAILABLE:
        st.error(f"PatchCore dependencies not available: {IMPORT_ERROR}")
        st.info("This tab requires complete PatchCore implementation. The interface is ready but dependencies are missing.")
        return
    
    st.info("""
    **Evaluation Guide:**
    For meaningful evaluation results, use a model trained on the full dataset via Google Colab.
    Local quick-test models (20 images) will have poor discrimination between normal and defective images.
    """)
    
    # Section 1: Model Status
    st.subheader("Model Status")
    
    model_info = check_model_status()
    if not model_info:
        st.warning("No trained model found. Go to Train tab first.")
        return
    
    # Display model configuration
    col1, col2, col3 = st.columns(3)
    with col1:
        st.metric("Category", model_info.get("category", "Unknown"))
    with col2:
        st.metric("Memory Bank Size", f"{model_info.get('memory_bank_size', 0):,}")
    with col3:
        st.metric("Backbone", model_info.get("backbone", "Unknown"))
    
    st.text(f"Trained at: {model_info.get('trained_at', 'Unknown')}")
    
    st.markdown("---")
    
    # Section 2: Run Evaluation
    st.subheader("Run Evaluation on Test Set")
    
    if st.button("Run Evaluation on Test Set", type="primary"):
        run_evaluation(model_info["model_path"])
    
    # Check if evaluation results exist
    if "eval_results" not in st.session_state:
        st.info("Click 'Run Evaluation' to analyze model performance on test images.")
        return
    
    results = st.session_state.eval_results
    
    st.markdown("---")
    
    # Section 3: Score Distribution
    st.subheader("Score Distribution")
    
    threshold = st.session_state.get("threshold", get_optimal_threshold(results))
    
    fig_dist = create_score_distribution_plot(results, threshold)
    st.plotly_chart(fig_dist, use_container_width=True)
    
    st.markdown("---")
    
    # Section 4: ROC Curve + AUROC
    st.subheader("ROC Curve Analysis")
    
    col_roc, col_auroc = st.columns([3, 1])
    
    with col_roc:
        fig_roc = create_roc_curve(results)
        st.plotly_chart(fig_roc, use_container_width=True)
    
    with col_auroc:
        auroc = results["auroc"]
        if auroc >= 0.85:
            st.success(f"AUROC: {auroc:.3f}")
        else:
            st.error(f"AUROC: {auroc:.3f}")
        
        st.metric("Max Score", f"{results['max_score']:.3f}")
        st.metric("Min Score", f"{results['min_score']:.3f}")
    
    st.markdown("---")
    
    # Section 5: Threshold Selection
    st.subheader("Threshold Selection")
    
    # Threshold slider
    threshold = st.slider(
        "Anomaly Score Threshold",
        min_value=0.0,
        max_value=results['max_score'],
        value=get_optimal_threshold(results),
        step=0.01,
        help="Scores above this threshold are classified as defective"
    )
    
    st.session_state.threshold = threshold
    
    # Confusion matrix and metrics
    metrics = calculate_metrics(results, threshold)
    
    col_cm, col_metrics = st.columns(2)
    
    with col_cm:
        st.write("**Confusion Matrix**")
        cm_data = {
            "Predicted": ["Normal", "Normal", "Defective", "Defective"],
            "Actual": ["Normal", "Defective", "Normal", "Defective"],
            "Count": [metrics["tn"], metrics["fn"], metrics["fp"], metrics["tp"]]
        }
        st.dataframe(cm_data, hide_index=True)
    
    with col_metrics:
        st.write("**Performance Metrics**")
        col_prec, col_rec = st.columns(2)
        with col_prec:
            st.metric("Precision", f"{metrics['precision']:.3f}")
            st.metric("Recall", f"{metrics['recall']:.3f}")
        with col_rec:
            st.metric("F1-Score", f"{metrics['f1']:.3f}")
            st.metric("Accuracy", f"{metrics['accuracy']:.3f}")
    
    st.markdown("---")
    
    # Section 6: Sample Predictions
    st.subheader("Sample Predictions")
    
    sample_type = st.selectbox(
        "Select sample type to view:",
        ["Normal samples", "Defect samples"]
    )
    
    show_sample_predictions(results, threshold, sample_type)


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


def run_evaluation(model_path: Path):
    """Run evaluation on test set and cache results."""
    try:
        # Show progress
        progress_bar = st.progress(0)
        status_text = st.empty()
        
        # Load model
        status_text.text("Loading PatchCore model...")
        progress_bar.progress(10)
        
        model = PatchCore.load(model_path)
        
        # Find test data
        status_text.text("Finding test images...")
        progress_bar.progress(20)
        
        mvtec_path = BASE_PATH / "data" / "raw" / "mvtec"
        category = model.category
        
        normal_images, defect_images = find_test_images(mvtec_path, category)
        
        if not normal_images and not defect_images:
            st.error(f"No test images found for category '{category}' in {mvtec_path}")
            return
        
        # Run predictions
        status_text.text("Running predictions on normal images...")
        progress_bar.progress(30)
        
        normal_scores = []
        normal_paths = []
        normal_maps = []
        
        for i, img_path in enumerate(normal_images):
            try:
                image = Image.open(img_path).convert('RGB')
                score, anomaly_map = model.predict(image)
                normal_scores.append(score)
                normal_paths.append(img_path)
                normal_maps.append(anomaly_map)
                
                progress = 30 + (i / len(normal_images)) * 30
                progress_bar.progress(int(progress))
            except Exception as e:
                st.warning(f"Failed to process {img_path}: {e}")
        
        status_text.text("Running predictions on defect images...")
        progress_bar.progress(60)
        
        defect_scores = []
        defect_paths = []
        defect_maps = []
        
        for i, img_path in enumerate(defect_images):
            try:
                image = Image.open(img_path).convert('RGB')
                score, anomaly_map = model.predict(image)
                defect_scores.append(score)
                defect_paths.append(img_path)
                defect_maps.append(anomaly_map)
                
                progress = 60 + (i / len(defect_images)) * 30
                progress_bar.progress(int(progress))
            except Exception as e:
                st.warning(f"Failed to process {img_path}: {e}")
        
        # Calculate ROC and AUROC
        status_text.text("Calculating ROC curve...")
        progress_bar.progress(90)
        
        # Combine scores and labels
        all_scores = normal_scores + defect_scores
        all_labels = [0] * len(normal_scores) + [1] * len(defect_scores)  # 0=normal, 1=defect
        
        if len(set(all_labels)) < 2:
            st.error("Need both normal and defect images for evaluation")
            return
        
        fpr, tpr, thresholds = roc_curve(all_labels, all_scores)
        auroc = auc(fpr, tpr)
        
        # Store results
        results = {
            "normal_scores": normal_scores,
            "defect_scores": defect_scores,
            "normal_paths": normal_paths,
            "defect_paths": defect_paths,
            "normal_maps": normal_maps,
            "defect_maps": defect_maps,
            "fpr": fpr.tolist(),
            "tpr": tpr.tolist(),
            "roc_thresholds": thresholds.tolist(),
            "auroc": auroc,
            "all_scores": all_scores,
            "all_labels": all_labels,
            "max_score": max(all_scores),
            "min_score": min(all_scores)
        }
        
        st.session_state.eval_results = results
        
        progress_bar.progress(100)
        status_text.text("Evaluation complete!")
        
        st.success(f"Evaluated {len(normal_scores)} normal and {len(defect_scores)} defect images")
        st.rerun()
        
    except Exception as e:
        st.error(f"Evaluation failed: {e}")


def find_test_images(mvtec_path: Path, category: str) -> Tuple[List[Path], List[Path]]:
    """Find normal and defect test images for given category."""
    test_dir = mvtec_path / category / "test"
    
    normal_images = []
    defect_images = []
    
    if not test_dir.exists():
        return normal_images, defect_images
    
    # Normal images
    normal_dir = test_dir / "good"
    if normal_dir.exists():
        for img_file in normal_dir.iterdir():
            if img_file.suffix.lower() in ['.jpg', '.jpeg', '.png']:
                normal_images.append(img_file)
    
    # Defect images (all subdirectories except 'good')
    for defect_dir in test_dir.iterdir():
        if defect_dir.is_dir() and defect_dir.name != "good":
            for img_file in defect_dir.iterdir():
                if img_file.suffix.lower() in ['.jpg', '.jpeg', '.png']:
                    defect_images.append(img_file)
    
    return sorted(normal_images), sorted(defect_images)


def create_score_distribution_plot(results: Dict, threshold: float) -> go.Figure:
    """Create overlaid histogram of normal vs defect scores."""
    fig = go.Figure()
    
    # Normal scores histogram
    fig.add_trace(go.Histogram(
        x=results["normal_scores"],
        name="Normal",
        opacity=0.7,
        marker_color="green",
        nbinsx=30
    ))
    
    # Defect scores histogram
    fig.add_trace(go.Histogram(
        x=results["defect_scores"],
        name="Defective",
        opacity=0.7,
        marker_color="red",
        nbinsx=30
    ))
    
    # Threshold line
    fig.add_vline(
        x=threshold,
        line_dash="dash",
        line_color="blue",
        annotation_text=f"Threshold: {threshold:.3f}"
    )
    
    fig.update_layout(
        title="Anomaly Score Distribution",
        xaxis_title="Anomaly Score",
        yaxis_title="Count",
        barmode="overlay",
        legend=dict(orientation="h", yanchor="bottom", y=1.02, xanchor="right", x=1)
    )
    
    return fig


def create_roc_curve(results: Dict) -> go.Figure:
    """Create ROC curve plot."""
    fig = go.Figure()
    
    fig.add_trace(go.Scatter(
        x=results["fpr"],
        y=results["tpr"],
        mode="lines",
        name=f"ROC Curve (AUROC = {results['auroc']:.3f})",
        line=dict(color="blue", width=2)
    ))
    
    # Diagonal reference line
    fig.add_trace(go.Scatter(
        x=[0, 1],
        y=[0, 1],
        mode="lines",
        name="Random Classifier",
        line=dict(color="gray", dash="dash")
    ))
    
    fig.update_layout(
        title="ROC Curve",
        xaxis_title="False Positive Rate",
        yaxis_title="True Positive Rate",
        width=400,
        height=400
    )
    
    return fig


def get_optimal_threshold(results: Dict) -> float:
    """Calculate optimal threshold using Youden's index."""
    fpr = np.array(results["fpr"])
    tpr = np.array(results["tpr"])
    thresholds = np.array(results["roc_thresholds"])
    
    # Youden's index: TPR - FPR
    j_scores = tpr - fpr
    optimal_idx = np.argmax(j_scores)
    
    return float(thresholds[optimal_idx])


def calculate_metrics(results: Dict, threshold: float) -> Dict:
    """Calculate confusion matrix and performance metrics for given threshold."""
    all_scores = np.array(results["all_scores"])
    all_labels = np.array(results["all_labels"])
    
    # Predictions: 1 if score > threshold, 0 otherwise
    predictions = (all_scores > threshold).astype(int)
    
    # Confusion matrix
    tn, fp, fn, tp = confusion_matrix(all_labels, predictions).ravel()
    
    # Metrics
    precision = tp / (tp + fp) if (tp + fp) > 0 else 0
    recall = tp / (tp + fn) if (tp + fn) > 0 else 0
    f1 = 2 * (precision * recall) / (precision + recall) if (precision + recall) > 0 else 0
    accuracy = (tp + tn) / (tp + tn + fp + fn)
    
    return {
        "tp": tp, "fp": fp, "fn": fn, "tn": tn,
        "precision": precision,
        "recall": recall,
        "f1": f1,
        "accuracy": accuracy
    }


def show_sample_predictions(results: Dict, threshold: float, sample_type: str):
    """Show sample predictions with images and heatmaps."""
    if sample_type == "Normal samples":
        scores = results["normal_scores"]
        paths = results["normal_paths"]
        maps = results["normal_maps"]
        expected_label = "PASS"
    else:
        scores = results["defect_scores"]
        paths = results["defect_paths"]
        maps = results["defect_maps"]
        expected_label = "FAIL"
    
    if not scores:
        st.warning(f"No {sample_type.lower()} available")
        return
    
    # Select 4 random samples
    indices = np.random.choice(len(scores), size=min(4, len(scores)), replace=False)
    
    cols = st.columns(4)
    
    for i, idx in enumerate(indices):
        with cols[i]:
            score = scores[idx]
            path = paths[idx]
            anomaly_map = maps[idx]
            
            # Prediction based on threshold
            prediction = "FAIL" if score > threshold else "PASS"
            
            # Load and display image
            try:
                image = Image.open(path).convert('RGB')
                st.image(image, caption=f"{path.name}", use_column_width=True)
                
                # Create heatmap overlay
                heatmap_rgb = create_heatmap_overlay(np.array(image), anomaly_map)
                st.image(heatmap_rgb, caption="Anomaly Heatmap", use_column_width=True)
                
                # Score and prediction
                st.write(f"**Score:** {score:.3f}")
                
                if prediction == expected_label:
                    st.success(f"Correct: {prediction}")
                else:
                    st.error(f"Incorrect: {prediction}")
                
            except Exception as e:
                st.error(f"Failed to load {path.name}: {e}")


def create_heatmap_overlay(image: np.ndarray, anomaly_map: np.ndarray) -> np.ndarray:
    """Create heatmap overlay on original image."""
    # Normalize anomaly map to 0-255
    heatmap = cv2.normalize(anomaly_map, None, 0, 255, cv2.NORM_MINMAX).astype(np.uint8)
    
    # Resize heatmap to match image size
    h, w = image.shape[:2]
    heatmap_resized = cv2.resize(heatmap, (w, h))
    
    # Apply colormap
    heatmap_colored = cv2.applyColorMap(heatmap_resized, cv2.COLORMAP_JET)
    
    # Convert BGR to RGB
    heatmap_rgb = cv2.cvtColor(heatmap_colored, cv2.COLOR_BGR2RGB)
    
    # Blend with original image
    alpha = 0.4
    overlay = cv2.addWeighted(image, 1 - alpha, heatmap_rgb, alpha, 0)
    
    return overlay


# Initialize session state
if "eval_results" not in st.session_state:
    st.session_state.eval_results = None
if "threshold" not in st.session_state:
    st.session_state.threshold = 0.5