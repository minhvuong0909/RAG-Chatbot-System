# Bảng Số Liệu RAGAS Benchmark (Model Comparison History)

*Dữ liệu được trích xuất từ hệ thống chấm điểm tự động (Ablation Study) chạy trên 20 câu hỏi XQuAD.*

| Thời điểm | Profile / Cấu hình | Mô hình Sinh chữ (Generator) | Trạng thái | Số câu hoàn thành | Context Precision | Context Recall | Faithfulness | Answer Relevancy |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 17/07 18:17 | `xquad-e5-chunk800` | (Retrieval Only) | Đã hoàn thành | 20 / 20 | 0.317 | 0.950 | - | - |
| 17/07 18:16 | `xquad-e5-chunk300` | (Retrieval Only) | Đã hoàn thành | 20 / 20 | 0.317 | 0.950 | - | - |
| 17/07 18:11 | `xquad-e5-base` | `meta-llama/llama-4-scout-17b` | Đã hoàn thành | 4 / 4 | 0.333 | 1.000 | 1.000 | 1.000 |
| 17/07 18:08 | `xquad-e5-base` | `qwen/qwen3-32b` | Đã hoàn thành | 4 / 4 | 0.333 | 1.000 | 1.000 | 1.000 |
| 17/07 18:08 | `xquad-phobert-base` | (Retrieval Only) | Đã hoàn thành | 20 / 20 | 0.267 | 0.800 | - | - |
| 17/07 18:08 | `xquad-bge-m3` | (Retrieval Only) | Đã hoàn thành | 20 / 20 | 0.333 | 1.000 | - | - |
| 17/07 18:07 | `xquad-e5-base` | (Retrieval Only) | Đã hoàn thành | 20 / 20 | 0.333 | 1.000 | - | - |

**Ghi chú:**
- **Context Precision & Recall:** Đánh giá độ chính xác của bước Tìm kiếm tài liệu (Retrieval).
- **Faithfulness & Answer Relevancy:** Các chỉ số của framework RAGAS dùng để đánh giá chất lượng câu trả lời cuối cùng (Độ trung thực và Độ liên quan).
- **Retrieval Only:** Chế độ tiết kiệm chi phí, chỉ chạy tìm kiếm tài liệu (không gọi LLM sinh chữ) để đo lường Precision và Recall. Mức độ ưu tiên cao nhất là tối đa hoá Recall để AI không bao giờ bị thiếu kiến thức nền.
