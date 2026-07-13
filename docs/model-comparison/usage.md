# Hướng dẫn dùng trang So sánh mô hình AI

## Truy cập

Đăng nhập với role **Admin** hoặc **Teacher** → vào tab "So sánh mô hình" trong khu vực quản trị (`/Admin/ModelComparison`).

## Quy trình

1. Chọn môn học (dataset đã có tài liệu được index).
2. Nhập câu hỏi thử nghiệm.
3. Tick chọn các model muốn so sánh (Groq, Gemini, Ollama).
4. Bấm "Chạy thử nghiệm".

## Cách hoạt động phía sau

- Hệ thống retrieval (FAISS + BM25 + rerank qua RAG API) chạy **đúng 1 lần** cho câu hỏi, lấy ra context chung.
- Cùng 1 prompt (system instruction + context + câu hỏi) được gửi tới từng model đã chọn — đảm bảo công bằng khi so sánh, không model nào nhận context khác nhau.
- Mỗi model đo riêng: thời gian phản hồi (ms, đo bằng `Stopwatch`), số token input/output/tổng, và cờ đánh dấu số token là số thật từ API hay chỉ ước lượng (`WasActualTokenUsage`).

## Đọc kết quả

Mỗi model hiển thị 1 card: tên model, trạng thái (Thành công/Lỗi), thời gian phản hồi, số token, và nội dung câu trả lời đầy đủ để so sánh trực tiếp bằng mắt.

## Giới hạn hiện tại

- Kết quả **chưa được lưu vào DB** — chỉ hiển thị trong phiên chạy hiện tại, mất khi tải lại trang. Nếu cần lưu lịch sử để làm báo cáo/chứng minh sau này, cần bổ sung bảng `ModelComparisonRun` (chưa làm ở giai đoạn này).
- Chưa có chấm điểm chất lượng tự động (LLM-as-judge) — người dùng tự đọc và đánh giá bằng mắt dựa trên câu trả lời hiển thị.
- Đây là công cụ nội bộ cho Admin/Teacher thử nghiệm, không phải một phần của luồng chat mà sinh viên sử dụng.
