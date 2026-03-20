import streamlit as st

def render_export_tab():
    """Render the Export tab for ONNX export and memory bank export."""
    
    st.header("Model Export")
    
    st.info("ONNX export + memory bank export coming soon")
    
    # Placeholder sections
    col1, col2 = st.columns(2)
    
    with col1:
        st.subheader("PatchCore Export")
        st.markdown("**Export Format:**")
        st.markdown("- Memory bank: .npz file")
        st.markdown("- Feature extractor: ONNX")
        st.markdown("- Config: JSON metadata")
        st.markdown("- Model version: timestamp")
        st.markdown("- Target: C# inference client")
    
    with col2:
        st.subheader("YOLOv8-seg Export")
        st.markdown("**Export Format:**")
        st.markdown("- Model: ONNX opset 21")
        st.markdown("- Input size: 640x640")
        st.markdown("- Output: boxes + masks + scores")
        st.markdown("- Optimization: dynamic axes")
        st.markdown("- Target: OnnxRuntime")