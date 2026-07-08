# Đặc Tả Yêu Cầu MVP Cho 3 Workflow Chính

## 1. Mục Đích

Tài liệu này mô tả chi tiết yêu cầu MVP cho hệ thống RAG Chatbot dùng trong bối cảnh một trường đại học. Đây là tài liệu quy ước chung để các thành viên trong nhóm bám theo khi triển khai, tránh làm lệch nghiệp vụ hoặc mở rộng scope quá mức.

Bối cảnh hệ thống:

- Admin quản lý môn học, người dùng và phân công giảng viên.
- Giảng viên phụ trách chính quản lý tài liệu tri thức cho các môn học được phân công.
- Sinh viên đặt câu hỏi trong từng môn học và nhận câu trả lời dựa trên tài liệu chính thức.
- Nhà trường cần kiểm soát mức sử dụng AI, xem thống kê, và có bằng chứng thực nghiệm để chọn mô hình phù hợp.

Thời gian MVP dự kiến: 1 đến 1.5 tuần với nhóm 5 người. Vì vậy, yêu cầu trong tài liệu này tập trung vào phần có giá trị demo cao, nghiệp vụ rõ, và khả thi trong thời gian ngắn.

## 2. Phạm Vi MVP

### 2.1 Ba Workflow Chính

MVP gồm 3 workflow chính:

1. **Quản lý tri thức môn học**
   - Admin tạo môn học và phân công một giảng viên phụ trách chính.
   - Giảng viên upload, xem trước và quản lý tài liệu trong môn được phân công.
   - Hệ thống extract text, chia chunk, tạo embedding và index vào RAG.

2. **Hỏi đáp có dẫn chứng kiểm chứng**
   - Sinh viên hỏi trong workspace của từng môn học.
   - Chatbot chỉ trả lời dựa trên tài liệu của môn học đó.
   - Citation hiển thị ngay bên dưới câu trả lời.
   - Khi click citation, hệ thống mở modal preview tài liệu và highlight đúng đoạn dẫn chứng.

3. **Quản trị mức sử dụng AI và đánh giá mô hình**
   - Hệ thống ghi nhận token/lượt hỏi.
   - Sinh viên bị tạm dừng gửi câu hỏi mới khi vượt hạn mức AI trong ngày.
   - Admin/Giảng viên xem dashboard thống kê usage.
   - Admin/Giảng viên chạy thử nghiệm so sánh mô hình AI và dùng kết quả cho báo cáo/benchmark.

### 2.2 Ngoài Phạm Vi MVP

Không làm các phần sau trong MVP, trừ khi toàn bộ yêu cầu chính đã ổn định:

- Thanh toán, premium package, doanh thu.
- Audit log đầy đủ cho mọi thao tác.
- Versioning tài liệu.
- Quy trình duyệt tài liệu phức tạp.
- Citation theo từng claim/câu nhỏ.
- Chunk config riêng theo từng môn học.
- Quota phức tạp theo khoa/kỳ học/gói sử dụng.
- Tích hợp RAGAS đầy đủ vào UI nếu script/report riêng đã đủ.

## 3. Vai Trò Và Phân Quyền

### 3.1 Vai Trò

| Vai trò | Mô tả |
|---|---|
| Admin | Quản trị viên hệ thống/trường. Quản lý user, môn học, phân công giảng viên, global settings, thống kê và model evaluation. |
| Teacher | Giảng viên phụ trách chính của một hoặc nhiều môn học. Quản lý tài liệu, xem thống kê và chạy evaluation cho các môn được phân công. |
| Student | Sinh viên. Xem các môn được phép truy cập và hỏi chatbot trong hạn mức AI mỗi ngày. |

### 3.2 Quyền Của Admin

- Tạo, sửa, xóa hoặc unapprove môn học.
- Tạo hoặc duyệt tài khoản giảng viên/sinh viên.
- Phân công hoặc gỡ phân công giảng viên khỏi môn học.
- Upload/xóa tài liệu ở mọi môn học.
- Xem toàn bộ thống kê hệ thống.
- Chạy model evaluation cho mọi môn học.
- Cấu hình chunk size, chunk overlap và hạn mức AI hằng ngày cho sinh viên.

### 3.3 Quyền Của Teacher

- Xem các môn học được Admin phân công.
- Upload, preview và xóa tài liệu chỉ trong môn được phân công.
- Xem thống kê chỉ trong môn được phân công.
- Chạy model evaluation chỉ trong môn được phân công.
- Không được phân công giảng viên khác nếu không có quyền Admin.
- Không được sửa global quota/global settings nếu không có quyền Admin.

