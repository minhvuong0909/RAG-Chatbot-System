# Giải thích ERD - RAG Chatbot System

## 1. Tổng quan hệ thống

ERD này mô tả cơ sở dữ liệu cho hệ thống **RAG Chatbot System**.  
RAG là viết tắt của **Retrieval-Augmented Generation**, tức là hệ thống chatbot không chỉ trả lời bằng kiến thức có sẵn của mô hình AI, mà còn truy xuất thông tin từ tài liệu đã được upload vào hệ thống.

Hệ thống này có hai luồng xử lý chính:

1. **Data Ingestion Flow**: xử lý tài liệu sau khi upload.
2. **Query & Answer Flow**: xử lý câu hỏi của người dùng và tạo câu trả lời dựa trên tài liệu.

Cơ sở dữ liệu được thiết kế để lưu các thông tin chính như:

- Người dùng.
- Dataset.
- Tài liệu upload.
- Các đoạn chunk được tách từ tài liệu.
- Vector embedding phục vụ similarity search.
- Phiên chat.
- Tin nhắn chat.
- Nguồn trích dẫn cho câu trả lời.

---

## 2. Danh sách các bảng trong ERD

ERD gồm các bảng chính sau:

| Bảng | Chức năng |
|---|---|
| `Users` | Lưu thông tin người dùng trong hệ thống |
| `Datasets` | Lưu nhóm tài liệu hoặc bộ dữ liệu |
| `Documents` | Lưu thông tin tài liệu được upload |
| `Chunks` | Lưu các đoạn văn bản nhỏ sau khi chia tài liệu |
| `VectorRecords` | Lưu vector embedding của từng chunk |
| `ChatSessions` | Lưu phiên hội thoại của người dùng |
| `ChatMessages` | Lưu từng tin nhắn trong phiên chat |
| `Citations` | Lưu nguồn trích dẫn được dùng trong câu trả lời |

---

## 3. Giải thích từng bảng

## 3.1. Bảng Users

Bảng `Users` dùng để lưu thông tin người dùng trong hệ thống.

```plantuml
entity "Users" as users {
  * user_id : UUID <<PK>>
  --
  full_name : VARCHAR
  email : VARCHAR
  role : VARCHAR
  created_at : TIMESTAMP
}
```

### Ý nghĩa các trường

| Trường | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|
| `user_id` | UUID | Khóa chính, định danh duy nhất cho mỗi người dùng |
| `full_name` | VARCHAR | Họ tên người dùng |
| `email` | VARCHAR | Email đăng nhập hoặc liên hệ |
| `role` | VARCHAR | Vai trò người dùng, ví dụ: admin, user |
| `created_at` | TIMESTAMP | Thời điểm tạo tài khoản |

### Vai trò trong hệ thống

Bảng `Users` giúp hệ thống biết:

- Ai là người tạo dataset.
- Ai là người upload tài liệu.
- Ai là người bắt đầu phiên chat.

---

## 3.2. Bảng Datasets

Bảng `Datasets` dùng để quản lý các nhóm tài liệu.

```plantuml
entity "Datasets" as datasets {
  * dataset_id : UUID <<PK>>
  --
  name : VARCHAR
  description : TEXT
  created_by : UUID <<FK>>
  created_at : TIMESTAMP
  updated_at : TIMESTAMP
}
```

### Ý nghĩa các trường

| Trường | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|
| `dataset_id` | UUID | Khóa chính của dataset |
| `name` | VARCHAR | Tên dataset |
| `description` | TEXT | Mô tả dataset |
| `created_by` | UUID | Người tạo dataset, liên kết với bảng `Users` |
| `created_at` | TIMESTAMP | Thời điểm tạo dataset |
| `updated_at` | TIMESTAMP | Thời điểm cập nhật dataset gần nhất |

### Vai trò trong hệ thống

Dataset giúp gom nhóm tài liệu theo từng chủ đề hoặc mục đích sử dụng.

Ví dụ:

- Dataset tài liệu nhân sự.
- Dataset tài liệu pháp lý.
- Dataset tài liệu học tập.
- Dataset tài liệu công ty.

