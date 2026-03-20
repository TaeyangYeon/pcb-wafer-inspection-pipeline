import streamlit as st
import json
import numpy as np
import pandas as pd
import plotly.graph_objects as go
import plotly.express as px
from pathlib import Path
from datetime import datetime, timedelta
from typing import List, Tuple, Dict, Optional
from scipy.stats import ks_2samp
import sys

# Add src to path for imports
BASE_PATH = Path(__file__).parent.parent.parent
sys.path.insert(0, str(BASE_PATH / "src"))


def render_monitor_tab():
    """Render the drift monitoring and retraining recommendation interface."""
    
    st.header("Model Monitoring & Drift Detection")
    
    st.info("""
    **Drift Monitoring:**
    Monitors whether the model's behavior changes over time as real-world data distribution shifts.
    Uses Kolmogorov-Smirnov test to compare baseline vs recent score distributions.
    """)
    
    # Section 1: Deployment Status
    st.subheader("Deployment Status")
    
    # Check for real inference log
    inference_log_path = BASE_PATH / "models" / "patchcore" / "inference_log.jsonl"
    baseline_scores = get_baseline_scores()
    
    if not inference_log_path.exists() or not baseline_scores:
        # Demo mode with simulated data
        st.info("**Demo Mode:** Showing simulated drift scenario (no real inference log found)")
        
        recent_scores, log_data = generate_simulated_data(baseline_scores)
        total_predictions = len(recent_scores)
        date_range = "Simulated: Last 7 days"
        
    else:
        # Real deployment data
        log_data = load_inference_log(inference_log_path)
        recent_scores = [entry["score"] for entry in log_data]
        
        total_predictions = len(recent_scores)
        if total_predictions > 0:
            first_date = log_data[0]["timestamp"]
            last_date = log_data[-1]["timestamp"]
            date_range = f"{first_date} to {last_date}"
        else:
            date_range = "No data"
        
        st.success("**Production Mode:** Using real inference log data")
    
    # Display deployment metrics
    col_status1, col_status2, col_status3 = st.columns(3)
    with col_status1:
        st.metric("Total Predictions", f"{total_predictions:,}")
    with col_status2:
        st.metric("Date Range", date_range)
    with col_status3:
        baseline_count = len(baseline_scores) if baseline_scores else 0
        st.metric("Baseline Size", f"{baseline_count:,}")
    
    if not recent_scores:
        st.warning("No recent inference data available for monitoring")
        return
    
    st.markdown("---")
    
    # Section 2: Score Distribution Comparison
    st.subheader("Score Distribution Comparison")
    
    if baseline_scores:
        fig_hist = create_distribution_comparison(baseline_scores, recent_scores)
        st.plotly_chart(fig_hist, use_container_width=True)
    else:
        st.warning("No baseline scores available. Run evaluation first to establish baseline.")
        return
    
    st.markdown("---")
    
    # Section 3: KS-Test Result
    st.subheader("Statistical Drift Detection")
    
    ks_stat, p_value = perform_ks_test(baseline_scores, recent_scores)
    drift_status = get_drift_status(p_value)
    
    # Display KS-test results
    col_ks1, col_ks2, col_ks3 = st.columns(3)
    
    with col_ks1:
        st.metric("KS Statistic", f"{ks_stat:.4f}")
    with col_ks2:
        st.metric("P-Value", f"{p_value:.6f}")
    with col_ks3:
        if drift_status["level"] == "stable":
            st.success(drift_status["message"])
        elif drift_status["level"] == "monitor":
            st.warning(drift_status["message"])
        else:  # critical
            st.error(drift_status["message"])
    
    # Explanation
    st.write("**Interpretation:**")
    st.write("- KS Statistic: Measures maximum difference between distributions (0-1)")
    st.write("- P-Value: Probability distributions are the same (lower = more different)")
    st.write("- Threshold: p < 0.05 indicates significant distribution shift")
    
    st.markdown("---")
    
    # Section 4: Score Trend Over Time
    st.subheader("Score Trend Analysis")
    
    if len(recent_scores) >= 10:  # Need enough data for trend
        fig_trend = create_trend_chart(log_data, baseline_scores)
        st.plotly_chart(fig_trend, use_container_width=True)
    else:
        st.info("Need at least 10 data points for trend analysis")
    
    st.markdown("---")
    
    # Section 5: Retraining Action
    if p_value < 0.05:
        st.subheader("Retraining Recommendation")
        
        if p_value < 0.01:
            st.error("**Critical Drift Detected:** Score distribution has shifted significantly")
        else:
            st.warning("**Moderate Drift Detected:** Score distribution showing signs of change")
        
        st.write("**Recommended Actions:**")
        st.write("1. Review recent inference results for quality issues")
        st.write("2. Collect new training data representing current conditions")  
        st.write("3. Retrain PatchCore model with updated dataset")
        st.write("4. Re-run evaluation to establish new baseline")
        
        # Retraining button
        st.link_button(
            "🚀 Open Colab Retraining Notebook",
            "https://colab.research.google.com/github/example/patchcore-retraining",
            help="Opens Google Colab notebook for model retraining"
        )
        
        # Retraining instructions
        with st.expander("📋 Detailed Retraining Instructions"):
            st.markdown("""
            **Step 1: Data Collection**
            - Gather new normal images representing current production conditions
            - Ensure at least 100+ images for robust retraining
            
            **Step 2: Model Retraining**
            - Use Colab notebook for GPU-accelerated training
            - Update dataset paths in training script
            - Train new PatchCore model with fresh data
            
            **Step 3: Evaluation & Deployment**
            - Run evaluation on new test set to establish updated baseline
            - Export new model package with calibrated threshold
            - Deploy updated model to FastAPI inference server
            
            **Step 4: Monitoring Reset**
            - Clear inference_log.jsonl to start fresh monitoring
            - Update baseline scores from new evaluation results
            """)