### 3.4 Quyền Của Student

- Xem các môn học được phép truy cập.
- Tạo chat session trong môn được phép truy cập.
- Gửi câu hỏi nếu chưa vượt hạn mức AI trong ngày.
- Xem citation và dẫn chứng tài liệu của môn được phép truy cập.
- Không được upload/xóa tài liệu.
- Không được truy cập môn học không có quyền.
- Không được reset quota bằng cách tạo chat session mới.

### 3.5 Constraint Bảo Mật

- Mọi action phía server phải kiểm tra lại role và quyền truy cập môn học. Không tin vào hidden input hoặc state từ frontend.
- Request xem citation/preview tài liệu phải kiểm tra user hiện tại có quyền truy cập môn học chứa citation đó.
- Teacher phải được kiểm tra qua bảng phân công môn học.
- Student quota phải được kiểm tra trước khi gọi LLM.
- Nếu môn học bị unapprove/xóa, user không được tạo chat session mới trong môn đó.

## 4. Workflow 1: Quản Lý Tri Thức Môn Học

### 4.1 Mục Tiêu Nghiệp Vụ

Nhà trường cần duy trì một kho tri thức đáng tin cậy theo từng môn học. Giảng viên chỉ chịu trách nhiệm tài liệu trong các môn được phân công. Sinh viên hỏi đáp dựa trên tài liệu chính thức của môn học, không bị trộn dữ liệu giữa các môn.

### 4.2 Actor

- Admin
- Teacher
- Hệ thống

### 4.3 Luồng Chính

1. Admin tạo môn học/dataset.
2. Admin phân công một giảng viên phụ trách chính vào môn học.
3. Teacher mở workspace của môn được phân công.
4. Teacher upload tài liệu.
5. Hệ thống validate file.
6. Hệ thống lưu file.
7. Hệ thống extract text từ file.
8. Hệ thống chia text thành chunk theo chunk settings hiện tại.
9. Hệ thống tạo embedding và index chunk vào RAG API.
10. Hệ thống lưu chunk metadata vào database.
11. Teacher preview các chunk đã tạo.
12. Teacher có thể xóa tài liệu sai hoặc lỗi thời.
13. Sinh viên có thể hỏi đáp sau khi môn học có ít nhất một tài liệu `Completed`.

### 4.4 Yêu Cầu Chức Năng

#### Quản Lý Môn Học

- Admin có thể tạo môn học với:
  - tên môn học,
  - mô tả,
  - trạng thái public/approved nếu hệ thống hiện có.
- Admin có thể cập nhật tên/mô tả/trạng thái môn học.
- Admin không hard delete môn học trong MVP. Nút xóa trên UI phải hoạt động như archive/unapprove để giữ lại documents, chunks và trace data.
- Danh sách môn học nên hiển thị số lượng tài liệu nếu truy vấn đơn giản.

#### Phân Công Giảng Viên

- Admin có thể assign một Teacher phụ trách chính vào môn học.
- Admin có thể unassign Teacher phụ trách chính khỏi môn học.
- Trong MVP, mỗi môn học chỉ có một Teacher phụ trách chính. Không làm nhiều giảng viên cùng quản lý một môn.
- Teacher chỉ quản lý tài liệu trong môn được assign.
- Nếu Teacher bị unassign khi đang mở trang môn học, các action tiếp theo phải bị từ chối bởi server.

#### Upload Tài Liệu

- Định dạng hỗ trợ trong MVP:
  - PDF
  - DOCX
  - TXT
- Không hỗ trợ MD trong MVP để giữ luồng upload ổn định và dễ kiểm thử.
- Dung lượng tối đa theo giới hạn hiện tại của app, đang là 50MB.
- Upload phải có trạng thái xử lý:
  - `Uploaded`
  - `Processing`
  - `Completed`
  - `Failed`
- Nếu extract không có text, document phải chuyển `Failed` và hiển thị lỗi rõ ràng.
- Teacher không được upload vào môn không được assign bằng cách sửa `datasetId` trong request.

#### Xử Lý Tài Liệu Trùng

MVP nên xử lý duplicate đơn giản nhưng chặt.

