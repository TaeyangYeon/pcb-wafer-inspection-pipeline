# PatchCore Grid Anomaly Detection Model

This directory contains the trained PatchCore model for grid/trace anomaly detection.

## Expected Files

Place the following files here after Program 1 (PatchCore) training completes:

- `memory_bank.npz` - PatchCore memory bank with normal feature embeddings
- `config.json` - Training configuration and hyperparameters
  - Image size, patch size, backbone model info
  - Feature dimension, memory bank size
  - Anomaly threshold value

## Training Source

This model is trained using:
- Script: `01_training/scripts/train_patchcore_grid.py`
- Dataset: Grid/trace normal samples (extracted from component detection)
- Method: PatchCore with coreset sampling and Mahalanobis distance

## Usage

The FastAPI server will load these files from this directory for inference.
The C# inspection system will call `/anomaly/grid` endpoint for detection.

## File Sizes

Expected sizes:
- `memory_bank.npz`: 50-200 MB (depends on coreset size)
- `config.json`: <1 KB (configuration only)