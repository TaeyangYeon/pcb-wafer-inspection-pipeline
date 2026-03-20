import streamlit as st
from pathlib import Path
import sys

# Add parent directory to path for imports
sys.path.append(str(Path(__file__).parent.parent))

from pages.data_tab import render_data_tab
from pages.train_tab import render_train_tab
from pages.eval_tab import render_eval_tab
from pages.export_tab import render_export_tab
from pages.monitor_tab import render_monitor_tab

st.set_page_config(
    page_title="PCB Wafer Inspection Pipeline",
    layout="wide",
    initial_sidebar_state="expanded"
)

st.title("PCB Wafer Inspection Pipeline")

# Sidebar with dataset status
st.sidebar.title("Project Overview")
st.sidebar.markdown("---")

# Check dataset status
base_path = Path(__file__).parent.parent
pcb_data_path = base_path / "data/raw/pcb"
mvtec_data_path = base_path / "data/raw/mvtec"

# PCB dataset status
if pcb_data_path.exists():
    try:
        from src.data_manager import PCBDataManager
        pcb_manager = PCBDataManager(pcb_data_path)
        pcb_count = len(pcb_manager.get_image_paths("all"))
        pcb_status = f"{pcb_count:,} images"
    except Exception:
        pcb_status = "Error loading"
else:
    pcb_status = "Not found"

# MVTec dataset status
mvtec_categories = []
if mvtec_data_path.exists():
    try:
        mvtec_categories = [d.name for d in mvtec_data_path.iterdir() if d.is_dir()]
        mvtec_status = f"{len(mvtec_categories)} categories"
    except Exception:
        mvtec_status = "Error loading"
else:
    mvtec_status = "Not found"

st.sidebar.subheader("Dataset Status")
st.sidebar.markdown(f"**PCB Dataset:** {pcb_status}")
st.sidebar.markdown(f"**MVTec AD:** {mvtec_status}")

# Model status
patchcore_model = base_path / "models/patchcore"
yolo_model = base_path / "models/yolo_seg.onnx"

st.sidebar.markdown("---")
st.sidebar.subheader("Model Status")
st.sidebar.markdown(f"**PatchCore:** {'Ready' if patchcore_model.exists() else 'Not trained'}")
st.sidebar.markdown(f"**YOLOv8-seg:** {'Ready' if yolo_model.exists() else 'Not trained'}")

st.sidebar.markdown("---")
st.sidebar.caption("AI Vision Inspection Pipeline v2.0")

# Main tabs
tabs = st.tabs(["Data", "Train", "Eval", "Export", "Monitor"])

with tabs[0]:
    render_data_tab()

with tabs[1]:
    render_train_tab()

with tabs[2]:
    render_eval_tab()

with tabs[3]:
    render_export_tab()

with tabs[4]:
    render_monitor_tab()