- Hệ thống nên tính `FileHash` hoặc `ContentHash` trước khi tạo document hoàn chỉnh.
- Nếu cùng một môn học đã có hash tương tự:
  - không index file mới,
  - không tạo duplicate chunks,
  - hiển thị thông báo tiếng Anh: "This document already exists in this subject."
- Cùng một file ở môn học khác có thể được cho phép vì knowledge base được tách theo môn.
- Không làm versioning trong MVP.
- Không làm replace/update document trong MVP trừ khi còn thời gian.

#### Cấu Hình Chunk

- Admin có thể chỉnh global chunk size và chunk overlap nếu feature hiện tại đã có.
- Chunk settings mới chỉ áp dụng cho tài liệu upload sau đó, trừ khi có chức năng re-index riêng.
- UI phải nói rõ tài liệu cũ không tự thay đổi khi đổi chunk setting.
- Constraint đề xuất:
  - chunk size: 300-700 ký tự,
  - chunk overlap: 100 đến chunk size / 2.

#### Preview Chunk

- Teacher và Admin có thể preview chunk của một tài liệu.
- Chunk preview phải hiển thị:
  - chunk index,
  - page number,
  - nội dung chunk,
  - tên tài liệu.
- Student không cần trang preview chunk tổng quát. Student chỉ xem chunk thông qua citation.

### 4.5 Business Rules

- Môn học chưa có tài liệu `Completed` vẫn có thể hiển thị, nhưng chat phải báo rõ chưa có tài liệu đã index.
- Chỉ tài liệu `Completed` mới nên được dùng làm nguồn trả lời.
- Tài liệu `Failed` vẫn nên hiển thị cho Teacher/Admin cùng trạng thái lỗi.
- Xóa tài liệu trong MVP là soft delete: tài liệu bị ẩn khỏi nghiệp vụ/chat nhưng vẫn giữ record DB để truy vết.
- MVP không cần UI riêng để xem danh sách tài liệu đã soft delete. Giữ DB trace là đủ.
- Khi soft delete, hệ thống vẫn nên gọi RAG API delete để gỡ khỏi index.
- Nếu xóa khỏi RAG API thất bại, hệ thống phải log warning, thông báo cho user bằng tiếng Anh, và rebuild index thủ công trước demo nếu tài liệu vẫn còn searchable.

### 4.6 Dữ Liệu Cần Có

Đã có hoặc dự kiến có:

- `Dataset`
- `TeacherSubjectAssignment`
- `Document`
- `Chunk`
- `VectorRecord`

Nên thêm cho MVP:

- `Document.FileHash` hoặc `Document.ContentHash`
- `Document.ProcessError` nếu muốn lưu lỗi xử lý
- `Document.IsDeleted`, `Document.DeletedAt`, `Document.DeletedBy` để soft delete

Nếu migration quá rủi ro, có thể fallback duplicate theo file name + file size, nhưng hash vẫn là phương án đúng hơn.

### 4.7 Acceptance Criteria

- Admin tạo được môn học và assign Teacher.
- Teacher mở được môn được assign.
- Teacher không upload được vào môn không được assign.
- Teacher upload được PDF/DOCX/TXT.
- Document chuyển `Completed` và xem được chunks.
- Upload lại cùng tài liệu trong cùng môn bị chặn hoặc xử lý rõ ràng.
- Student chỉ chat khi môn có tài liệu đã index.

## 5. Workflow 2: Hỏi Đáp Có Dẫn Chứng Kiểm Chứng

### 5.1 Mục Tiêu Nghiệp Vụ

Sinh viên phải nhận được câu trả lời có căn cứ từ tài liệu chính thức của môn học. Mỗi câu trả lời cần có nguồn dẫn chứng để sinh viên và giảng viên kiểm chứng.

Đây là workflow demo quan trọng nhất vì nó chứng minh hệ thống không chỉ là chatbot, mà là RAG học thuật có bằng chứng.

### 5.2 Actor

- Student
- Teacher
- Admin
- RAG API
- LLM Provider

### 5.3 Luồng Chính

1. Student mở một môn học.
2. Student tạo hoặc chọn chat session.
3. Student gửi câu hỏi.
4. Hệ thống kiểm tra quyền truy cập môn học.
5. Hệ thống kiểm tra hạn mức AI trong ngày.
6. Hệ thống retrieve các chunk liên quan, chỉ trong môn học đang chọn.
7. Hệ thống gửi context vào LLM với prompt bắt buộc grounding.
8. LLM trả lời.
9. Hệ thống lưu user message, assistant message và citations.
10. UI hiển thị câu trả lời.
11. UI hiển thị citations ngay bên dưới câu trả lời.
12. Student click một citation.
13. UI mở modal preview tài liệu.
14. Modal hiển thị hoặc scroll tới chunk/page tương ứng.
15. Đoạn dẫn chứng được highlight rõ ràng.

