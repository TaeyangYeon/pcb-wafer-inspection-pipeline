import streamlit as st
import xml.etree.ElementTree as ET
from pathlib import Path
import plotly.express as px
import pandas as pd
from PIL import Image, ImageDraw, ImageFont
import random
import sys

# Add src to path for imports
sys.path.insert(0, str(Path(__file__).parent.parent.parent / "src"))

@st.cache_data
def load_pcb_data_manager():
    """Load PCB data manager with caching for performance."""
    try:
        from data_manager import PCBDataManager
        data_root = Path(__file__).parent.parent.parent / "data" / "raw" / "pcb"
        if data_root.exists():
            return PCBDataManager(data_root)
    except Exception as e:
        st.error(f"Error loading PCB dataset: {e}")
    return None

@st.cache_data
def get_mvtec_categories():
    """Get available MVTec categories with caching."""
    mvtec_root = Path(__file__).parent.parent.parent / "data" / "raw" / "mvtec"
    if mvtec_root.exists():
        return [d.name for d in mvtec_root.iterdir() if d.is_dir()]
    return []

@st.cache_data
def get_mvtec_images(category, subset, split):
    """Get MVTec images for specified category/subset/split."""
    mvtec_root = Path(__file__).parent.parent.parent / "data" / "raw" / "mvtec"
    path = mvtec_root / category / split / subset
    if path.exists():
        images = []
        for ext in ['*.jpg', '*.png', '*.jpeg']:
            images.extend(list(path.glob(ext)))
        return sorted(images)
    return []

def parse_pcb_annotation(annotation_path):
    """Parse PCB XML annotation to extract bounding boxes."""
    try:
        tree = ET.parse(annotation_path)
        root = tree.getroot()
        
        boxes = []
        for obj in root.findall('object'):
            name_elem = obj.find('name')
            if name_elem is not None:
                class_name = name_elem.text.strip()
                
                bbox = obj.find('bndbox')
                if bbox is not None:
                    xmin = int(float(bbox.find('xmin').text))
                    ymin = int(float(bbox.find('ymin').text))
                    xmax = int(float(bbox.find('xmax').text))
                    ymax = int(float(bbox.find('ymax').text))
                    
                    boxes.append({
                        'class_name': class_name,
                        'bbox': (xmin, ymin, xmax, ymax)
                    })
        
        return boxes
    except Exception as e:
        return []

def draw_bounding_boxes(image, boxes):
    """Draw bounding boxes on PIL image."""
    if not boxes:
        return image
    
    # Create a copy to draw on
    img_with_boxes = image.copy()
    draw = ImageDraw.Draw(img_with_boxes)
    
    # Define colors for different classes
    colors = [
        '#FF6B6B', '#4ECDC4', '#45B7D1', '#96CEB4', 
        '#FECA57', '#FF9FF3', '#54A0FF', '#5F27CD'
    ]
    
    # Draw boxes
    for i, box in enumerate(boxes):
        color = colors[i % len(colors)]
        bbox = box['bbox']
        class_name = box['class_name']
        
        # Draw rectangle
        draw.rectangle(bbox, outline=color, width=3)
        
        # Draw label background
        try:
            # Try to use default font
            font = ImageFont.load_default()
        except:
            font = None
        
        # Get text size for label background
        if font:
            bbox_text = draw.textbbox((0, 0), class_name, font=font)
            text_width = bbox_text[2] - bbox_text[0]
            text_height = bbox_text[3] - bbox_text[1]
        else:
            text_width = len(class_name) * 8
            text_height = 12
        
        # Draw label background
        label_bbox = (
            bbox[0], bbox[1] - text_height - 4,
            bbox[0] + text_width + 8, bbox[1]
        )
        draw.rectangle(label_bbox, fill=color)
        
        # Draw text
        text_pos = (bbox[0] + 4, bbox[1] - text_height - 2)
        draw.text(text_pos, class_name, fill='white', font=font)
    
    return img_with_boxes

def render_dataset_overview():
    """Render dataset overview section."""
    st.header("Dataset Overview")
    
    col1, col2 = st.columns(2)
    
    with col1:
        st.subheader("PCB Defect Dataset")
        pcb_manager = load_pcb_data_manager()
        
        if pcb_manager:
            # Get dataset statistics
            split_counts = pcb_manager.get_split_counts()
            class_distribution = pcb_manager.get_class_distribution()
            
            # Display basic stats
            st.metric("Total Images", f"{split_counts['total']:,}")
            st.metric("Train Images", f"{split_counts['train']:,}")
            st.metric("Val Images", f"{split_counts['val']:,}")
            st.metric("Defect Classes", len(class_distribution))
            
            # Class distribution chart
            df = pd.DataFrame([
                {"Class": class_name, "Count": count}
                for class_name, count in class_distribution.items()
            ])
            
            fig = px.bar(
                df, x="Class", y="Count",
                title="PCB Defect Class Distribution",
                color="Class"
            )
            fig.update_xaxes(tickangle=45)
            st.plotly_chart(fig, use_container_width=True)
        else:
            st.error("PCB dataset not found")
    
    with col2:
        st.subheader("MVTec AD Dataset")
        categories = get_mvtec_categories()
        
        if categories:
            st.metric("Categories Available", len(categories))
            
            # Show category details
            category_stats = []
            for category in categories:
                train_normal = get_mvtec_images(category, "good", "train")
                test_normal = get_mvtec_images(category, "good", "test")
                
                # Count defect types
                mvtec_root = Path(__file__).parent.parent.parent / "data" / "raw" / "mvtec"
                test_path = mvtec_root / category / "test"
                defect_types = []
                if test_path.exists():
                    defect_types = [d.name for d in test_path.iterdir() 
                                  if d.is_dir() and d.name != "good"]
                
                category_stats.append({
                    "Category": category,
                    "Train Normal": len(train_normal),
                    "Test Normal": len(test_normal),
                    "Defect Types": len(defect_types)
                })
            
            # Display as table
            df_mvtec = pd.DataFrame(category_stats)
            st.dataframe(df_mvtec, use_container_width=True)
            
            # Available categories
            st.markdown("**Categories:** " + ", ".join(categories))
        else:
            st.error("MVTec dataset not found")

