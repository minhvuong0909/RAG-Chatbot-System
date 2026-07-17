"""Low-cost, resumable RAG benchmark runner.

Reads docs/model-comparison/benchmark.json, calls the local retrieval API and a
Groq OpenAI-compatible chat model, then writes auditable JSON and CSV results.
It deliberately calculates retrieval precision/recall from the annotated chunk
GUIDs. Faithfulness and answer relevancy remain null until an LLM judge or
manual rubric is applied; they are never fabricated.
"""

from __future__ import annotations

import argparse
import csv
import json
import os
import time
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

DEFAULT_MODEL = "qwen/qwen3-32b"
PROMPT_VERSION = "rag-v1"


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description="Run an auditable RAG benchmark")
    parser.add_argument("--dataset-id", required=True, help="PRN222 Dataset GUID")
    parser.add_argument("--benchmark", default="../docs/model-comparison/benchmark.json")
    parser.add_argument("--rag-url", default="http://127.0.0.1:8000")
    parser.add_argument("--groq-model", default=DEFAULT_MODEL)
    parser.add_argument("--profile-id", default="default", help="Index/cache profile already built in RAG API")
    parser.add_argument("--top-k", type=int, default=10)
    parser.add_argument("--disable-rerank", action="store_true", help="Use hybrid retrieval without the cross-encoder reranker.")
    parser.add_argument("--holdout-only", action="store_true")
    parser.add_argument("--retrieval-only", action="store_true", help="Score retrieval only; do not spend LLM quota.")
    parser.add_argument("--validate-only", action="store_true", help="Validate the benchmark schema and exit.")
    parser.add_argument("--output-dir", default="../docs/model-comparison/results")
    parser.add_argument("--resume", help="Existing JSON result file to resume")
    return parser.parse_args()


def load_benchmark(path: Path) -> dict[str, Any]:
    data = json.loads(path.read_text(encoding="utf-8"))
    questions = data.get("questions", [])
    if not data.get("name") or not data.get("version") or len(questions) < 1:
        raise ValueError("Benchmark must contain name, version, and at least one question.")
    required = {"sortOrder", "question", "referenceAnswer", "relevantChunkIds", "isHoldout"}
    sort_orders: set[int] = set()
    for index, question in enumerate(questions, start=1):
        missing = required.difference(question)
        if missing or not question["relevantChunkIds"]:
            raise ValueError(f"Question {index} is missing: {', '.join(sorted(missing)) or 'relevantChunkIds'}")
        if not isinstance(question["sortOrder"], int) or question["sortOrder"] < 1:
            raise ValueError(f"Question {index} has an invalid sortOrder.")
        if question["sortOrder"] in sort_orders:
            raise ValueError(f"Duplicate sortOrder: {question['sortOrder']}")
        sort_orders.add(question["sortOrder"])
    return data


def validate_gold_ids_exist(benchmark: dict[str, Any], dataset_id: str) -> None:
    """Fail early when a benchmark was generated against another DB rebuild."""
    import uuid
    expected = {chunk_id for question in benchmark["questions"] for chunk_id in question["relevantChunkIds"]}
    if not expected or any(not isinstance(chunk_id, str) for chunk_id in expected):
        raise ValueError("Benchmark does not contain valid gold chunk IDs.")
    # Dataset IDs are only checked structurally here. The retrieval loop then
    # verifies actual returned metadata; DB existence is validated by importer.
    uuid.UUID(dataset_id)


def retrieve(client: httpx.Client, rag_url: str, dataset_id: str, question: str, profile_id: str, top_k: int, enable_rerank: bool) -> dict[str, Any]:
    response = client.post(
        f"{rag_url.rstrip('/')}/retrieve",
        json={
            "query": question,
            "dataset_id": dataset_id,
            "top_k": top_k,
            "semantic_weight": 0.7,
            "lexical_weight": 0.3,
            "enable_rerank": enable_rerank,
            "profile_id": profile_id,
        },
        timeout=90,
    )
    response.raise_for_status()
    return response.json()


def generate(client: httpx.Client, question: str, documents: list[dict[str, Any]], model: str) -> tuple[str, dict[str, int]]:
    api_key = os.getenv("GROQ_API_KEY")
    if not api_key:
        raise RuntimeError("GROQ_API_KEY is required to generate benchmark answers.")
    context = "\n\n---\n\n".join(document.get("page_content", "") for document in documents)
    prompt = (
        "Trả lời bằng tiếng Việt chỉ dựa trên ngữ cảnh. Nếu không có thông tin, "
        "hãy nói không tìm thấy trong tài liệu; không bịa.\n\n"
        f"Ngữ cảnh:\n{context}\n\nCâu hỏi: {question}\nCâu trả lời:"
    )
    response = client.post(
        "https://api.groq.com/openai/v1/chat/completions",
        headers={"Authorization": f"Bearer {api_key}"},
        json={"model": model, "temperature": 0, "messages": [{"role": "user", "content": prompt}]},
        timeout=120,
    )
    if response.status_code == 429:
        retry_after = float(response.headers.get("retry-after", "5"))
        time.sleep(max(1, retry_after))
        return generate(client, question, documents, model)
    response.raise_for_status()
    payload = response.json()
    usage = payload.get("usage", {})
    return payload["choices"][0]["message"]["content"].strip(), {
        "input_tokens": int(usage.get("prompt_tokens", 0)),
        "output_tokens": int(usage.get("completion_tokens", 0)),
        "total_tokens": int(usage.get("total_tokens", 0)),
    }