### 5.4 Yêu Cầu Chức Năng

#### Chat Session

- Student có thể tạo nhiều chat session trong một môn.
- Chat history vẫn hiển thị sau khi reload trang.
- Mỗi session gắn với đúng một subject/dataset.
- Session của môn này không được retrieve tài liệu từ môn khác.

#### Retrieval

- Retrieval bắt buộc filter theo `DatasetId`.
- Cấu hình MVP đề xuất:
  - gọi RAG API lấy top 10,
  - filter theo dataset,
  - lấy top 3 làm context cho LLM.
- Nếu sau khi filter không còn chunk phù hợp, bot phải trả lời rằng không tìm thấy thông tin phù hợp trong tài liệu môn học.

#### Prompt Grounding

Prompt phải ép các rule:

- Trả lời bằng tiếng Việt, trừ khi user yêu cầu ngôn ngữ khác.
- Chỉ dùng thông tin trong context được cung cấp.
- Không bịa thêm thông tin ngoài tài liệu.
- Nếu không có thông tin trong context, nói rõ không tìm thấy trong tài liệu đã upload.
- Ưu tiên văn phong học thuật, ngắn gọn, dễ hiểu.
- Không tạo nguồn dẫn chứng giả.

Câu trả lời fallback tối thiểu:

> Tôi không tìm thấy thông tin này trong tài liệu đã upload của môn học.

#### Hiển Thị Citation

- Citation phải xuất hiện ngay bên dưới câu trả lời của Assistant.
- Mỗi citation card/chip phải có:
  - tên tài liệu,
  - số trang,
  - chunk index nếu có,
  - đoạn quote ngắn,
  - nút/hành động "Xem dẫn chứng".
- User không nên phải mở side panel riêng mới biết câu trả lời có nguồn.
- Side panel hiện tại có thể giữ, nhưng inline citation là requirement MVP.

#### Modal Xem Dẫn Chứng

Khi click citation, modal phải hiển thị:

- tên tài liệu,
- số trang,
- toàn bộ chunk hoặc phần preview tài liệu liên quan,
- đoạn evidence được highlight,
- nút đóng modal.

Các cách triển khai MVP hợp lệ:

1. Nếu render PDF page khó, modal có thể hiển thị stored chunk text và highlight quote.
2. Nếu trang document preview hiện có dùng lại được, có thể mở preview trong modal/panel rồi scroll tới chunk.
3. Điều bắt buộc là đoạn evidence phải được highlight và người xem hiểu rõ nó liên kết với câu trả lời.

Modal không được hiển thị tài liệu thuộc môn mà user không có quyền.

#### Highlight Evidence

- Nếu tìm được `QuoteText` chính xác trong preview text, highlight đúng quote.
- Nếu exact match fail do whitespace/PDF extraction khác biệt, highlight toàn bộ cited chunk.
- Highlight phải dễ đọc trong theme sáng/tối.
- Nếu chunk/document không còn tồn tại, modal hiển thị fallback message rõ ràng.

#### Trường Hợp Không Có Evidence

- Nếu có câu trả lời nhưng không có citation, UI phải hiển thị warning:
  - "Câu trả lời này chưa có nguồn dẫn chứng."
- MVP nên hạn chế case này vì citation được tạo từ retrieved context.
- Không được tạo citation giả.

### 5.5 Business Rules

- Mỗi assistant answer dùng context nên có citation records.
- Citation gắn với assistant message, không gắn với user message.
- Citation phải trỏ tới `ChunkId` và `DocumentId` hợp lệ.
- Nếu tài liệu nguồn bị xóa sau này:
  - chat history cũ vẫn hiển thị,
  - click citation phải báo "Tài liệu nguồn không còn tồn tại" hoặc hiển thị stored `QuoteText`.
- Student không được xem citation/tài liệu của môn không có quyền.
- Teacher/Admin được xem citation của các môn họ quản lý.

### 5.6 Dữ Liệu Cần Có

Đã có:

- `ChatSession`
- `ChatMessage`
- `Citation`
- `Chunk`
- `Document`

Có thể thêm nếu kịp:

