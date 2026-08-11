#!/usr/bin/env python3
"""Reproducible entry point for the isolated C03 Runtime V2 Batch 03."""

from __future__ import annotations

from pathlib import Path
import runpy


PROCESS = Path(__file__).resolve().parents[1] / "process_batch_03.py"


if __name__ == "__main__":
    runpy.run_path(str(PROCESS), run_name="__main__")

