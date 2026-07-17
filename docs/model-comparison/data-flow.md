# Cơ chế đồng bộ Chunk ID trong hệ thống RBL

Một trong những câu hỏi cốt lõi về tính chính xác của hệ thống là: *"Làm sao mô hình AI lấy ra được chính xác cái ID giống hệt với ID đã gán trong file JSON?"*

Câu trả lời nằm ở luồng dữ liệu (Data Flow) đồng nhất giữa **Quá trình Lập chỉ mục (Indexing)** và **Quá trình Truy vấn (Retrieval)**. Các ID này **không** được lưu trong PostgreSQL, mà được lưu trong **FAISS Vector Database** (Cơ sở dữ liệu Vector chuyên dụng cho AI).

Dưới đây là 4 bước luân chuyển của một ID trong hệ thống:

### Bước 1: Khởi tạo ID (Sinh Ground Truth)
Ban đầu, một kịch bản mồi (Script) sẽ cắt tài liệu gốc ra thành các đoạn văn. Với mỗi đoạn văn, hệ thống sẽ sinh ra một chuỗi UUID ngẫu nhiên (Ví dụ: `fe559863-f68b...`).
- UUID này cùng với câu hỏi được ghi cố định vào file `benchmark.json`. Đây gọi là **Đáp án gốc (Ground Truth)**.

### Bước 2: Lập chỉ mục vào Vector Database (Indexing)
Trước khi chạy Benchmark, hệ thống phải chạy một script gọi là `reindex_xquad_profiles.py`.
Script này làm một nhiệm vụ cực kỳ quan trọng:
1. Nó đọc từng đoạn văn và cái UUID tương ứng trong file JSON.
2. Nó nạp đoạn văn đó vào mô hình Embedding (ví dụ E5) để biến thành Vector số học.
3. Nó lưu Vector này vào **FAISS Database**, đồng thời **nhét cái UUID vào phần Metadata (Dữ liệu đi kèm)** của Vector đó.

*=> Lúc này, FAISS đã ghi nhớ: "Đoạn văn có ý nghĩa A, mang mã số là fe559863...".*

### Bước 3: Truy vấn (Retrieval)
Khi chúng ta chạy `benchmark_runner.py`, hệ thống gửi câu hỏi cho API Tìm kiếm.
1. FAISS Database sẽ lấy câu hỏi biến thành Vector, rồi dùng toán học (Cosine Similarity) để quét xem Vector đoạn văn nào gần giống nhất.
2. Khi tìm thấy đoạn văn giống nhất, **FAISS sẽ móc phần Metadata ra và trả về cho hệ thống chính cái UUID mà nó đã lưu ở Bước 2.**

### Bước 4: Chấm điểm (Evaluation)
Bây giờ `benchmark_runner.py` đang cầm trên tay 2 thứ:
- ID vừa moi được từ FAISS.
- ID gốc nằm trong file `benchmark.json`.

Hệ thống chỉ việc so sánh 2 chuỗi ID này (String Matching). Nếu giống nhau hoàn toàn, chứng tỏ FAISS đã tìm đúng tài liệu mà con người mong muốn => **Context Recall = 1.0!**

---
**💡 Tổng kết:**
Hệ thống không tự "bịa" ra ID, mà chúng ta đã **nhét sẵn ID đó vào FAISS (Vector DB)** dưới dạng Metadata từ lúc Index. Khi FAISS tìm thấy văn bản, nó chỉ đơn giản là "nhả" cái ID đó ra lại cho chúng ta đối chiếu!
