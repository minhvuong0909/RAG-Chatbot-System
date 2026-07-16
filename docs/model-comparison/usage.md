# Hướng dẫn dùng trang So sánh mô hình AI

## Truy cập

Đăng nhập với role **Admin** hoặc **Teacher** → bấm link **"So sánh mô hình"** (có ở menu các trang Admin và ở header các trang Teacher hay dùng), hoặc vào thẳng `/Admin/ModelComparison`.

- **Admin**: thấy được lịch sử của **tất cả** môn học.
- **Teacher**: chỉ thấy môn học **được phân công** (qua `TeacherSubjectAssignment`) — cả ở dropdown chọn môn lẫn ở lịch sử/biểu đồ.

## Quy trình chạy thử nghiệm

1. Chọn **môn học** (dataset đã có tài liệu được index).
2. Nhập **câu hỏi thử nghiệm** (tối đa 1000 ký tự).
3. Tick chọn các **model** muốn so sánh (hiện tại: Groq – Llama 3.3 70B, Groq – Qwen 32B).
4. Bấm **"Chạy thử nghiệm"**.

Các trường hợp báo lỗi (validate): chưa chọn môn, chưa nhập câu hỏi, câu hỏi > 1000 ký tự, chưa tick model nào → form hiện thông báo, chưa gọi AI.

## Cách hoạt động phía sau

1. **Retrieval 1 lần**: hệ thống RAG (FAISS + BM25 + rerank qua Python RAG API) chạy **đúng 1 lần** cho câu hỏi, lấy ra tối đa 10 đoạn ngữ cảnh chung.
2. **Từng model trả lời**: cùng 1 prompt (chỉ dẫn + ngữ cảnh + câu hỏi) gửi tới từng model đã chọn — đảm bảo công bằng, không model nào nhận ngữ cảnh khác nhau. Mỗi model đo riêng: thời gian phản hồi (`Stopwatch`), token vào/ra/tổng (số thật từ API).
3. **Giám khảo AI chấm so sánh**: tất cả câu trả lời thành công được gửi **cùng lúc** trong 1 lần gọi tới model giám khảo (`openai/gpt-oss-120b`). Giám khảo được yêu cầu **so sánh trực tiếp và phân biệt** chất lượng (không cho điểm huề nhau), chấm thang 1–10 kèm lý do.
4. **Lưu + hiển thị**: kết quả lưu vào database (bảng `ModelComparisonRun` + `ModelComparisonResult`) và hiển thị ngay dạng thẻ.

## Đọc kết quả (trang chạy thử nghiệm)

Mỗi model 1 thẻ, đặt cạnh nhau: tên model, trạng thái (Thành công/Lỗi), thời gian phản hồi (ms), token (vào/ra/tổng), **Điểm chất lượng (AI chấm)** kèm lý do, và toàn văn câu trả lời để so sánh trực tiếp bằng mắt.

Header kết quả hiển thị: số đoạn ngữ cảnh lấy được và thời gian retrieval (tách riêng, không tính vào thời gian của từng model).

## Trang Lịch sử & Báo cáo (`/Admin/ModelComparison/History`)

- **3 biểu đồ tổng hợp** (Chart.js): Token trung bình, Độ trễ trung bình, Điểm chất lượng trung bình — theo từng model. Biểu đồ Token/Độ trễ tự co giãn trục theo dữ liệu; biểu đồ Điểm cố định thang 0–10.
- **Bảng lịch sử**: 100 lần chạy gần nhất (thời gian theo giờ Việt Nam, môn học, câu hỏi, và với mỗi model: latency, token, điểm, trạng thái).
- Số liệu trung bình chỉ tính trên các lần **thành công** (lần lỗi không kéo trung bình xuống sai lệch).

## Giới hạn hiện tại (cần biết khi báo cáo)

- **Điểm chất lượng do AI chấm** — mang tính đánh giá tương đối của 1 AI khác, không phải phép đo khách quan tuyệt đối. Đây là giới hạn chung của mọi hệ thống LLM-as-judge.
- **TopK cố định = 10** đoạn ngữ cảnh — nếu tài liệu dài/dạng bảng, thông tin cần thiết có thể không lọt vào top 10 (khiến model trả lời thiếu — không phải model kém). Không cấu hình được ở giao diện.
- **Mỗi lần bấm = 1 lần chạy** — không tự lặp nhiều lần lấy trung bình; muốn kiểm tra độ ổn định phải tự chạy lại nhiều lần.
- Đây là công cụ nội bộ cho Admin/Teacher, **không** trừ Credit, **không** thuộc luồng chat của sinh viên.

## Các chỉ số khách quan vs chủ quan

| Chỉ số | Loại | Nguồn |
|---|---|---|
| Thời gian phản hồi (latency) | Khách quan | `Stopwatch` |
| Token (chi phí) | Khách quan | Số thật từ API |
| Điểm chất lượng | Chủ quan | AI giám khảo chấm |

> Khi so sánh để chọn model, nên nhìn **đồng thời cả 3** — 1 model nhanh + điểm cao nhưng tốn gấp đôi token vẫn có thể không phải lựa chọn tốt nhất.
