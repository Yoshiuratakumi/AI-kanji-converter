"""
Export cl-tohoku/bert-base-japanese-v3 to ONNX (BertForMaskedLM).

Requirements:
    pip install "transformers[ja]" torch onnx onnxruntime fugashi ipadic

Usage:
    python tools/export_model.py --output-dir models/

Output (place all files in the directory passed to AIC_Initialize):
    models/bert_mlm.onnx   – ONNX model
    models/vocab.txt        – vocabulary (one token per line)
"""

import argparse
import os
import warnings
import torch
from transformers import AutoTokenizer, BertForMaskedLM


MODEL_NAME = "cl-tohoku/bert-base-japanese-v3"


def export(output_dir: str) -> None:
    os.makedirs(output_dir, exist_ok=True)

    # ── Save vocabulary ───────────────────────────────────────────────────────
    print(f"[1/4] Downloading tokenizer: {MODEL_NAME}")
    tokenizer = AutoTokenizer.from_pretrained(MODEL_NAME)
    vocab_path = os.path.join(output_dir, "vocab.txt")
    tokenizer.save_vocabulary(output_dir)
    print(f"      Vocabulary saved → {vocab_path}")

    # ── Load model ────────────────────────────────────────────────────────────
    print(f"[2/4] Downloading model: {MODEL_NAME}")
    model = BertForMaskedLM.from_pretrained(MODEL_NAME)
    model.eval()

    # ── Build dummy input ─────────────────────────────────────────────────────
    seq_len = 32
    dummy_ids  = torch.zeros(1, seq_len, dtype=torch.long)
    dummy_mask = torch.ones (1, seq_len, dtype=torch.long)
    dummy_type = torch.zeros(1, seq_len, dtype=torch.long)

    dummy_ids[0, 0] = tokenizer.cls_token_id
    dummy_ids[0, 1] = tokenizer.mask_token_id
    dummy_ids[0, 2] = tokenizer.sep_token_id
    dummy_mask[0, 3:] = 0

    onnx_path = os.path.join(output_dir, "bert_mlm.onnx")

    # ── Export to ONNX (TorchScript path) ─────────────────────────────────────
    print(f"[3/4] Exporting ONNX model → {onnx_path}")
    warnings.filterwarnings("ignore", category=UserWarning)
    with torch.no_grad():
        torch.onnx.export(
            model,
            args=(dummy_ids, dummy_mask, dummy_type),
            f=onnx_path,
            input_names=["input_ids", "attention_mask", "token_type_ids"],
            output_names=["logits"],
            dynamic_axes={
                "input_ids":      {0: "batch", 1: "seq_len"},
                "attention_mask": {0: "batch", 1: "seq_len"},
                "token_type_ids": {0: "batch", 1: "seq_len"},
                "logits":         {0: "batch", 1: "seq_len"},
            },
            opset_version=14,
            do_constant_folding=True,
            dynamo=False,
        )

    # ── Verify with onnxruntime ───────────────────────────────────────────────
    print("[4/4] Verifying with ONNX Runtime...")
    import onnxruntime as ort
    import numpy as np

    sess = ort.InferenceSession(onnx_path, providers=["CPUExecutionProvider"])
    input_names = [i.name for i in sess.get_inputs()]

    feeds = {k: v.numpy() for k, v in
             {"input_ids": dummy_ids, "attention_mask": dummy_mask,
              "token_type_ids": dummy_type}.items()
             if k in input_names}

    out = sess.run(["logits"], feeds)
    assert out[0].ndim == 3, f"Unexpected output shape: {out[0].shape}"
    print(f"      Output shape: {out[0].shape}  ← [batch, seq_len, vocab_size]")
    print("      Verification OK")

    print("\nDone!")
    print(f"  {onnx_path}")
    print(f"  {vocab_path}")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__,
                                     formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--output-dir", default="models",
                        help="Directory to write bert_mlm.onnx and vocab.txt")
    args = parser.parse_args()
    export(args.output_dir)