def get_baseline_scores() -> List[float]:
    """Get baseline scores from evaluation results."""
    if "eval_results" in st.session_state:
        eval_results = st.session_state.eval_results
        return eval_results.get("all_scores", [])
    
    # Try to load from saved evaluation if available
    model_dir = BASE_PATH / "models" / "patchcore"
    if model_dir.exists():
        for item in model_dir.iterdir():
            if item.is_dir():
                eval_file = item / "baseline_scores.json"
                if eval_file.exists():
                    try:
                        with open(eval_file, 'r') as f:
                            data = json.load(f)
                        return data.get("scores", [])
                    except Exception:
                        continue
    
    return []


def load_inference_log(log_path: Path) -> List[Dict]:
    """Load inference log from JSONL file."""
    log_entries = []
    
    try:
        with open(log_path, 'r') as f:
            for line in f:
                line = line.strip()
                if line:
                    try:
                        entry = json.loads(line)
                        log_entries.append(entry)
                    except json.JSONDecodeError:
                        continue
    except Exception:
        pass
    
    return log_entries


def generate_simulated_data(baseline_scores: List[float]) -> Tuple[List[float], List[Dict]]:
    """Generate simulated drift data for demo purposes."""
    if not baseline_scores:
        # Create synthetic baseline if none exists
        np.random.seed(42)
        baseline_scores = np.random.normal(2.5, 0.8, 100).tolist()
    
    # Generate recent scores with drift (shift mean upward)
    np.random.seed(123)
    baseline_mean = np.mean(baseline_scores)
    baseline_std = np.std(baseline_scores)
    
    # Simulate gradual drift over time
    n_recent = 100
    drift_magnitude = 0.5  # Shift mean by this amount
    
    recent_scores = []
    log_data = []
    
    for i in range(n_recent):
        # Progressive drift: starts small, increases over time
        drift_factor = (i / n_recent) * drift_magnitude
        shifted_mean = baseline_mean + drift_factor
        
        # Add some randomness
        score = np.random.normal(shifted_mean, baseline_std)
        score = max(0, score)  # Ensure non-negative
        
        recent_scores.append(score)
        
        # Create log entry
        timestamp = (datetime.now() - timedelta(days=7-i*7/n_recent)).strftime("%Y-%m-%d %H:%M:%S")
        result = "NG" if score > baseline_mean + 2*baseline_std else "OK"
        
        log_data.append({
            "timestamp": timestamp,
            "score": score,
            "result": result
        })
    
    return recent_scores, log_data


