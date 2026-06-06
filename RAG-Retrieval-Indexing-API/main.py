from contextlib import asynccontextmanager
from fastapi import FastAPI, HTTPException

from langchain_core.documents import Document

from schemas import IndexRequest, RetrieveRequest, RetrieveResponse, DocumentModel
from service import get_retriever, RerankerService, hybrid_retrieve
from config import settings

@asynccontextmanager
async def lifespan(app: FastAPI):
    # Khởi động: Tải trước mô hình lên RAM để tăng tốc lượt yêu cầu đầu tiên
    print("Warming up models (Embedding and Reranker)...")
    try:
        get_retriever() # Tải trước mô hình Embedding và chỉ mục cache
        RerankerService.get_instance() # Tải trước mô hình Reranker
        print("Models successfully warmed up and ready.")
    except Exception as e:
        print(f"Model warm-up error: {e}")
    yield

app = FastAPI(title="RAG Retrieval & Indexing API", lifespan=lifespan)

@app.get("/health")
async def health():
    return {"status": "healthy", "config": settings.dict()}

@app.post("/index")
async def index_documents(request: IndexRequest):
    try:
        # Chuyển đổi mô hình Pydantic sang LangChain Documents
        docs = [Document(page_content=d.page_content, metadata=d.metadata) for d in request.documents]
        
        # Tải lại retriever và gộp tài liệu mới để cập nhật chỉ mục
        retriever = get_retriever(force_reload=True, documents=docs, rebuild_cache=request.rebuild_cache)
        
        # Tái sử dụng embeddings đã tính sẵn từ _build_index (không tính lại lần 2)
        embeddings = retriever.get_last_embeddings(docs)
        
        return {
            "message": f"Successfully indexed {len(docs)} documents.",
            "embeddings": embeddings
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.post("/retrieve", response_model=RetrieveResponse)
async def retrieve(request: RetrieveRequest):
    try:
        results = hybrid_retrieve(
            query=request.query,
            top_k=request.top_k,
            semantic_weight=request.semantic_weight,
            lexical_weight=request.lexical_weight,
            enable_rerank=request.enable_rerank
        )
        
        # Chuyển đổi kết quả tìm kiếm sang dạng response model
        doc_models = [DocumentModel(page_content=d.page_content, metadata=d.metadata) for d in results["documents"]]
        
        return RetrieveResponse(
            query=request.query,
            documents=doc_models,
            scores=results["scores"],
            trace=results["trace"]
        )
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

@app.delete("/documents/{document_id}")
async def delete_document(document_id: str):
    try:
        retriever = get_retriever()
        success = retriever.delete_document(document_id)
        if not success:
            raise HTTPException(status_code=404, detail=f"Document with ID {document_id} not found in index.")
        return {"status": "success", "message": f"Successfully deleted document {document_id} from RAG index."}
    except HTTPException:
        raise
    except Exception as e:
        raise HTTPException(status_code=500, detail=str(e))

if __name__ == "__main__":
    import uvicorn
    uvicorn.run(app, host="0.0.0.0", port=8000)
