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

    # Profile names are persisted by the .NET evaluation module. Keep models
    # local so the default benchmark does not incur embedding API charges.
    EMBEDDING_PROFILES: dict[str, str] = {
        "default": "sentence-transformers/all-MiniLM-L6-v2",
        "e5-base": "intfloat/multilingual-e5-base",
        "phobert-base": "vinai/phobert-base",
        "bge-m3": "BAAI/bge-m3",
        # XQuAD aliases keep FAISS caches separate while reusing local models.
        "xquad-default": "sentence-transformers/all-MiniLM-L6-v2",
        "xquad-e5-base": "intfloat/multilingual-e5-base",
        "xquad-phobert-base": "vinai/phobert-base",
        "xquad-bge-m3": "BAAI/bge-m3",
        # Chunking ablation profiles — same E5 embedding, different chunk sizes.
        "xquad-e5-chunk300": "intfloat/multilingual-e5-base",
        "xquad-e5-chunk800": "intfloat/multilingual-e5-base",
    }

    # Chunking ablation: (chunk_size, chunk_overlap) per profile slug.
    # Profiles not listed here use the documents as-is (pre-chunked by .NET).
    CHUNKING_PROFILES: dict[str, tuple[int, int]] = {
        "xquad-e5-chunk300": (300, 50),
        "xquad-e5-chunk800": (800, 100),
    }

    class Config:
        env_file = ".env"
        extra = "ignore"

settings = Settings()

def embedding_model_for_profile(profile_id: str) -> str:
    if profile_id.endswith("e5-base") or "chunk" in profile_id:
        return settings.EMBEDDING_PROFILES.get("e5-base", settings.EMBEDDING_MODEL)
    if profile_id.endswith("phobert-base"):
        return settings.EMBEDDING_PROFILES["phobert-base"]
    if profile_id.endswith("bge-m3"):
        return settings.EMBEDDING_PROFILES["bge-m3"]
    return settings.EMBEDDING_PROFILES.get(profile_id, settings.EMBEDDING_MODEL)

def embedding_batch_size_for_profile(profile_id: str) -> int:
    """Conservative defaults for the project's 4 GB VRAM machine."""
    if profile_id.endswith("bge-m3"):
        return 1
    if profile_id.endswith("e5-base") or profile_id.endswith("phobert-base") or "chunk" in profile_id:
        return 4
    return 8

def chunking_config_for_profile(profile_id: str) -> tuple[int, int] | None:
    """Return (chunk_size, chunk_overlap) for profiles that need re-chunking,
    or None if documents should be indexed as-is."""
    return settings.CHUNKING_PROFILES.get(profile_id)

# Validation
if not settings.HF_TOKEN:
    print("WARNING: HF_TOKEN is not set.")
else:
    os.environ["HF_TOKEN"] = settings.HF_TOKEN
