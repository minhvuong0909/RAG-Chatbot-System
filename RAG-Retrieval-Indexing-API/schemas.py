from pydantic import BaseModel, Field
from typing import Any

class DocumentModel(BaseModel):
    page_content: str
    metadata: dict[str, Any] = Field(default_factory=dict)

class IndexRequest(BaseModel):
    documents: list[DocumentModel]
    rebuild_cache: bool = False

class RetrieveRequest(BaseModel):
    query: str
    dataset_id: str | None = None
    top_k: int = 3
    semantic_weight: float = 0.7
    lexical_weight: float = 0.3
    enable_rerank: bool = True

class RetrieveResponse(BaseModel):
    query: str
    documents: list[DocumentModel]
    scores: list[float]
    trace: list[str]

class ScoreRequest(BaseModel):
    answer: str
    context: str
    question: str

class ScoreResponse(BaseModel):
    # Cosine similarity (0..1) trên embedding — dùng chuẩn RAGAS.
    faithfulness: float  # answer vs context: câu trả lời bám sát tài liệu tới đâu (chống bịa)
    relevance: float     # answer vs question: câu trả lời đúng trọng tâm câu hỏi tới đâu