- `Citation.ChunkIndex` nếu không join được từ `Chunk`
- `Citation.RetrievalScore`
- `Citation.ModelName`

Không bắt buộc thêm các field optional nếu làm chậm MVP.

### 5.7 Acceptance Criteria

- Student hỏi và nhận câu trả lời grounded.
- Citation hiển thị ngay dưới câu trả lời.
- Click citation mở modal dẫn chứng.
- Đoạn evidence được highlight.
- Câu hỏi ngoài tài liệu không bị hallucinate, bot trả lời không tìm thấy trong tài liệu.
- User không có quyền môn học không xem được citation.

## 6. Workflow 3: Quản Trị Mức Sử Dụng AI Và Đánh Giá Mô Hình

Workflow này có hai phần liên quan:

1. Hạn mức AI hằng ngày và dashboard usage.
2. So sánh mô hình AI và benchmark cho RAG học thuật.

Hai phần này được gom chung vì đều phục vụ quản trị hệ thống và chứng minh chất lượng.

## 6A. Hạn Mức Token Và Thống Kê Usage

### 6A.1 Mục Tiêu Nghiệp Vụ

Nhà trường cần kiểm soát chi phí AI, tránh lạm dụng, và hiểu sinh viên đang sử dụng chatbot như thế nào theo từng môn học.

Không gọi đây là "khóa tài khoản sinh viên". Nghiệp vụ đúng là:

> Khi sinh viên dùng hết hạn mức AI trong ngày, hệ thống tạm dừng việc gửi câu hỏi mới. Sinh viên vẫn đăng nhập, xem lịch sử, xem môn học và xem citation bình thường.

### 6A.2 Actor

- Admin
- Teacher
- Student
- Hệ thống

### 6A.3 Luồng Chính

1. Admin cấu hình hạn mức AI hằng ngày cho role Student.
2. Student mở chat trong một môn.
3. Student gửi câu hỏi.
4. Hệ thống kiểm tra lượng token đã dùng trong ngày trước khi gọi LLM.
5. Nếu đã vượt quota, hệ thống chặn câu hỏi mới.
6. Nếu còn quota, hệ thống xử lý RAG chat bình thường.
7. Sau khi có câu trả lời, hệ thống ghi usage.
8. Admin xem dashboard usage toàn hệ thống.
9. Teacher xem dashboard usage của các môn được assign.

### 6A.4 Yêu Cầu Chức Năng

#### Cấu Hình Quota

- Admin có thể cấu hình global daily token limit cho Student.
- Gợi ý default MVP:
  - 3000 đến 10000 estimated tokens / sinh viên / ngày.
- Hạn mức áp dụng cho mỗi sinh viên trên toàn bộ môn học, trừ khi sau này có per-subject quota.
- Admin và Teacher không bị giới hạn trong MVP, hoặc có limit cao hơn nhiều.
- Nếu limit để trống hoặc bằng 0, phải định nghĩa rõ:
  - khuyến nghị: 0 nghĩa là tắt quota/no limit khi Admin chủ động cấu hình.
  - tránh lỗi đặt 0 làm block toàn bộ sinh viên.

#### Chặn Khi Vượt Quota

- Quota được kiểm tra trước khi gọi LLM.
- Nếu used tokens hôm nay >= limit:
  - không gọi LLM,
  - không tạo assistant answer,
  - hiển thị thông báo rõ ràng.
- User message có thể:
  - không lưu, hoặc
  - lưu với trạng thái `Blocked`.
- Khuyến nghị MVP: không lưu blocked question như message thường để tránh gây nhiễu chat history.
- Quota reset hằng ngày theo timezone hệ thống.
- Với dự án này, báo cáo và UI nên dùng timezone Việt Nam.

#### Đo Token

Ưu tiên:

- Dùng token usage từ provider nếu provider trả về.

Fallback MVP:

- Ước lượng token theo số ký tự.
- Công thức đề xuất:
  - `estimatedTokens = ceil(characterCount / 4)`.

Khi ghi usage, nên tính:

- token câu hỏi,
- token context gửi vào LLM,
- token câu trả lời,
- tổng token.

#### Thông Báo Khi Hết Quota

Thông báo tiếng Việt:

> Bạn đã dùng hết hạn mức AI hôm nay. Bạn vẫn có thể xem lịch sử chat và nguồn dẫn chứng. Vui lòng quay lại vào ngày mai hoặc liên hệ giảng viên/quản trị viên.

Không dùng các câu:

