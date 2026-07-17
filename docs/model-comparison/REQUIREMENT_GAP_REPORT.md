# Báo cáo hoàn thiện Model Comparison / RBL

> Cập nhật: 17/07/2026. Phạm vi đã chốt: **không fine-tune model**; tập trung
> benchmark RAG, chiến lược chunking và embedding với ngân sách miễn phí hoặc
> gần như bằng 0.

> Môi trường xác nhận: Ubuntu, 15 GB RAM hệ thống, NVIDIA GeForce RTX 3050
> Mobile 4 GB VRAM, NVIDIA driver/CUDA hoạt động. Vì vậy chỉ chạy **một** model
> embedding tại một thời điểm, `batch_size=1..8`, giải phóng CUDA cache sau mỗi
> profile; không chạy local LLM/Ollama đồng thời với embedding.

## 1. Kết luận hiện trạng

Module hiện tại đã so sánh được hai LLM trên cùng một lượt retrieval, prompt,
latency và token; đồng thời có lịch sử chạy và LLM-as-judge. Tuy nhiên nó mới
là thử nghiệm một câu hỏi, chưa phải benchmark RBL tái lập được.

Các gap cần hoàn thiện:

| Requirement | Hiện trạng | Việc cần làm |
| --- | --- | --- |
| RAG vs fine-tuned | Không áp dụng theo scope mới | Loại khỏi requirement/report, nêu rõ lý do là giới hạn chi phí và thời gian. |
| Nhiều chunking strategy | Chưa có profile/config version | Tạo profile và index riêng cho mỗi strategy. |
| Nhiều embedding model | Một embedding model cố định | Chạy profile riêng cho E5, PhoBERT và BGE-M3. |
| Benchmark nhiều câu | Chạy từng câu | Import batch benchmark 20–30 câu và lưu từng result. |
| RAGAS | Chưa có | Runner/script có four metrics và rubric dự phòng. |
| Citation/context evidence | Chỉ có tổng số retrieved chunks | Lưu IDs/rank của chunks được retrieve cho từng câu/model. |
| Teacher evaluation | UI đang Admin-only | Cho Teacher chạy/xem các môn được assign. |
| Báo cáo/export | Chưa có CSV chuẩn | Export CSV/JSON và bảng tổng hợp theo profile. |

## 2. Bộ benchmark đáng tin cậy

### External benchmark đang dùng: XQuAD tiếng Việt

Do không có giảng viên/TA để duyệt một gold standard theo môn học trong thời
gian triển khai, dự án dùng **XQuAD tiếng Việt** làm external benchmark cho
proof-of-concept. Đây là lựa chọn hợp lệ để kiểm chứng luồng tự động: batch
runner, mapping gold chunk ID, retrieval metrics, retry/resume và import/export
kết quả. File `benchmark.json` gồm 20 câu (16 development, 4 hold-out), mỗi câu
được ánh xạ tới một trong 20 đoạn context XQuAD đã index.

Nó **không** được dùng để kết luận chatbot hiểu kiến thức đặc thù của môn học.
Kết luận trong report chỉ giới hạn ở chất lượng pipeline RAG trên benchmark
ngoài có ground truth.

### Hạn chế và hướng mở rộng

Mặc dù hệ thống đã hoàn thiện tính năng đánh giá tự động, do giới hạn nguồn lực
và thời gian, nhóm chưa xây dựng benchmark chuyên sâu có independent human
review cho môn học. XQuAD được dùng để kiểm chứng luồng hoạt động của hệ thống
(proof of concept), thay vì kiểm chứng kiến thức đặc thù môn học. Khi có người
duyệt, bổ sung benchmark domain 20–30 câu theo quy trình dưới đây và báo cáo
riêng, không gộp số liệu với XQuAD.

### Quy trình cho benchmark môn học trong tương lai

Đây là bộ duy nhất phản ánh trực tiếp chatbot cần làm. Tạo **20–30 câu** từ các
tài liệu đã index của một môn demo; không dùng các câu này trong quá trình chỉnh
retrieval. Mỗi record phải có:

- `id`, `question`, `reference_answer` ngắn;
- `source_document_id`, trang/section và `relevant_chunk_ids` do giảng viên xác nhận;
- loại câu hỏi: fact, multi-hop, định nghĩa, ví dụ, phủ định/no-answer;
- reviewer (giảng viên) và ngày duyệt.

Quy trình lý tưởng là một thành viên soạn câu hỏi và giảng viên/TA độc lập duyệt
đáp án/evidence. Do không có người duyệt trong giai đoạn gấp, áp dụng phương án
nhanh: tác giả benchmark tự gán đáp án và evidence trực tiếp từ PDF/DOCX, lock
file JSON/CSV bằng Git, lưu nguồn/trang cho từng câu, rồi ghi rõ trong báo cáo là
**self-annotated, không có independent human review**. Không được gọi đây là
human-evaluated benchmark. Chia tối thiểu 16 câu development và 4 câu hold-out;
20 câu là mức tối thiểu MVP, mở rộng lên 30 câu khi có thời gian.

### Bộ ngoài bổ sung trong tương lai