Khi người dùng hỏi chatbot, hệ thống có thể giới hạn phạm vi tìm kiếm trong một dataset cụ thể.

---

## 3.3. Bảng Documents

Bảng `Documents` dùng để lưu thông tin của các tài liệu được upload.

```plantuml
entity "Documents" as documents {
  * document_id : UUID <<PK>>
  --
  dataset_id : UUID <<FK>>
  file_name : VARCHAR
  file_path : TEXT
  file_type : VARCHAR
  file_size : BIGINT
  status : VARCHAR
  uploaded_by : UUID <<FK>>
  uploaded_at : TIMESTAMP
  updated_at : TIMESTAMP
}
```

### Ý nghĩa các trường

| Trường | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|
| `document_id` | UUID | Khóa chính của tài liệu |
| `dataset_id` | UUID | Dataset chứa tài liệu |
| `file_name` | VARCHAR | Tên file gốc |
| `file_path` | TEXT | Đường dẫn lưu file |
| `file_type` | VARCHAR | Loại file, ví dụ: PDF, DOCX, TXT |
| `file_size` | BIGINT | Dung lượng file |
| `status` | VARCHAR | Trạng thái xử lý tài liệu |
| `uploaded_by` | UUID | Người upload tài liệu |
| `uploaded_at` | TIMESTAMP | Thời điểm upload |
| `updated_at` | TIMESTAMP | Thời điểm cập nhật gần nhất |

### Vai trò trong hệ thống

Bảng này giúp quản lý vòng đời của tài liệu, bao gồm:

- Tài liệu đã upload hay chưa.
- Tài liệu đã được xử lý hay chưa.
- Tài liệu thuộc dataset nào.
- Tài liệu do ai upload.
- File gốc được lưu ở đâu.

### Ví dụ trạng thái `status`

| Trạng thái | Ý nghĩa |
|---|---|
| `Uploaded` | Tài liệu đã được upload |
| `Processing` | Tài liệu đang được xử lý |
| `Completed` | Tài liệu đã xử lý xong |
| `Failed` | Tài liệu xử lý lỗi |
| `Reingesting` | Tài liệu đang được xử lý lại |

---

## 3.4. Bảng Chunks

Bảng `Chunks` lưu các đoạn văn bản nhỏ được tách ra từ tài liệu.

```plantuml
entity "Chunks" as chunks {
  * chunk_id : UUID <<PK>>
  --
  document_id : UUID <<FK>>
  chunk_index : INT
  content : TEXT
  page_number : INT
  section_title : VARCHAR
  metadata_json : JSONB
  created_at : TIMESTAMP
}
```

### Ý nghĩa các trường

| Trường | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|
| `chunk_id` | UUID | Khóa chính của chunk |
| `document_id` | UUID | Tài liệu chứa chunk |
| `chunk_index` | INT | Thứ tự chunk trong tài liệu |
| `content` | TEXT | Nội dung văn bản của chunk |
| `page_number` | INT | Số trang chứa chunk |
| `section_title` | VARCHAR | Tiêu đề mục hoặc phần chứa chunk |
| `metadata_json` | JSONB | Metadata bổ sung của chunk |
| `created_at` | TIMESTAMP | Thời điểm tạo chunk |

### Vai trò trong hệ thống

Tài liệu thường rất dài, không thể đưa toàn bộ vào LLM cùng lúc. Vì vậy, hệ thống cần chia tài liệu thành nhiều đoạn nhỏ gọi là chunk.

Chunk là đơn vị chính dùng để:

- Tạo embedding.
- Tìm kiếm similarity search.
- Làm ngữ cảnh cho LLM trả lời.
- Trích dẫn nguồn trong câu trả lời.

### Ví dụ metadata trong `metadata_json`

```json
{
  "file_name": "employee_policy.pdf",
  "page": 5,
  "section": "Annual Leave Policy",
  "dataset": "HR Documents"
}
```

---

## 3.5. Bảng VectorRecords

Bảng `VectorRecords` dùng để lưu vector embedding của từng chunk.

