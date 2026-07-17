# Hướng Dẫn Chạy Thực Nghiệm Model Comparison Từ A-Z (E2E Guide)

Tài liệu này dành cho các thành viên trong nhóm (Teammates) muốn tự tay chạy lại quy trình đánh giá Chatbot RAG (Model Comparison) từ đầu đến cuối trên máy cá nhân, sau đó đưa kết quả lên Dashboard C# Web để xem.

---

## Bước 1: Khởi động hệ thống Docker

Đầu tiên, hãy chắc chắn bạn đã tải bộ code mới nhất từ nhánh `dev`. Hệ thống này yêu cầu Docker để chạy.

1. Mở Terminal / PowerShell tại thư mục gốc của dự án (`RAG-Chatbot-System`).
2. Gõ lệnh để khởi động (lần đầu sẽ mất vài phút để tải và build):
   ```bash
   docker compose build
   docker compose up -d
   ```
3. Chờ cho đến khi tất cả 3 container (`rag-postgres-db`, `rag-retrieval-api`, `rag-web-app`) báo trạng thái xanh lá (Running/Healthy).

---

## Bước 2: Nạp dữ liệu Lịch sử (Tuỳ chọn nhưng cực kỳ khuyến nghị)

Lúc này, Database PostgreSQL trong Docker của bạn đang trống không. Nếu bạn muốn xem ngay 100+ kết quả báo cáo thực nghiệm XQuAD mà bạn Kiệt đã làm sẵn, hãy bơm dữ liệu Seed vào:

**Với Mac / Linux / Git Bash (Windows):**
```bash
cat docs/model-comparison/seed_data.sql | docker exec -i rag-postgres-db psql -U postgres -d RagChatbotSystemDb
```

**Với PowerShell (Windows):**
```powershell
Get-Content docs\model-comparison\seed_data.sql | docker exec -i rag-postgres-db psql -U postgres -d RagChatbotSystemDb
```

👉 *Kiểm tra thử:* Mở trình duyệt `http://localhost:5259`, đăng nhập quyền Admin, vào menu **Quản trị > So sánh mô hình > Benchmark RAG**, chọn Dataset "XQuAD Benchmark" để thấy dữ liệu ồ ạt hiện ra!

---

## Bước 3: Tạo Dữ Liệu Thực Nghiệm (Tự động nạp FAISS)

Vì bộ nhớ Vector Database (FAISS) không được đẩy lên GitHub do quá nặng, nên bạn chưa có file tài liệu nào trong AI. Hãy chạy Script dưới đây để hệ thống tự động: Tạo 3 đoạn văn bản về PRN222 -> Nạp vào FAISS -> Xuất ra file đề thi mẫu (`prn222-benchmark.json`).

1. Chuyển vào thư mục chứa code AI:
   ```bash
   cd RAG-Retrieval-Indexing-API
   ```
2. Chạy kịch bản tự động nạp:
   ```bash
   uv run python create_sample_benchmark.py
   ```
   *(Màn hình sẽ báo: ✅ Đã lưu 3 đoạn văn vào FAISS thành công! và ✅ Đã tạo xong file prn222-benchmark.json)*

---

## Bước 4: Chạy Thực Nghiệm Tự Động (Benchmark Runner)

Bây giờ bạn đã có Dataset (file json đề thi) và tài liệu trong FAISS. Hãy để hệ thống làm bài thi!

Trong cùng thư mục `RAG-Retrieval-Indexing-API`, chạy lệnh:
```bash
uv run python benchmark_runner.py --dataset-id "PRN222-Sample-Benchmark" --profile-id "prn222-sample-profile" --file "prn222-benchmark.json"
```

Hệ thống sẽ chạy qua 3 câu hỏi, tìm kiếm bằng E5 và gọi AI (Qwen/Llama) để trả lời.
Khi chạy xong 100%, bạn sẽ thấy nó sinh ra 2 file (1 cái `.json` và 1 cái `.csv`) nằm trong thư mục `docs/model-comparison/results/`.

---

## Bước 5: Xem Kết Quả (Dashboard)

File Terminal chỉ dành cho dân kỹ thuật, bây giờ hãy đưa thành quả này lên Web cho Giảng viên xem!

1. Trở lại Web Dashboard (`http://localhost:5259/Admin/ModelComparison/Benchmark`).
2. Nhìn lên góc trên bên trái:
   - **Dataset:** Chọn `PRN222-Sample-Benchmark`. (Hoặc nếu không thấy, cứ làm bước Browse trước).
   - Bấm nút **Browse...** (Hoặc *Choose File*).
3. Chọn file `.json` vừa được sinh ra ở Bước 4 (nằm ở `docs/model-comparison/results/`).
4. Bấm nút màu xanh **Nhập kết quả**.
5. Kéo xuống dưới bảng **Lịch sử Batch**, bạn sẽ thấy một dòng kết quả mới toanh vừa xuất hiện!
6. Bấm vào **Mốc thời gian màu xanh dương** (Ví dụ `18/07 10:30`) ở ngoài cùng bên trái để chuyển sang trang **Run Detail**. Tại đây, bạn sẽ tận mắt thấy câu hỏi, đáp án mẫu, đáp án do AI sinh ra, và điểm số của từng câu!

🎉 **HOÀN TẤT E2E BENCHMARK!** Bạn đã hiểu tường tận cách hệ thống vận hành.