- "Tài khoản của bạn bị khóa."
- "Bạn bị cấm sử dụng."

#### Dashboard Usage

Dashboard Admin phải có:

- tổng số câu hỏi,
- tổng estimated tokens,
- số sinh viên active,
- top môn học theo số câu hỏi,
- top sinh viên theo token usage,
- biểu đồ token theo ngày,
- biểu đồ câu hỏi theo ngày.

Dashboard Teacher chỉ hiển thị môn được assign:

- số câu hỏi trong môn được assign,
- estimated tokens trong môn được assign,
- top tài liệu được citation nếu dễ làm,
- no-answer count/rate nếu dễ làm.

Yêu cầu MVP tối thiểu:

- ít nhất một biểu đồ line/bar cho usage theo ngày,
- ít nhất một bảng top user hoặc top subject.

### 6A.5 Business Rules

- Quota của Student tính theo user identity, không theo browser session.
- Tạo chat session mới không reset quota.
- Logout/login không reset quota.
- Request lỗi:
  - nếu chưa gọi LLM, không tính token,
  - nếu đã gọi LLM và lỗi sau đó, ghi input token estimate và đánh dấu failed nếu có field.
- Xem citation/history không bao giờ tốn quota.
- Retrieval-only không cần tính quota trong MVP, trừ khi team thống nhất tính.

### 6A.6 Dữ Liệu Cần Có

Nên thêm entity:

`TokenUsageLog`

Fields:

- `UsageId`
- `UserId`
- `DatasetId`
- `SessionId`
- `MessageId` nullable
- `ModelName`
- `InputTokens`
- `OutputTokens`
- `TotalTokens`
- `WasSuccessful`
- `CreatedAt`

Nên thêm setting:

- `SystemSetting.StudentDailyTokenLimit`

Optional:

- `QuotaResetTimezone`
- `BlockedReason`

### 6A.7 Acceptance Criteria

- Admin cấu hình được daily token limit cho Student.
- Student chat được khi còn quota.
- Token usage được ghi sau câu trả lời thành công.
- Student không gửi được câu hỏi mới sau khi vượt quota.
- Student vẫn xem được lịch sử chat và citation sau khi hết quota.
- Admin xem được thống kê token/question toàn hệ thống.
- Teacher chỉ xem được thống kê của môn được assign.

## 6B. So Sánh Mô Hình Và Benchmark

### 6B.1 Mục Tiêu Nghiệp Vụ

Nhà trường và nhóm dự án cần có bằng chứng để chọn mô hình AI phù hợp. Việc chọn model phải dựa trên chất lượng câu trả lời, tốc độ, token/cost và độ bám tài liệu của RAG, không chỉ dựa vào cảm tính.

Đây cũng là deliverable nghiên cứu cho báo cáo cuối kỳ.

### 6B.2 Actor

- Admin
- Teacher
- Hệ thống
- LLM Providers
- RAG API

### 6B.3 Luồng Chính

1. Admin/Teacher mở trang Model Evaluation.
2. User chọn môn học.
3. User chọn hoặc nhập bộ câu hỏi benchmark.
4. User chọn các model cần so sánh.
5. Hệ thống chạy từng câu hỏi qua từng model với cùng retrieval config.
6. Hệ thống lưu answer, citations, latency và token estimate.
7. Hệ thống hiển thị bảng so sánh.
8. Script/report riêng có thể tính thêm RAGAS metrics.
9. Nhóm export/copy kết quả vào báo cáo và slide.

### 6B.4 Yêu Cầu Chức Năng

#### Chọn Model

MVP nên so sánh 2 model.

Lựa chọn ổn định được khuyến nghị:

- Gemini vs Groq

Optional:

- Ollama nếu setup local ổn định.

Lưu ý:

- Viết đúng là "Ollama", không phải "Olama".
- Ollama có thể cần tải model local, RAM/VRAM và setup runtime/Docker thêm. Nếu gây rủi ro demo, để Ollama ở mức optional.

#### Input Cho Evaluation

Admin/Teacher có thể cung cấp:

- môn học/dataset,
- danh sách câu hỏi,
- danh sách model.

Các cách nhập câu hỏi MVP:

1. Textarea, mỗi dòng một câu hỏi.
2. Upload CSV.
3. Hardcode demo question set.

Khuyến nghị MVP:

- Textarea với 5-20 câu hỏi.

#### Rule Khi Chạy Evaluation

