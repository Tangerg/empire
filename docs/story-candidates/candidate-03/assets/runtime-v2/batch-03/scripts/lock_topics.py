#!/usr/bin/env python3
"""Regenerate and verify the isolated C03 Batch 03 combat topic lock."""

from __future__ import annotations

import importlib.util
from pathlib import Path
import sys


BATCH = Path(__file__).resolve().parents[1]
PROCESS = BATCH / "process_batch_03.py"


def load_process():
    module_spec = importlib.util.spec_from_file_location("c03_runtime_v2_b03", PROCESS)
    if module_spec is None or module_spec.loader is None:
        raise RuntimeError(f"cannot load {PROCESS}")
    module = importlib.util.module_from_spec(module_spec)
    sys.modules[module_spec.name] = module
    module_spec.loader.exec_module(module)
    return module


def main() -> None:
    process = load_process()
    process.write_topic_lock()
    print(f"Locked Primary 4 + Batch 02 8 + Batch 03 28 -> {process.TOPIC_LOCK_PATH}")


if __name__ == "__main__":
    main()

