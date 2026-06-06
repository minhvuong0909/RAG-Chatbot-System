import os
from pydantic_settings import BaseSettings

class Settings(BaseSettings):
    HF_TOKEN: str = os.getenv("HF_TOKEN", "")
    
    # Model Configs
    EMBEDDING_MODEL: str = "sentence-transformers/all-MiniLM-L6-v2"
    CROSS_ENCODER_MODEL: str = "cross-encoder/ms-marco-MiniLM-L-6-v2"
    
    # Paths
    CACHE_DIR: str = "cache"
    FAISS_INDEX_PATH: str = os.path.join(CACHE_DIR, "faiss_index")
    BM25_PATH: str = os.path.join(CACHE_DIR, "bm25.pkl")
    
    # RAG Defaults
    SEMANTIC_WEIGHT: float = 0.7
    LEXICAL_WEIGHT: float = 0.3
    TOP_K: int = 3
    
    class Config:
        env_file = ".env"

settings = Settings()

# Validation
if not settings.HF_TOKEN:
    print("WARNING: HF_TOKEN is not set.")
else:
    os.environ["HF_TOKEN"] = settings.HF_TOKEN