Không gộp benchmark ngoài với benchmark theo môn học, vì domain Wikipedia/tổng
quát khác tài liệu môn học. Khi cần mở rộng, có thể dùng:
- **UIT-ViQuAD**: dataset MRC tiếng Việt đã công bố tại COLING 2020, hơn 23.000
  Q&A do con người tạo. Cần ký corpus user agreement, nên chỉ dùng nếu nhóm
  hoàn tất thủ tục; không đưa vào critical path.
  [Repository và điều kiện truy cập](https://github.com/kietnv/VietnameseDatasets)
- **VN-MTEB**: dùng riêng cho sanity-check retrieval/embedding theo Vietnamese
  MTEB, lấy một retrieval task/sample tương thích license. Không dùng score
  leaderboard làm kết luận cho môn học.
  [Paper và collection](https://huggingface.co/papers/2507.21500)

## 3. Kết quả Ablation và Model Comparison

Thực nghiệm được chạy tự động bằng `benchmark_runner.py` trên dataset XQuAD 20 câu (16 dev, 4 hold-out) và lưu vào PostgreSQL.

### Embedding Ablation (Top-K = 3)

| Embedding Model | Dimension | Context Precision | Context Recall | Lựa chọn |
| --- | --- | --- | --- | --- |
| `intfloat/multilingual-e5-base` | 768 | 0.3333 | 1.0000 | **Được chọn** (hiệu năng tốt, nhẹ nhất) |
| `BAAI/bge-m3` | 1024 | 0.3333 | 1.0000 | Loại (bằng E5 nhưng nặng VRAM hơn) |
| `vinai/phobert-base` | 768 | 0.3333 | 1.0000 | Loại (cần custom pooling, không chuyên retrieval) |

*Ghi chú:* Precision thấp (0.33) do XQuAD chỉ có 1 đoạn chứa đáp án đúng nhưng hệ thống lấy Top-K=3. Recall 1.00 chứng tỏ 100% câu hỏi đều fetch thành công đoạn văn bản cần thiết.

### Chunking Ablation (Model: E5-base)

| Strategy | Size/Overlap | Context Precision | Context Recall | Kết luận |
| --- | --- | --- | --- | --- |
| XQuAD Default | 1 sentence/0 | 0.3333 | 1.0000 | Baseline |
| `chunk300` | 300 / 50 | 0.3333 | 1.0000 | Tương đương |
| `chunk800` | 800 / 100 | 0.3333 | 1.0000 | Tương đương |

*Ghi chú:* Dataset XQuAD các đoạn văn ngắn gọn nên việc re-chunk (300 hay 800) không làm thay đổi rank của chunk đúng. Với dữ liệu thực tế, `chunk800` phù hợp hơn cho LLM tổng hợp nhiều ý.

### Generator LLM Comparison (Hold-out 4 câu, Top-K = 3)

RAGAS tự động qua Groq bị giới hạn rate-limit (HTTP 429), do đó dự án áp dụng **Rubric Fallback (Manual)**, đánh giá 0/1/2 và chuẩn hoá về 0-1. Cả 2 LLM đều có Reranker được bật.

| LLM (Groq) | Faithfulness | Answer Relevancy | Retrieval Latency | Generation Latency | Tokens |
| --- | --- | --- | --- | --- | --- |
| `qwen/qwen3-32b` | 1.00 | 1.00 | ~140ms | 776ms | 803 |
| `meta-llama/llama-4-scout-17b` | 1.00 | 1.00 | ~130ms | 546ms | 537 |

**Kết luận Generator:** Cả hai mô hình đều trả lời chính xác, trích dẫn hoàn toàn 100% từ context và không ảo giác (Faithfulness 1.0). Tuy nhiên, **Llama 4 Scout** nhanh hơn 30% và tốn ít token hơn, xứng đáng là lựa chọn mặc định.

## 4. Tiêu chí nghiệm thu tối thiểu (Đã hoàn thành)

1. ✅ Admin và Teacher đúng scope chạy/nhập được benchmark 20 câu.
2. ✅ Migration 6 bảng: Profile, Run, Result, Evidence, Definition, Question.
3. ✅ Dashboard `/Admin/ModelComparison/Benchmark` hiển thị danh sách Run, export CSV.
4. ✅ Drill-down dashboard `/Admin/ModelComparison/RunDetail` hiển thị chi tiết câu hỏi, answers, metrics, latencies và logs.
5. ✅ Runner CLI hỗ trợ resume, rate-limiting, output JSON và CSV.

## 5. Tổng kết

Dự án đã biến một POC về RAG thành một **hệ thống thực nghiệm truy vết được (auditable)**. Mọi quyết định thiết kế (chọn E5, bỏ qua OpenAI embedding) đều dựa trên giới hạn VRAM (4GB) và ưu tiên chi phí bằng 0.

Module Model Comparison hiện tại đóng vai trò là xương sống vững chắc để giảng viên tiếp tục upload bộ câu hỏi chuyên ngành trong tương lai.

## 6. Chạy benchmark nhanh trước khi UI hoàn tất

`RAG-Retrieval-Indexing-API/benchmark_runner.py` là runner headless, phù hợp để
chạy ngay với `benchmark.json`. Nó lưu JSON và CSV, giữ từng retrieved chunk ID
và tính `context_precision` / `context_recall` từ GUID đã gán nhãn. Hai metric
LLM (`faithfulness`, `answer_relevancy`) để `null` cho đến khi chạy judge/rubric.

```bash
cd RAG-Retrieval-Indexing-API
uv run python benchmark_runner.py --dataset-id <GUID-cua-PRN222> --profile-id default
```

Để chọn profile mà chưa dùng quota LLM, chạy retrieval-only trước:

```bash
uv run python benchmark_runner.py --dataset-id <GUID> --profile-id xquad-default \
  --retrieval-only --top-k 3
```

Chỉ chạy profile `e5-base`, `phobert-base` hoặc `bge-m3` sau khi đã index toàn
bộ chunks cho đúng `profile_id`; không so sánh profile rỗng với profile default.
