import json
import httpx
import uuid
import sys

def main():
    print("--- TẠO DỮ LIỆU MẪU ĐỂ CHẠY BENCHMARK ---")
    
    # 1. Chuẩn bị 3 đoạn văn bản mẫu (Kiến thức PRN222)
    paragraphs = [
        "Trong C#, ASP.NET Core là một framework mã nguồn mở đa nền tảng dùng để xây dựng các ứng dụng web hiện đại, kết nối đám mây. Nó được phát triển bởi Microsoft.",
        "Entity Framework Core (EF Core) là một trình ánh xạ cơ sở dữ liệu đối tượng (O/RM) nhẹ, có thể mở rộng, mã nguồn mở và đa nền tảng dành cho .NET. Phiên bản mới nhất hỗ trợ LINQ.",
        "Mô hình MVC (Model-View-Controller) trong ASP.NET Core chia ứng dụng thành ba nhóm thành phần chính: Model chứa dữ liệu, View hiển thị giao diện, và Controller xử lý logic người dùng."
    ]
    
    # Gán UUID cố định cho 3 đoạn văn này để làm Ground Truth
    chunk_ids = [
        str(uuid.uuid4()),
        str(uuid.uuid4()),
        str(uuid.uuid4())
    ]
    
    print("1. Đang nạp dữ liệu vào FAISS Vector Database...")
    documents = []
    for i, text in enumerate(paragraphs):
        documents.append({
            "page_content": text,
            "metadata": {"chunk_id": chunk_ids[i]}
        })
        
    # Gửi tới API của RAG-Retrieval-Indexing-API
    # Đảm bảo container rag-retrieval-api đang chạy ở port 8000
    try:
        response = httpx.post(
            "http://localhost:8000/index",
            json={
                "documents": documents,
                "profile_id": "prn222-sample-profile",
                "rebuild_cache": True
            },
            timeout=30.0
        )
        response.raise_for_status()
        print(f"✅ Đã lưu {len(paragraphs)} đoạn văn vào FAISS thành công!")
    except Exception as e:
        print(f"❌ Lỗi khi nạp FAISS: {e}")
        print("Hãy chắc chắn bạn đã chạy 'docker compose up -d' nhé!")
        sys.exit(1)
        
    print("\n2. Đang tạo file prn222-benchmark.json...")
    
    benchmark_dataset = {
        "name": "PRN222-Sample-Benchmark",
        "version": "v1",
        "description": "Bộ benchmark mẫu tạo tự động cho Teammate chạy thử nghiệm",
        "questions": [
            {
                "sortOrder": 1,
                "question": "ASP.NET Core được phát triển bởi ai?",
                "referenceAnswer": "Microsoft",
                "category": "fact",
                "sourceReference": "Tài liệu mẫu, Đoạn 1",
                "relevantChunkIds": [chunk_ids[0]],
                "evidenceNote": "Microsoft",
                "isHoldout": False
            },
            {
                "sortOrder": 2,
                "question": "EF Core là viết tắt của từ gì?",
                "referenceAnswer": "Entity Framework Core",
                "category": "fact",
                "sourceReference": "Tài liệu mẫu, Đoạn 2",
                "relevantChunkIds": [chunk_ids[1]],
                "evidenceNote": "Entity Framework Core",
                "isHoldout": False
            },
            {
                "sortOrder": 3,
                "question": "Trong mô hình MVC, thành phần nào xử lý logic người dùng?",
                "referenceAnswer": "Controller",
                "category": "fact",
                "sourceReference": "Tài liệu mẫu, Đoạn 3",
                "relevantChunkIds": [chunk_ids[2]],
                "evidenceNote": "Controller",
                "isHoldout": True
            }
        ]
    }
    
    # Lưu ra file
    with open("prn222-benchmark.json", "w", encoding="utf-8") as f:
        json.dump(benchmark_dataset, f, ensure_ascii=False, indent=2)
        
    print("✅ Đã tạo xong file prn222-benchmark.json")
    print("\n🎉 HOÀN TẤT! Teammate của bạn bây giờ có thể chạy thực nghiệm bằng lệnh:")
    print("uv run python benchmark_runner.py --dataset-id PRN222-Sample-Benchmark --profile-id prn222-sample-profile --file prn222-benchmark.json")

if __name__ == "__main__":
    main()
