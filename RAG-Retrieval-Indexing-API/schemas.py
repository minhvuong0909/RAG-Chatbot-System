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
    top_k: int = 3
    semantic_weight: float = 0.7
    lexical_weight: float = 0.3
    enable_rerank: bool = True

class RetrieveResponse(BaseModel):
    query: str
    documents: list[DocumentModel]
    scores: list[float]
    trace: list[str]
