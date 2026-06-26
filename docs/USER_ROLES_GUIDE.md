# Hướng dẫn vai trò người dùng — RAG Chatbot System

Mô tả quyền theo role. Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## Admin

- Duyệt tài khoản đăng ký mới (`IsApproved`).
- Tạo / quản lý user, gán role Teacher hoặc Student.
- Phân quyền dataset (`DatasetPermission`).
- Phân công môn/dataset cho Teacher (`TeacherSubjectAssignment`).
- Truy cập toàn bộ chức năng quản trị (`AdminController`).

---

## Teacher

- Tạo và quản lý dataset được gán.
- Upload, index, xóa tài liệu trong dataset có quyền quản lý.
- Xem preview tài liệu.
- Sử dụng chat trên dataset được phép.

Các action upload/index thường yêu cầu `[Authorize(Roles = "Teacher,Admin")]`.

---

## Student

- Đăng nhập sau khi được duyệt.
- Chat trên dataset có `DatasetPermission` đọc.
- Không upload / xóa tài liệu (trừ khi được cấp quyền cao hơn).

---

## Luồng đăng ký

1. User đăng ký qua form Register.
2. Admin duyệt trong trang quản trị.
3. User đăng nhập — có thể bị yêu cầu đổi mật khẩu (`MustChangePassword`).

---

## Admin seed (lần deploy đầu)

Cấu hình `ADMIN_EMAIL` + `ADMIN_PASSWORD` (hoặc `AdminSeed:*`) — tự tạo admin nếu chưa có user role Admin.

---

## Tài liệu liên quan

- [FAQ.md](./FAQ.md)
- [ERD_RAG_Chatbot_Explanation.md](./ERD_RAG_Chatbot_Explanation.md)
