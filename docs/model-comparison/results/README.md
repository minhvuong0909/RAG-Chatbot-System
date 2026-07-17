# Kết quả benchmark

- File mới nhất khớp với dataset ID trong PostgreSQL (`XQuAD-benchmark-v1-xquad-default-*.json`) là smoke test hợp lệ:
  4 câu hold-out, profile `xquad-default`, retrieval-only, dataset XQuAD có 20
  chunk độc lập. Kết quả này chỉ xác nhận E2E retrieval/metric; không dùng để
  kết luận chất lượng generation hoặc RAGAS.
- Các file dùng `default` profile cũ hoặc dataset ID khác dataset XQuAD hiện hành, gồm
  `XQuAD-benchmark-v1-default-20260717T164536Z.*` và
  `XQuAD-benchmark-v1-default-20260717T164654Z.*` là **invalid/không dùng trong
  báo cáo**. Chúng được tạo trước khi API/profile XQuAD được rebuild hoặc khi
  runner chưa chặn retrieval rỗng.
- Các file `XQuAD-benchmark-v1-xquad-{e5-base,phobert-base,bge-m3}-20260717T17*.json`
  tạo trước khi UUID ổn định được áp dụng cũng **invalid/không dùng**: benchmark
  JSON và DB khi đó thuộc hai lần recreate dataset khác nhau, nên gold ID không
  khớp và metric 0.0 không có giá trị khoa học.

Kết quả báo cáo chính phải chạy lại trên benchmark/profile hiện hành, kèm JSON
và CSV sinh từ cùng một run.
