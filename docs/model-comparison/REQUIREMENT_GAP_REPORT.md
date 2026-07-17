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

## 3. Thiết kế benchmark không phát sinh chi phí API

### Profiles cần chạy

Giữ nguyên LLM generator trong mọi profile (Groq Qwen hoặc Llama), temperature
0, cùng prompt, top-k, reranker và benchmark version. Chỉ thay **một biến** cho
mỗi nhóm thí nghiệm:

| Nhóm | Profiles tối thiểu | Chi phí |
| --- | --- | --- |
| Chunking | fixed 300/overlap 50; fixed 500/overlap 80; semantic/heading nếu tài liệu có heading | Local/miễn phí |
| Embedding | `intfloat/multilingual-e5-base`; `vinai/phobert-base`; `BAAI/bge-m3` | Local/miễn phí, đổi bằng RAM/CPU/GPU |
| Generator | 2 model Groq free-tier hiện có | Free-tier, phải throttle/retry |

`PhoBERT-base` không được huấn luyện chuyên cho sentence retrieval như E5/BGE,
nên phải mô tả rõ pooling/prefix trong report và coi là baseline nghiên cứu, không
mặc định kỳ vọng thắng. BGE-M3 nặng hơn nhưng có thể chạy trên máy hiện tại nếu
chạy tuần tự, batch nhỏ và không giữ đồng thời reranker/embedding model khác trên
VRAM; chạy batch offline một lần thay vì index lại mỗi lần người dùng bấm UI.

Không đưa `text-embedding-3-small` vào default run vì là API trả phí. Đây là
quyết định **cost-effective education solution**: E5, PhoBERT và BGE-M3 chạy
local tuần tự, phù hợp GPU 4 GB và không phụ thuộc ngân sách API. Kiến trúc vẫn
design-ready cho OpenAI embedding: khi có ngân sách chỉ cần thêm API key/profile
và chạy một profile tách biệt. Nó là future work, không phải số liệu thực nghiệm
trong phạm vi này.

### RAGAS và đánh giá rẻ

RAGAS có faithfulness, response relevancy, context precision và context recall.
Context recall/precision phải sử dụng `reference_answer` và relevant contexts đã
được gán nhãn; không được suy đoán từ số citation.

- Ưu tiên chạy RAGAS sau batch bằng Groq free-tier qua API tương thích
  OpenAI, tuần tự và có cache/retry; không gọi trong UI. Không chạy Ollama/local
  judge vì VRAM 4 GB nên dành cho embedding benchmark.
- Nếu hết quota hoặc judge không khả dụng, chấm thủ công 20 câu bằng rubric 0/1/2 cho faithfulness,
  đúng/sai và evidence coverage; ghi rõ đây là fallback, không ghi nhầm là RAGAS.
- Cache theo `(benchmark_version, profile_version, model, question_id)`; resume
  run khi lỗi; chỉ gửi context top-k cần thiết để tránh hết free-tier quota.

Tài liệu RAGAS về metric: [Available metrics](https://docs.ragas.io/en/latest/concepts/metrics/available_metrics/).
Groq có free tier nhưng bị giới hạn request/token; runner phải đọc header, backoff
khi nhận `429` và chạy tuần tự/batched nhỏ. [Groq rate limits](https://console.groq.com/docs/rate-limits)

## 4. Tiêu chí nghiệm thu tối thiểu

1. Admin và Teacher đúng scope chạy/nhập được benchmark 20 câu cho dataset được cấp quyền.
2. Mỗi result lưu question, answer, retrieved chunks/ranks, model, prompt,
   chunking/embedding/retrieval profile, latency, token, status và lỗi.
3. Dashboard xuất CSV có trung bình và score từng câu cho ít nhất 3 chunking và
   3 embedding profiles; tất cả run dùng cùng benchmark version.
4. Report có RAGAS bốn metrics, hoặc bảng manual rubric được ghi nhãn fallback;
   có latency, token và citation/evidence coverage.
5. Báo cáo nêu rõ: không benchmark fine-tuned model và `text-embedding-3-small`
   không thuộc default zero-cost path.

## 5. Thứ tự triển khai đề xuất

1. Chốt/lock benchmark JSON, migration cho evaluation entities và Teacher scope.
2. Refactor RAG API thành index cache theo `profile_id`; hoàn tất chunking profiles.
3. Thêm embeddings local, chạy index/retrieval batch và lưu traces/chunk IDs.
4. Thêm batch runner, retry/rate-limit, CSV/JSON export và dashboard.
5. Chạy RAGAS local hoặc rubric fallback, kiểm thử, rồi đóng gói report/screenshot.

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