def score_retrieval(expected_ids: list[str], documents: list[dict[str, Any]]) -> tuple[float, float, list[str]]:
    retrieved_ids = [str(d.get("metadata", {}).get("id", "")) for d in documents]
    expected = set(expected_ids)
    hits = set(retrieved_ids).intersection(expected)
    precision = len(hits) / len(retrieved_ids) if retrieved_ids else 0.0
    recall = len(hits) / len(expected) if expected else 0.0
    return precision, recall, retrieved_ids


def summarize(results: list[dict[str, Any]]) -> dict[str, Any]:
    completed = [item for item in results if item["status"] == "Completed"]
    metric = lambda key: round(sum(float(item.get(key, 0.0)) for item in completed) / len(completed), 4) if completed else None
    return {
        "total": len(results),
        "completed": len(completed),
        "failed": len(results) - len(completed),
        "mean_context_precision": metric("context_precision"),
        "mean_context_recall": metric("context_recall"),
    }


def write_csv(path: Path, results: list[dict[str, Any]]) -> None:
    fields = ["sort_order", "is_holdout", "status", "question", "answer", "reference_answer", "context_precision", "context_recall", "retrieval_latency_ms", "generation_latency_ms", "total_tokens", "error"]
    with path.open("w", newline="", encoding="utf-8-sig") as file:
        writer = csv.DictWriter(file, fieldnames=fields)
        writer.writeheader()
        for result in results:
            writer.writerow({field: result.get(field, "") for field in fields})


def main() -> None:
    args = parse_args()
    global httpx
    try:
        import httpx  # Installed by the RAG API environment / uv sync.
    except ImportError as error:
        raise RuntimeError("Run this script with the RAG API environment: uv run python benchmark_runner.py ...") from error
    benchmark = load_benchmark(Path(args.benchmark))
    validate_gold_ids_exist(benchmark, args.dataset_id)
    if args.validate_only:
        print(f"Valid benchmark: {benchmark['name']} {benchmark['version']} ({len(benchmark['questions'])} questions)")
        return
    output_dir = Path(args.output_dir)
    output_dir.mkdir(parents=True, exist_ok=True)

    previous: dict[int, dict[str, Any]] = {}
    if args.resume:
        previous = {item["sort_order"]: item for item in json.loads(Path(args.resume).read_text(encoding="utf-8"))["results"]}

    selected = [q for q in benchmark["questions"] if not args.holdout_only or q["isHoldout"]]
    results: list[dict[str, Any]] = []
    with httpx.Client() as client:
        for item in selected:
            if item["sortOrder"] in previous:
                results.append(previous[item["sortOrder"]])
                continue
            started = time.perf_counter()
            result: dict[str, Any] = {
                "sort_order": item["sortOrder"], "is_holdout": item["isHoldout"],
                "question": item["question"], "reference_answer": item["referenceAnswer"],
                "expected_chunk_ids": item["relevantChunkIds"], "status": "Failed",
                "faithfulness": None, "answer_relevancy": None,
            }
            try:
                retrieval_started = time.perf_counter()
                response = retrieve(client, args.rag_url, args.dataset_id, item["question"], args.profile_id, args.top_k, not args.disable_rerank)
                result["retrieval_latency_ms"] = round((time.perf_counter() - retrieval_started) * 1000)
                precision, recall, retrieved_ids = score_retrieval(item["relevantChunkIds"], response["documents"])
                result.update({"context_precision": precision, "context_recall": recall, "retrieved_chunk_ids": retrieved_ids, "trace": response.get("trace", [])})
                if not response["documents"]:
                    raise RuntimeError("Retrieval returned no context; answer generation was skipped.")
                if not retrieved_ids or any(not chunk_id for chunk_id in retrieved_ids):
                    raise RuntimeError("Retrieval response is missing chunk metadata.id; result is not auditable.")
                if args.retrieval_only:
                    result["answer"] = ""
                    result["generation_latency_ms"] = 0
                    result["input_tokens"] = result["output_tokens"] = result["total_tokens"] = 0
                    result["status"] = "Completed"
                else:
                    generation_started = time.perf_counter()
                    answer, usage = generate(client, item["question"], response["documents"], args.groq_model)
                    result.update(usage)
                    result["answer"] = answer
                    result["generation_latency_ms"] = round((time.perf_counter() - generation_started) * 1000)
                    result["status"] = "Completed"
            except Exception as error:  # Preserve individual failure and continue batch.
                result["error"] = str(error)
            result["elapsed_ms"] = round((time.perf_counter() - started) * 1000)
            results.append(result)
            time.sleep(1)  # Free-tier friendly pacing.

    stamp = datetime.now(timezone.utc).strftime("%Y%m%dT%H%M%SZ")
    base = output_dir / f"{benchmark['name']}-{benchmark['version']}-{args.profile_id}-{stamp}"
    report = {
        "benchmark_name": benchmark["name"], "benchmark_version": benchmark["version"],
        "dataset_id": args.dataset_id, "profile_id": args.profile_id, "model": args.groq_model,
        "prompt_version": PROMPT_VERSION, "created_at": datetime.now(timezone.utc).isoformat(),
        "mode": "retrieval-only" if args.retrieval_only else "generation",
        "enable_rerank": not args.disable_rerank,
        "summary": summarize(results),
        "results": results,
    }
    base.with_suffix(".json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    write_csv(base.with_suffix(".csv"), results)
    print(f"Wrote {base.with_suffix('.json')} and {base.with_suffix('.csv')}")


if __name__ == "__main__":
    main()