```plantuml
entity "VectorRecords" as vectors {
  * vector_id : UUID <<PK>>
  --
  chunk_id : UUID <<FK>>
  embedding : VECTOR
  embedding_model : VARCHAR
  created_at : TIMESTAMP
}
```

### Ý nghĩa các trường

| Trường | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|
| `vector_id` | UUID | Khóa chính của vector record |
| `chunk_id` | UUID | Chunk tương ứng với vector |
| `embedding` | VECTOR | Vector embedding của chunk |
| `embedding_model` | VARCHAR | Tên embedding model được sử dụng |
| `created_at` | TIMESTAMP | Thời điểm tạo embedding |

### Vai trò trong hệ thống

Bảng này là phần rất quan trọng trong RAG vì nó phục vụ bước **similarity search**.

Khi người dùng nhập câu hỏi:

1. Câu hỏi được chuyển thành vector.
2. Hệ thống so sánh vector câu hỏi với các vector trong bảng `VectorRecords`.
3. Các chunk có vector gần nhất sẽ được lấy ra.
4. Những chunk đó được đưa vào LLM để tạo câu trả lời.

Nếu dùng PostgreSQL, trường `embedding` có thể được lưu bằng extension `pgvector`.

Ví dụ:

```sql
embedding vector(1536)
```

Số `1536` là số chiều vector, phụ thuộc vào embedding model được sử dụng.

---

## 3.6. Bảng ChatSessions

Bảng `ChatSessions` lưu thông tin từng phiên chat.

```plantuml
entity "ChatSessions" as sessions {
  * session_id : UUID <<PK>>
  --
  user_id : UUID <<FK>>
  dataset_id : UUID <<FK>>
  title : VARCHAR
  started_at : TIMESTAMP
  updated_at : TIMESTAMP
}
```

### Ý nghĩa các trường

| Trường | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|
| `session_id` | UUID | Khóa chính của phiên chat |
| `user_id` | UUID | Người dùng bắt đầu phiên chat |
| `dataset_id` | UUID | Dataset được sử dụng trong phiên chat |
| `title` | VARCHAR | Tiêu đề phiên chat |
| `started_at` | TIMESTAMP | Thời điểm bắt đầu phiên chat |
| `updated_at` | TIMESTAMP | Thời điểm cập nhật phiên chat gần nhất |

### Vai trò trong hệ thống

Mỗi lần người dùng bắt đầu một cuộc trò chuyện mới, hệ thống tạo một `ChatSession`.

Bảng này giúp:

- Quản lý nhiều cuộc hội thoại.
- Liên kết hội thoại với người dùng.
- Biết phiên chat đang hỏi trên dataset nào.
- Hỗ trợ lấy lịch sử hội thoại để hiểu ngữ cảnh.

---

## 3.7. Bảng ChatMessages

Bảng `ChatMessages` lưu từng tin nhắn trong một phiên chat.

```plantuml
entity "ChatMessages" as messages {
  * message_id : UUID <<PK>>
  --
  session_id : UUID <<FK>>
  role : VARCHAR
  content : TEXT
  created_at : TIMESTAMP
}
```

### Ý nghĩa các trường

| Trường | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|
| `message_id` | UUID | Khóa chính của tin nhắn |
| `session_id` | UUID | Phiên chat chứa tin nhắn |
| `role` | VARCHAR | Vai trò người gửi |
| `content` | TEXT | Nội dung tin nhắn |
| `created_at` | TIMESTAMP | Thời điểm tạo tin nhắn |

### Vai trò trong hệ thống

Bảng này lưu cả câu hỏi của người dùng và câu trả lời của AI.

Ví dụ giá trị của `role`:

| Role | Ý nghĩa |
|---|---|
| `user` | Tin nhắn do người dùng gửi |
| `assistant` | Tin nhắn do AI trả lời |
| `system` | Tin nhắn hệ thống hoặc prompt điều khiển |

Trong Query & Answer Flow, hệ thống sẽ lấy các tin nhắn trước đó trong cùng session để hiểu ngữ cảnh hội thoại.