- Tất cả model phải dùng cùng retrieval settings.
- Tất cả model dùng cùng tài liệu môn học.
- Prompt template giống nhau, trừ phần gọi API riêng của từng provider.
- Nếu một model lỗi, đánh dấu result đó là `Failed` và tiếp tục các model/câu hỏi khác.
- Student không được chạy model evaluation.
- Teacher chỉ evaluate môn được assign.

#### Hiển Thị Kết Quả

Bảng kết quả cần có:

- câu hỏi,
- tên model,
- câu trả lời,
- số citation,
- latency ms,
- input token estimate,
- output token estimate,
- total token estimate,
- status,
- error message nếu lỗi.

Optional nếu còn thời gian:

- so sánh answer side-by-side,
- badge model tốt nhất,
- export CSV.

#### RAGAS Benchmark

MVP không bắt buộc tích hợp RAGAS vào UI.

Deliverable MVP hợp lệ:

- script/notebook/report dùng evaluation results,
- bộ benchmark 20-30 câu hỏi,
- bảng kết quả gồm:
  - faithfulness,
  - answer relevancy,
  - context precision,
  - context recall,
  - latency,
  - token estimate.

Nếu RAGAS mất thời gian, report vẫn có thể dùng latency/token/citation count và bảng đánh giá thủ công nhỏ về faithfulness.

### 6B.5 Business Rules

- Evaluation chỉ dành cho Admin/Teacher, không dành cho Student.
- Evaluation không tính vào quota của Student.
- Mỗi evaluation run phải lưu người tạo và thời gian tạo.
- Result cần đủ thông tin để đưa vào báo cáo:
  - model name,
  - subject,
  - câu hỏi,
  - prompt version nếu có.
- Một model lỗi không được làm hỏng toàn bộ evaluation run.

### 6B.6 Dữ Liệu Cần Có

Nên thêm entity:

`ModelEvaluationRun`

Fields:

- `RunId`
- `DatasetId`
- `CreatedBy`
- `CreatedAt`
- `Status`
- `QuestionCount`
- `ModelNames`

Nên thêm entity:

`ModelEvaluationResult`

Fields:

- `ResultId`
- `RunId`
- `Question`
- `ModelName`
- `Answer`
- `CitationCount`
- `LatencyMs`
- `InputTokens`
- `OutputTokens`
- `TotalTokens`
- `Status`
- `ErrorMessage`
- `CreatedAt`

Optional RAGAS fields:

- `Faithfulness`
- `AnswerRelevancy`
- `ContextPrecision`
- `ContextRecall`

### 6B.7 Acceptance Criteria

- Admin chạy được model evaluation cho một môn học.
- Teacher chỉ chạy được model evaluation cho môn được assign.
- Hệ thống so sánh được ít nhất 2 model.
- Bảng kết quả hiển thị latency/token/citation.
- Một model lỗi không làm hỏng toàn bộ run.
- Nhóm có thể export hoặc copy kết quả vào báo cáo cuối kỳ.

## 7. Yêu Cầu Chung Cho Toàn Hệ Thống

### 7.1 UI

- UI nên có label tiếng Việt ở các màn hình demo chính.
- Error message phải dễ hiểu với người không rành kỹ thuật.
- Cần loading state cho:
  - upload/index tài liệu,
  - sinh câu trả lời,
  - chạy model evaluation.
- Không hiển thị raw stack trace cho user.
- Trang Admin/Teacher nên rõ ràng, dễ scan, ưu tiên nghiệp vụ hơn trang trí.

### 7.2 Realtime

Nếu SignalR hiện tại ổn định, giữ:

- tiến trình xử lý document,
- streaming câu trả lời,
- cập nhật UI khi document/session thay đổi.

Không thêm realtime mới nếu làm tăng rủi ro MVP.

### 7.3 Data Integrity

- Không tạo chunk nếu không có document hợp lệ.
- Không tạo citation nếu không có assistant message hợp lệ.
- Không cho citation cross-subject.
- Token usage log phải reference user và subject.
- Evaluation result phải reference evaluation run.

### 7.4 Error Handling

Document upload:

- unsupported type -> validation error rõ ràng,
- empty file -> validation error,
- no extractable text -> status `Failed`,
- RAG API error -> status `Failed` kèm message.

Chat:

- không có quyền môn học -> forbidden,
- không có quyền session -> forbidden,
- vượt quota -> thông báo hết hạn mức,
- không có context -> no-answer response,
- LLM lỗi -> thông báo lỗi thân thiện.