def create_distribution_comparison(baseline_scores: List[float], recent_scores: List[float]) -> go.Figure:
    """Create overlaid histogram comparing baseline vs recent score distributions."""
    fig = go.Figure()
    
    # Baseline distribution
    fig.add_trace(go.Histogram(
        x=baseline_scores,
        name="Baseline (Test Set)",
        opacity=0.7,
        marker_color="blue",
        nbinsx=30,
        histnorm="probability density"
    ))
    
    # Recent distribution  
    fig.add_trace(go.Histogram(
        x=recent_scores,
        name="Recent (Production)",
        opacity=0.7,
        marker_color="orange", 
        nbinsx=30,
        histnorm="probability density"
    ))
    
    fig.update_layout(
        title="Anomaly Score Distribution Comparison",
        xaxis_title="Anomaly Score",
        yaxis_title="Probability Density",
        barmode="overlay",
        legend=dict(orientation="h", yanchor="bottom", y=1.02, xanchor="right", x=1),
        height=400
    )
    
    return fig


def perform_ks_test(baseline_scores: List[float], recent_scores: List[float]) -> Tuple[float, float]:
    """Perform Kolmogorov-Smirnov test for distribution comparison."""
    ks_statistic, p_value = ks_2samp(baseline_scores, recent_scores)
    return ks_statistic, p_value


def get_drift_status(p_value: float) -> Dict[str, str]:
    """Determine drift status based on p-value."""
    if p_value >= 0.05:
        return {
            "level": "stable",
            "message": "Model distribution stable",
            "color": "green"
        }
    elif p_value >= 0.01:
        return {
            "level": "monitor", 
            "message": "Monitor closely",
            "color": "yellow"
        }
    else:
        return {
            "level": "critical",
            "message": "Retraining recommended",
            "color": "red"
        }


def create_trend_chart(log_data: List[Dict], baseline_scores: List[float]) -> go.Figure:
    """Create time series chart showing score trend over time."""
    # Extract timestamps and scores
    timestamps = [entry["timestamp"] for entry in log_data]
    scores = [entry["score"] for entry in log_data]
    
    # Convert timestamps to datetime for proper plotting
    try:
        timestamps = pd.to_datetime(timestamps)
    except:
        # Fallback to sequential indices if timestamp parsing fails
        timestamps = list(range(len(scores)))
    
    # Calculate rolling average (window=10)
    window_size = min(10, len(scores) // 5 + 1)
    rolling_scores = pd.Series(scores).rolling(window=window_size, center=True).mean()
    
    fig = go.Figure()
    
    # Individual scores (dots)
    fig.add_trace(go.Scatter(
        x=timestamps,
        y=scores,
        mode="markers",
        name="Individual Scores",
        marker=dict(size=4, opacity=0.6, color="lightblue"),
        hovertemplate="Time: %{x}<br>Score: %{y:.3f}<extra></extra>"
    ))
    
    # Rolling average (line)
    fig.add_trace(go.Scatter(
        x=timestamps,
        y=rolling_scores,
        mode="lines",
        name=f"Rolling Average ({window_size} points)",
        line=dict(width=3, color="red"),
        hovertemplate="Time: %{x}<br>Avg Score: %{y:.3f}<extra></extra>"
    ))
    
    # Baseline mean (horizontal line)
    baseline_mean = np.mean(baseline_scores)
    fig.add_hline(
        y=baseline_mean,
        line_dash="dash",
        line_color="blue",
        annotation_text=f"Baseline Mean: {baseline_mean:.3f}",
        annotation_position="top left"
    )
    
    fig.update_layout(
        title="Anomaly Score Trend Over Time",
        xaxis_title="Time",
        yaxis_title="Anomaly Score", 
        legend=dict(orientation="h", yanchor="bottom", y=1.02, xanchor="right", x=1),
        height=400
    )
    
    return fig


# Initialize session state for monitoring
if "monitoring_enabled" not in st.session_state:
    st.session_state.monitoring_enabled = True
if "last_drift_check" not in st.session_state:
    st.session_state.last_drift_check = None