def render_pcb_browser():
    """Render PCB dataset browser section."""
    st.header("PCB Dataset Browser")
    
    pcb_manager = load_pcb_data_manager()
    if not pcb_manager:
        st.error("PCB dataset not available")
        return
    
    # Controls
    col1, col2, col3 = st.columns([2, 2, 1])
    
    with col1:
        class_names = pcb_manager.get_class_names()
        selected_class = st.selectbox("Select Defect Class", ["All"] + class_names)
    
    with col2:
        num_samples = st.slider("Number of Samples", 1, 10, 4)
    
    with col3:
        if st.button("Refresh Samples"):
            st.rerun()
    
    # Get sample images
    try:
        if selected_class == "All":
            samples = pcb_manager.get_sample_items(num_samples)
        else:
            # Filter by class - get more samples and filter
            all_samples = pcb_manager.get_sample_items(num_samples * 3)
            samples = [s for s in all_samples if s['class_name'] == selected_class][:num_samples]
        
        if not samples:
            st.warning(f"No samples found for class: {selected_class}")
            return
        
        # Display images in grid
        cols = st.columns(min(len(samples), 3))
        
        for i, sample in enumerate(samples):
            with cols[i % 3]:
                try:
                    # Load image
                    img_path = sample['image_path']
                    ann_path = sample['annotation_path']
                    
                    image = Image.open(img_path)
                    
                    # Parse annotation and draw bounding boxes
                    boxes = parse_pcb_annotation(ann_path)
                    if boxes:
                        image_with_boxes = draw_bounding_boxes(image, boxes)
                    else:
                        image_with_boxes = image
                    
                    # Display image
                    st.image(
                        image_with_boxes,
                        caption=f"{sample['class_name']} | {img_path.name}",
                        use_container_width=True
                    )
                    
                    # Show defect info
                    if boxes:
                        st.caption(f"Defects: {len(boxes)} objects")
                    else:
                        st.caption("No defects detected")
                        
                except Exception as e:
                    st.error(f"Error loading image: {e}")
    
    except Exception as e:
        st.error(f"Error getting samples: {e}")

def render_mvtec_browser():
    """Render MVTec dataset browser section."""
    st.header("MVTec Dataset Browser")
    
    categories = get_mvtec_categories()
    if not categories:
        st.error("MVTec dataset not available")
        return
    
    # Controls
    col1, col2, col3 = st.columns([2, 2, 1])
    
    with col1:
        selected_category = st.selectbox("Select Category", categories)
    
    with col2:
        subset_type = st.radio("Sample Type", ["Normal (good)", "Defect samples"])
    
    with col3:
        num_samples = st.slider("Number of samples", 1, 8, 4)
    
    if st.button("Refresh MVTec Samples"):
        st.rerun()
    
    # Determine subset and split based on selection
    if subset_type == "Normal (good)":
        subset = "good"
        split = "train"  # Use training normal images
    else:
        subset = None  # Will select random defect type
        split = "test"
    
    try:
        if subset == "good":
            images = get_mvtec_images(selected_category, subset, split)
        else:
            # Get available defect types
            mvtec_root = Path(__file__).parent.parent.parent / "data" / "raw" / "mvtec"
            test_path = mvtec_root / selected_category / "test"
            defect_types = []
            if test_path.exists():
                defect_types = [d.name for d in test_path.iterdir() 
                              if d.is_dir() and d.name != "good"]
            
            # Collect images from all defect types
            images = []
            for defect_type in defect_types:
                defect_images = get_mvtec_images(selected_category, defect_type, split)
                for img in defect_images:
                    images.append((img, defect_type))
            
            # Shuffle and sample
            random.shuffle(images)
            images = images[:num_samples]
        
        if not images:
            st.warning(f"No images found for {selected_category} - {subset_type}")
            return
        
        # Sample random images
        if isinstance(images[0], tuple):
            # Defect images with labels
            sampled = images[:num_samples]
        else:
            # Normal images
            random.shuffle(images)
            sampled = [(img, "good") for img in images[:num_samples]]
        
        # Display images in grid
        cols = st.columns(min(len(sampled), 3))
        
        for i, (img_path, label) in enumerate(sampled):
            with cols[i % 3]:
                try:
                    image = Image.open(img_path)
                    
                    st.image(
                        image,
                        caption=f"{selected_category} | {label} | {img_path.name}",
                        use_container_width=True
                    )
                    
                except Exception as e:
                    st.error(f"Error loading image: {e}")
    
    except Exception as e:
        st.error(f"Error browsing MVTec dataset: {e}")

def render_data_tab():
    """Render the complete Data tab for PCB and MVTec dataset browsing."""
    
    # Dataset Overview
    render_dataset_overview()
    
    st.markdown("---")
    
    # PCB Dataset Browser
    render_pcb_browser()
    
    st.markdown("---")
    
    # MVTec Dataset Browser
    render_mvtec_browser()