Model evaluation:

- model unavailable -> result `Failed`,
- question list rỗng/không hợp lệ -> validation error,
- môn chưa có indexed documents -> không cho chạy hoặc cảnh báo rõ.

### 7.5 Performance

Target MVP:

- Chat answer thường nên hoàn tất trong 30-60 giây.
- Upload tài liệu có thể lâu hơn, nhưng phải có progress.
- Model evaluation có thể chậm, nhưng phải có status/progress.
- Không chạy benchmark quá lớn trong demo live.

### 7.6 Logging

Server-side cần log:

- document processing failure,
- RAG API failure,
- LLM provider failure,
- quota block event,
- model evaluation failure.

Không log API keys hoặc toàn bộ environment variables.

## 8. Demo Script Bắt Buộc

Demo cuối cùng nên chứng minh theo thứ tự:

1. Admin tạo/mở môn học và assign Teacher.
2. Teacher upload tài liệu môn học và preview chunks.
3. Student hỏi một câu trong môn học.
4. Câu trả lời hiển thị citation ngay bên dưới.
5. Student click citation, modal mở và highlight evidence.
6. Student đạt quota hoặc tài khoản demo được set gần quota; câu hỏi tiếp theo bị chặn.
7. Admin/Teacher mở dashboard usage.
8. Admin/Teacher chạy model evaluation với 2 model và hiển thị bảng so sánh.

## 9. Chia Việc Gợi Ý Cho Nhóm 5 Người

### Người A: Quản Lý Môn Học Và Phân Quyền

- Polish subject management.
- Validate teacher assignment.
- Server-side permission checks.
- Duplicate document handling nếu liên quan Document flow.

### Người B: Document Pipeline Và Citation Data

- Upload/index stability.
- Chunk preview.
- Cải thiện citation metadata.
- Endpoint/backend data cho evidence modal nếu cần.

### Người C: Chat Sinh Viên Và Evidence UI

- Inline citation display.
- Modal preview.
- Highlight evidence.
- No-answer prompt behavior.

### Người D: Quota Và Analytics

- Token usage log.
- Daily quota check.
- Admin/Teacher dashboard.
- Charts/tables.

### Người E: Model Evaluation Và Báo Cáo

- Model evaluation UI/service.
- Lưu/export benchmark result.
- RAGAS hoặc benchmark report thủ công.
- Demo data/slides.

## 10. Ưu Tiên MVP

### Must Have

- Teacher assignment permission hoạt động.
- Upload/chunk/index tài liệu hoạt động.
- Student chat theo subject hoạt động.
- Citation hiển thị ngay dưới answer.
- Click citation mở modal/highlight evidence.
- Student hết quota chỉ bị chặn gửi câu hỏi mới.
- Dashboard usage cơ bản.
- Evaluation 2 model hoặc report có bảng so sánh.

### Should Have

- Duplicate document hash.
- Thống kê top cited documents.
- Thống kê no-answer rate.
- Export CSV cho model evaluation.
- RAGAS script/report.

### Could Have

- Ollama integration.
- Feedback thumbs up/down.
- Hiển thị retrieval score trong citation.
- Trang lịch sử evaluation run.

### Won't Have Trong MVP

- Payment/premium.
- Revenue dashboard.
- Full audit log.
- Document versioning.
- Claim-level citation.
- Per-subject quota.
- Per-subject chunk config.

## 11. Definition Of Done

Một workflow chỉ được xem là xong khi:

- Chạy được qua UI, không chỉ có service code.
- Permission rules được enforce phía server.
- Đã test thủ công happy path và ít nhất một failure path.
- Có demo data.
- Thành viên phụ trách giải thích được business rule phía sau.
- Không làm hỏng flow upload/chat hiện có.

## 12. Định Vị Khi Trình Bày

Nên trình bày hệ thống là:

> Trợ lý học tập RAG cho trường đại học, cho phép giảng viên quản lý tri thức chính thức của môn học, sinh viên hỏi đáp có dẫn chứng kiểm chứng, và nhà trường kiểm soát mức sử dụng AI cũng như đánh giá chất lượng mô hình.

Không nên trình bày hệ thống chỉ là:

- một chatbot,
- một tool upload file,
- một token limiter,
- một model playground.

Narrative mạnh nhất:

- **Trust**: câu trả lời có dẫn chứng.
- **Control**: quota và analytics.
- **Research quality**: model evaluation và RAG benchmark.