---

## 3.8. Bảng Citations

Bảng `Citations` lưu nguồn trích dẫn của câu trả lời.

```plantuml
entity "Citations" as citations {
  * citation_id : UUID <<PK>>
  --
  message_id : UUID <<FK>>
  chunk_id : UUID <<FK>>
  document_id : UUID <<FK>>
  page_number : INT
  quote_text : TEXT
  source_label : VARCHAR
  created_at : TIMESTAMP
}
```

### Ý nghĩa các trường

| Trường | Kiểu dữ liệu | Ý nghĩa |
|---|---|---|
| `citation_id` | UUID | Khóa chính của citation |
| `message_id` | UUID | Tin nhắn AI có sử dụng citation |
| `chunk_id` | UUID | Chunk được dùng làm nguồn |
| `document_id` | UUID | Tài liệu gốc chứa chunk |
| `page_number` | INT | Trang tài liệu chứa nguồn |
| `quote_text` | TEXT | Đoạn nội dung được trích dẫn |
| `source_label` | VARCHAR | Nhãn nguồn hiển thị cho người dùng |
| `created_at` | TIMESTAMP | Thời điểm tạo citation |

### Vai trò trong hệ thống

Citation giúp câu trả lời của AI có tính minh bạch và kiểm chứng được.

Ví dụ, sau khi chatbot trả lời, hệ thống có thể hiển thị:

```text
Nguồn: employee_policy.pdf, trang 5
```

hoặc:

```text
[1] employee_policy.pdf - Annual Leave Policy - page 5
```

---

## 4. Giải thích các mối quan hệ

## 4.1. Users - Datasets

```plantuml
users ||--o{ datasets : creates
```

Một người dùng có thể tạo nhiều dataset.  
Mỗi dataset được tạo bởi một người dùng.

Quan hệ:

```text
Users 1 - N Datasets
```

---

## 4.2. Users - Documents

```plantuml
users ||--o{ documents : uploads
```

Một người dùng có thể upload nhiều tài liệu.  
Mỗi tài liệu được upload bởi một người dùng.

Quan hệ:

```text
Users 1 - N Documents
```

---

## 4.3. Users - ChatSessions

```plantuml
users ||--o{ sessions : starts
```

Một người dùng có thể bắt đầu nhiều phiên chat.  
Mỗi phiên chat thuộc về một người dùng.

Quan hệ:

```text
Users 1 - N ChatSessions
```

---

## 4.4. Datasets - Documents

```plantuml
datasets ||--o{ documents : contains
```

Một dataset có thể chứa nhiều document.  
Mỗi document thuộc về một dataset.

Quan hệ:

```text
Datasets 1 - N Documents
```

---

## 4.5. Documents - Chunks

```plantuml
documents ||--o{ chunks : split_into
```

Một document sau khi xử lý có thể được chia thành nhiều chunk.  
Mỗi chunk thuộc về một document.

Quan hệ:

```text
Documents 1 - N Chunks
```

---

## 4.6. Chunks - VectorRecords

```plantuml
chunks ||--|| vectors : embedded_as
```

Mỗi chunk được embedding thành một vector.  
Mỗi vector tương ứng với một chunk.

Quan hệ:

```text
Chunks 1 - 1 VectorRecords
```

Ghi chú: nếu muốn hỗ trợ nhiều embedding model khác nhau cho cùng một chunk, có thể đổi quan hệ này thành:

```text
Chunks 1 - N VectorRecords
```

---

## 4.7. Datasets - ChatSessions

```plantuml
datasets ||--o{ sessions : used_in
```

Một dataset có thể được dùng trong nhiều phiên chat.  
Mỗi phiên chat thường dùng một dataset để giới hạn phạm vi truy xuất tài liệu.

Quan hệ:

```text
Datasets 1 - N ChatSessions
```

---

## 4.8. ChatSessions - ChatMessages

```plantuml
sessions ||--o{ messages : contains
```

Một phiên chat có thể chứa nhiều tin nhắn.  
Mỗi tin nhắn thuộc về một phiên chat.

Quan hệ:

```text
ChatSessions 1 - N ChatMessages
```

---

## 4.9. ChatMessages - Citations

```plantuml
messages ||--o{ citations : has
```

Một tin nhắn trả lời của AI có thể có nhiều nguồn trích dẫn.  
Mỗi citation thuộc về một tin nhắn.

Quan hệ:

```text
ChatMessages 1 - N Citations
```

---

## 4.10. Chunks - Citations

```plantuml
chunks ||--o{ citations : cited_by
```

Một chunk có thể được trích dẫn trong nhiều câu trả lời khác nhau.  
Mỗi citation tham chiếu đến một chunk.

Quan hệ:

```text
Chunks 1 - N Citations
```

---

## 4.11. Documents - Citations

```plantuml
documents ||--o{ citations : source_of
```

Một document có thể là nguồn của nhiều citation.  
Mỗi citation có thể chỉ ra tài liệu gốc chứa thông tin được trích dẫn.

Quan hệ:

```text
Documents 1 - N Citations
```

---

## 5. ERD hỗ trợ Data Ingestion Flow như thế nào?

Data Ingestion Flow gồm các bước:

1. Upload tài liệu.
2. Extract text từ tài liệu.
3. Làm sạch text.
4. Chia tài liệu thành chunks.
5. Gắn metadata cho từng chunk.
6. Embedding từng chunk thành vector.
7. Lưu vector, chunk và metadata vào database.

Mapping vào ERD:

| Bước trong workflow | Bảng liên quan |
|---|---|
| Upload tài liệu | `Documents` |
| Gắn tài liệu vào dataset | `Datasets`, `Documents` |
| Lưu thông tin người upload | `Users`, `Documents` |
| Chia tài liệu thành chunks | `Chunks` |
| Gắn metadata cho chunk | `Chunks.metadata_json` |
| Embedding chunk | `VectorRecords` |
| Lưu vector phục vụ similarity search | `VectorRecords.embedding` |

Luồng dữ liệu có thể hiểu như sau:

```text
Users
  ↓ upload
Documents
  ↓ split into
Chunks
  ↓ embedded as
VectorRecords
```

---

## 6. ERD hỗ trợ Query & Answer Flow như thế nào?

Query & Answer Flow gồm các bước:

1. Người dùng nhập câu hỏi.
2. Hệ thống lấy lịch sử hội thoại.
3. Embedding câu hỏi hiện tại thành vector.
4. Similarity search trên Vector Database.
5. Lấy các chunk liên quan nhất.
6. Gửi câu hỏi, chunk và lịch sử vào LLM.
7. LLM tạo câu trả lời trong phạm vi tài liệu.
8. Trích dẫn nguồn từ metadata của chunk.
9. Hiển thị câu trả lời và lưu vào lịch sử phiên chat.

Mapping vào ERD:

| Bước trong workflow | Bảng liên quan |
|---|---|
| Người dùng bắt đầu chat | `Users`, `ChatSessions` |
| Lưu phiên chat | `ChatSessions` |
| Lưu câu hỏi người dùng | `ChatMessages` |
| Lấy lịch sử hội thoại | `ChatMessages` |
| Similarity search | `VectorRecords`, `Chunks` |
| Lấy chunk liên quan | `Chunks` |
| Lấy nguồn tài liệu | `Documents`, `Chunks.metadata_json` |
| Lưu câu trả lời AI | `ChatMessages` |
| Lưu nguồn trích dẫn | `Citations` |

Luồng dữ liệu có thể hiểu như sau:

```text
User question
  ↓
ChatMessages
  ↓
VectorRecords similarity search
  ↓
Chunks
  ↓
LLM generates answer
  ↓
ChatMessages
  ↓
Citations
```

---

## 7. Giải thích về similarity search

Trong hệ thống RAG, similarity search là bước tìm những đoạn tài liệu có nội dung gần nhất với câu hỏi của người dùng.

Quy trình:

1. Chunk tài liệu đã được embedding và lưu trong `VectorRecords`.
2. Khi người dùng hỏi, câu hỏi cũng được embedding thành vector.
3. Hệ thống so sánh vector câu hỏi với vector của các chunk.
4. Các chunk có độ tương đồng cao nhất được lấy ra.
5. Các chunk này được đưa vào LLM làm ngữ cảnh trả lời.

Nếu dùng PostgreSQL + pgvector, có thể dùng các phép toán như:

| Toán tử | Ý nghĩa |
|---|---|
| `<->` | L2 distance |
| `<#>` | Inner product |
| `<=>` | Cosine distance |

Ví dụ truy vấn cosine distance:

```sql
SELECT c.chunk_id, c.content, v.embedding
FROM "Chunks" c
JOIN "VectorRecords" v ON c.chunk_id = v.chunk_id
ORDER BY v.embedding <=> @query_embedding
LIMIT 5;
```

Câu truy vấn trên sẽ lấy ra 5 chunk gần nhất với câu hỏi của người dùng.

---

## 8. Lý do thiết kế bảng VectorRecords riêng

Có hai cách thiết kế vector embedding:

### Cách 1: Lưu embedding trực tiếp trong bảng Chunks

Ưu điểm:

- Thiết kế đơn giản.
- Ít bảng hơn.

Nhược điểm:

- Khó quản lý nếu sau này đổi embedding model.
- Khó lưu nhiều vector khác nhau cho cùng một chunk.

### Cách 2: Tách riêng bảng VectorRecords

Ưu điểm:

- Dễ quản lý embedding.
- Có thể biết vector được tạo bằng model nào.
- Dễ re-embedding khi đổi model.
- Dễ mở rộng nếu một chunk có nhiều vector embedding.

Nhược điểm:

- Cần thêm một bảng và quan hệ.

ERD này chọn cách tách bảng `VectorRecords` riêng vì phù hợp hơn với hệ thống RAG có khả năng mở rộng.

---

## 9. Lý do cần bảng Citations

Bảng `Citations` rất quan trọng vì giúp câu trả lời của chatbot có nguồn rõ ràng.

Nếu không có citation, người dùng khó biết câu trả lời được lấy từ tài liệu nào.

Citation giúp:

- Tăng độ tin cậy của câu trả lời.
- Cho phép người dùng kiểm tra lại tài liệu gốc.
- Hạn chế việc AI trả lời lan man ngoài tài liệu.
- Phù hợp với yêu cầu “LLM tạo câu trả lời giới hạn trong phạm vi tài liệu”.

---

## 10. Gợi ý cải tiến ERD nếu mở rộng hệ thống

Nếu hệ thống cần mở rộng hơn, có thể bổ sung thêm các bảng sau:

| Bảng đề xuất | Mục đích |
|---|---|
| `DocumentVersions` | Lưu nhiều phiên bản của cùng một tài liệu |
| `IngestionJobs` | Theo dõi quá trình xử lý tài liệu |
| `EmbeddingJobs` | Theo dõi quá trình embedding |
| `Feedbacks` | Lưu đánh giá của người dùng về câu trả lời |
| `PromptTemplates` | Quản lý các mẫu prompt |
| `ReingestionLogs` | Lưu lịch sử re-ingestion |
| `DocumentPermissions` | Phân quyền truy cập tài liệu |

Đối với workflow hiện tại, ERD đang có là đủ để xây dựng bản RAG chatbot cơ bản đến trung bình.

---

## 11. Kết luận

ERD này phù hợp với hệ thống RAG Chatbot vì đã thể hiện đầy đủ các nhóm dữ liệu quan trọng:

- Quản lý người dùng.
- Quản lý dataset.
- Quản lý tài liệu.
- Quản lý chunk.
- Quản lý vector embedding.
- Quản lý lịch sử hội thoại.
- Quản lý nguồn trích dẫn.

Thiết kế này hỗ trợ tốt cả hai luồng chính của hệ thống là **Data Ingestion Flow** và **Query & Answer Flow**.  
Ngoài ra, việc sử dụng `VectorRecords` giúp hệ thống dễ tích hợp PostgreSQL + pgvector để thực hiện similarity search.
