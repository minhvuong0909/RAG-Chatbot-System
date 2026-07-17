import os
import pickle
import re
import gc
import torch
import numpy as np
from typing import Any
from langchain_core.documents import Document
from langchain_community.vectorstores import FAISS
from langchain_huggingface import HuggingFaceEmbeddings
from rank_bm25 import BM25Okapi
from sentence_transformers import CrossEncoder

from config import settings, embedding_batch_size_for_profile, embedding_model_for_profile

# Biên dịch sẵn regex tokenizer (tránh compile lại mỗi lần gọi)
_WORD_PATTERN = re.compile(r'\w+')


class HybridRetrieverService:
    def __init__(self, documents: list[Document] | None = None, rebuild_cache: bool = False, profile_id: str = "default"):
        self.profile_id = profile_id
        self.cache_dir = settings.CACHE_DIR if profile_id == "default" else os.path.join(settings.CACHE_DIR, "profiles", profile_id)
        self.faiss_index_path = settings.FAISS_INDEX_PATH if profile_id == "default" else os.path.join(self.cache_dir, "faiss_index")
        self.bm25_path = settings.BM25_PATH if profile_id == "default" else os.path.join(self.cache_dir, "bm25.pkl")
        self.documents: list[Document] = []
        self.embeddings: HuggingFaceEmbeddings | None = None
        self.vectorstore: FAISS | None = None
        self.bm25: BM25Okapi | None = None
        # Bộ nhớ đệm tokenized corpus để tránh tokenize lặp lại
        self._tokenized_cache: list[list[str]] | None = None
        # Bộ nhớ đệm embedding gần nhất (dùng cho /index trả về C#)
        self._last_embeddings: list[list[float]] | None = None

        # Khởi tạo mô hình embedding
        self._initialize_embeddings()

        if documents:
            # Luôn cố gắng tải các tài liệu đã lưu trong cache trước để gộp
            existing_docs: list[Document] = []
            if not rebuild_cache:
                if self._load_cache():
                    existing_docs = list(self.documents)

            # Gộp tài liệu mới, tránh trùng lặp dựa trên nội dung hoặc metadata["id"]
            existing_contents = {doc.page_content for doc in existing_docs}
            existing_ids = {doc.metadata.get("id") for doc in existing_docs if doc.metadata.get("id")}

            new_docs_to_add = [
                doc for doc in documents
                if doc.page_content not in existing_contents
                and (not doc.metadata.get("id") or doc.metadata.get("id") not in existing_ids)
            ]

            if new_docs_to_add:
                self.documents = existing_docs + new_docs_to_add
                self._build_index()
                self._save_cache()
            else:
                self.documents = existing_docs
                # Chỉ mục đã được tải và kích hoạt thành công từ cache
        else:
            # Cố gắng tải cache sẵn có nếu không truyền tài liệu mới
            self._load_cache()

    def _initialize_embeddings(self):
        device = 'cuda' if torch.cuda.is_available() else 'cpu'
        self.embeddings = HuggingFaceEmbeddings(
            model_name=embedding_model_for_profile(self.profile_id),
            model_kwargs={'device': device},
            encode_kwargs={
                'normalize_embeddings': True,
                'batch_size': embedding_batch_size_for_profile(self.profile_id),
            }
        )
        # PhoBERT has a shorter positional-embedding limit than E5/BGE. Long
        # XQuAD contexts must be truncated deterministically instead of making
        # the entire profile fail during batch indexing.
        if self.profile_id.endswith("phobert-base"):
            self.embeddings._client.max_seq_length = 256

    def _build_index(self):
        """Xây dựng chỉ mục FAISS + BM25 từ self.documents.
        Lưu lại embeddings vào self._last_embeddings để tránh tính lại."""
        if not self.documents:
            return

        # --- 1. Xây dựng chỉ mục FAISS ---
        # Tính embedding một lần duy nhất cho toàn bộ tài liệu
        texts = [doc.page_content for doc in self.documents]
        all_embeddings = self.embeddings.embed_documents(texts)
        self._last_embeddings = all_embeddings

        # Xây dựng FAISS từ embeddings đã tính sẵn (không tính lại)
        text_embedding_pairs = list(zip(texts, all_embeddings))
        metadatas = [doc.metadata for doc in self.documents]

        # Tạo danh sách ID từ metadata
        ids = []
        for doc in self.documents:
            doc_id = doc.metadata.get("id")
            ids.append(str(doc_id) if doc_id else None)

        self.vectorstore = FAISS.from_embeddings(
            text_embeddings=text_embedding_pairs,
            embedding=self.embeddings,
            metadatas=metadatas,
            ids=ids if any(ids) else None
        )

        # --- 2. Xây dựng chỉ mục BM25 ---
        self._tokenized_cache = [_WORD_PATTERN.findall(text.lower()) for text in texts]
        self.bm25 = BM25Okapi(self._tokenized_cache)

    def _tokenize(self, text: str) -> list[str]:
        """Tách từ (tokenize) văn bản bằng regex đã biên dịch sẵn."""
        return _WORD_PATTERN.findall(text.lower())

    def get_last_embeddings(self, docs: list[Document]) -> list[list[float]]:
        """Trả về embeddings đã tính sẵn cho các documents vừa index,
        tránh việc phải gọi embed_documents lần thứ hai."""
        if self._last_embeddings and len(self._last_embeddings) >= len(docs):
            # Embeddings nằm ở cuối danh sách (phần docs mới thêm)
            offset = len(self.documents) - len(docs)
            return self._last_embeddings[offset:]
        # Fallback: tính mới nếu cache không khớp
        return self.embeddings.embed_documents([d.page_content for d in docs])

    def delete_document(self, document_id: str) -> bool:
        """Xóa tài liệu theo document_id khỏi cả FAISS và BM25."""
        if not self.documents:
            return False

        # Tìm các tài liệu cần giữ lại và các chunk ID cần xóa
        docs_to_keep = []
        ids_to_delete = []
        doc_id_lower = document_id.lower()

        for doc in self.documents:
            doc_id_meta = doc.metadata.get("document_id")
            if doc_id_meta and str(doc_id_meta).lower() == doc_id_lower:
                chunk_id = doc.metadata.get("id")
                if chunk_id:
                    ids_to_delete.append(str(chunk_id))
            else:
                docs_to_keep.append(doc)

        if not ids_to_delete:
            return False

        # Cập nhật danh sách tài liệu
        self.documents = docs_to_keep

        # Xóa các vector tương ứng trong FAISS
        if self.vectorstore and ids_to_delete:
            try:
                self.vectorstore.delete(ids_to_delete)
            except Exception as e:
                print(f"Error calling vectorstore.delete: {e}. Rebuilding FAISS index...")
                # Dự phòng: Xây dựng lại chỉ mục FAISS từ các tài liệu còn lại
                if self.documents:
                    self._build_index()
                else:
                    self.vectorstore = None

        # Cập nhật chỉ mục BM25
        if self.documents:
            self._tokenized_cache = [self._tokenize(doc.page_content) for doc in self.documents]
            self.bm25 = BM25Okapi(self._tokenized_cache)
        else:
            self.bm25 = None
            self.vectorstore = None
            self._tokenized_cache = None

        # Lưu cache mới đã cập nhật
        self._save_cache()
        return True

    def _save_cache(self):
        os.makedirs(self.cache_dir, exist_ok=True)

        if self.vectorstore:
            self.vectorstore.save_local(self.faiss_index_path)
        elif os.path.exists(self.faiss_index_path):
            import shutil
            shutil.rmtree(self.faiss_index_path, ignore_errors=True)

        if self.bm25:
            with open(self.bm25_path, "wb") as f:
                pickle.dump({"bm25": self.bm25, "docs": self.documents}, f)
        elif os.path.exists(self.bm25_path):
            try:
                os.remove(self.bm25_path)
            except Exception:
                pass

    def _load_cache(self) -> bool:
        if not os.path.exists(self.faiss_index_path) or not os.path.exists(self.bm25_path):
            return False

        try:
            self.vectorstore = FAISS.load_local(
                self.faiss_index_path,
                self.embeddings,
                allow_dangerous_deserialization=True
            )

            with open(self.bm25_path, "rb") as f:
                data = pickle.load(f)
                self.bm25 = data["bm25"]
                self.documents = data["docs"]

            # Tạo lại tokenized cache từ tài liệu đã tải
            self._tokenized_cache = [self._tokenize(doc.page_content) for doc in self.documents]
            return True
        except Exception as e:
            print(f"Cache load failed: {e}")
            return False

    def semantic_search(self, query: str, k: int = 10, dataset_id: str | None = None) -> list[tuple[Document, float]]:
        """Tìm kiếm tương đồng ngữ nghĩa bằng FAISS.
        Chuyển đổi khoảng cách L2 sang điểm tương đồng."""
        if not self.vectorstore:
            return []
        metadata_filter = {"dataset_id": dataset_id} if dataset_id else None
        docs_and_scores = self.vectorstore.similarity_search_with_score(query, k=k, filter=metadata_filter)
        # Chuyển L2 distance -> similarity score trong 1 list comprehension (nhanh hơn append)
        return [(doc, 1.0 / (1.0 + score)) for doc, score in docs_and_scores]

    def lexical_search(self, query: str, k: int = 10, dataset_id: str | None = None) -> list[tuple[Document, float]]:
        """Tìm kiếm từ khóa bằng BM25.
        Dùng np.argpartition để lấy top-k nhanh hơn O(n log n) sort."""
        if not self.bm25 or not self.documents:
            return []
        tokenized_query = self._tokenize(query)
        scores = self.bm25.get_scores(tokenized_query)

        if dataset_id:
            eligible = np.array([
                str(doc.metadata.get("dataset_id", "")).lower() == dataset_id.lower()
                for doc in self.documents
            ], dtype=bool)
            if not eligible.any():
                return []
            scores = np.where(eligible, scores, -np.inf)
            k = min(k, int(eligible.sum()))

        # Dùng argpartition O(n) thay vì sort O(n log n) cho corpus lớn
        if len(scores) <= k:
            top_indices = np.argsort(scores)[::-1]
        else:
            # argpartition lấy top-k index nhanh, sau đó chỉ sort k phần tử
            top_indices = np.argpartition(scores, -k)[-k:]
            top_indices = top_indices[np.argsort(scores[top_indices])[::-1]]

        return [(self.documents[i], float(scores[i])) for i in top_indices]


class RerankerService:
    """Dịch vụ xếp hạng lại (Reranker) sử dụng CrossEncoder.
    Sử dụng Singleton pattern để chỉ tải mô hình một lần duy nhất."""
    _instance = None

    def __init__(self):
        self.model = None
        try:
            device = 'cuda' if torch.cuda.is_available() else 'cpu'
            self.model = CrossEncoder(settings.CROSS_ENCODER_MODEL, device=device)
        except Exception as e:
            print(f"Reranker init error: {e}")

    @classmethod
    def get_instance(cls):
        if cls._instance is None:
            cls._instance = cls()
        return cls._instance

    def rerank(self, query: str, documents: list[Document], top_k: int = 3) -> list[tuple[Document, float]]:
        """Xếp hạng lại danh sách tài liệu dựa trên mức độ liên quan với câu hỏi."""
        if not self.model or not documents:
            return [(doc, 0.0) for doc in documents[:top_k]]

        pairs = [[query, doc.page_content] for doc in documents]
        scores = self.model.predict(pairs)

        # Dùng np.argpartition để lấy top_k nhanh thay vì sort toàn bộ
        if len(scores) <= top_k:
            sorted_indices = np.argsort(scores)[::-1]
        else:
            top_indices = np.argpartition(scores, -top_k)[-top_k:]
            sorted_indices = top_indices[np.argsort(scores[top_indices])[::-1]]

        return [(documents[i], float(scores[i])) for i in sorted_indices]


# ========== Hàm quản lý Retriever toàn cục (Singleton) ==========

_retriever_instances: dict[str, HybridRetrieverService] = {}


def get_retriever(
    profile_id: str = "default",
    force_reload: bool = False,
    documents: list[Document] | None = None,
    rebuild_cache: bool = False
) -> HybridRetrieverService:
    """Lấy hoặc tạo mới Retriever toàn cục.
    Chỉ khởi tạo lại khi cần thiết để tiết kiệm RAM và thời gian."""
    # Benchmark embeddings are executed sequentially on a 4 GB GPU. Retaining
    # E5, PhoBERT and BGE-M3 instances would accumulate model memory and cause
    # an avoidable OOM. Production/default profile remains cached.
    if profile_id.startswith("xquad-"):
        for cached_profile in [key for key in _retriever_instances if key.startswith("xquad-") and key != profile_id]:
            del _retriever_instances[cached_profile]
        gc.collect()
        if torch.cuda.is_available():
            torch.cuda.empty_cache()

    if profile_id not in _retriever_instances or force_reload or documents:
        _retriever_instances[profile_id] = HybridRetrieverService(
            documents=documents,
            rebuild_cache=rebuild_cache,
            profile_id=profile_id)
    return _retriever_instances[profile_id]


def hybrid_retrieve(
    query: str,
    dataset_id: str | None = None,
    top_k: int = 3,
    semantic_weight: float = 0.7,
    lexical_weight: float = 0.3,
    enable_rerank: bool = True,
    profile_id: str = "default"
) -> dict[str, Any]:
    """Tìm kiếm kết hợp (Hybrid Search) sử dụng thuật toán Weighted RRF.
    Kết hợp kết quả từ FAISS (semantic) và BM25 (lexical),
    sau đó xếp hạng lại bằng CrossEncoder nếu được bật."""
    retriever = get_retriever(profile_id=profile_id)

    # Tăng số lượng ứng viên để gộp (gấp 4 lần top_k)
    k_candidates = top_k * 4

    # Thực hiện 2 luồng tìm kiếm song song về mặt logic
    semantic = retriever.semantic_search(query, k=k_candidates, dataset_id=dataset_id)
    lexical = retriever.lexical_search(query, k=k_candidates, dataset_id=dataset_id)

    # Hằng số RRF (k=60 là giá trị chuẩn quốc tế)
    rrf_k = 60
    doc_map: dict[str, dict[str, Any]] = {}

    # Xử lý Tìm kiếm tương đồng (Semantic) - tính điểm RRF theo thứ hạng
    for rank, (doc, _) in enumerate(semantic):
        doc_id = doc.metadata.get("id") or id(doc)
        if doc_id not in doc_map:
            doc_map[doc_id] = {"doc": doc, "sem_rrf": 0.0, "lex_rrf": 0.0}
        doc_map[doc_id]["sem_rrf"] = 1.0 / (rrf_k + rank + 1)

    # Xử lý Tìm kiếm từ khóa (Lexical) - tính điểm RRF theo thứ hạng
    for rank, (doc, _) in enumerate(lexical):
        doc_id = doc.metadata.get("id") or id(doc)
        if doc_id not in doc_map:
            doc_map[doc_id] = {"doc": doc, "sem_rrf": 0.0, "lex_rrf": 0.0}
        doc_map[doc_id]["lex_rrf"] = 1.0 / (rrf_k + rank + 1)

    # Tính điểm kết hợp có trọng số
    merged = [
        (data["doc"], semantic_weight * data["sem_rrf"] + lexical_weight * data["lex_rrf"])
        for data in doc_map.values()
    ]
    merged.sort(key=lambda x: x[1], reverse=True)
    results = merged[:k_candidates]

    trace = [f"Profile({profile_id})", "Retrieval(Hybrid-RRF)", "DatasetFilter" if dataset_id else "AllDatasets", "Merge"]

    # Xếp hạng lại bằng CrossEncoder (Rerank) nếu được bật
    if enable_rerank:
        reranker = RerankerService.get_instance()
        reranked = reranker.rerank(query, [d for d, _ in results], top_k=top_k)
        docs = [d for d, _ in reranked]
        scores = [float(s) for _, s in reranked]
        trace.append("Rerank(CrossEncoder)")
    else:
        results = results[:top_k]
        docs = [d for d, _ in results]
        scores = [float(s) for _, s in results]

    return {
        "documents": docs,
        "scores": scores,
        "trace": trace
    }


def _cosine_similarity(vec_a: list[float], vec_b: list[float]) -> float:
    """Cosine similarity giữa 2 vector, ép về khoảng [0, 1].
    Trả 0.0 nếu một trong hai vector rỗng/toàn số 0."""
    a = np.asarray(vec_a, dtype=np.float32)
    b = np.asarray(vec_b, dtype=np.float32)
    norm_a = np.linalg.norm(a)
    norm_b = np.linalg.norm(b)
    if norm_a == 0 or norm_b == 0:
        return 0.0
    cos = float(np.dot(a, b) / (norm_a * norm_b))
    # Cosine ∈ [-1, 1]; với văn bản thường ≥ 0, nhưng clamp cho an toàn
    return max(0.0, min(1.0, cos))


def score_answer(answer: str, context: str, question: str) -> dict[str, float]:
    """Chấm điểm câu trả lời bằng embedding (chuẩn RAGAS):
    - faithfulness = cosine(answer, context): độ bám tài liệu (chống bịa)
    - relevance    = cosine(answer, question): độ liên quan tới câu hỏi
    Tái dùng mô hình embedding đã load sẵn trong retriever (không load lại)."""
    answer = (answer or "").strip()
    context = (context or "").strip()
    question = (question or "").strip()

    if not answer:
        return {"faithfulness": 0.0, "relevance": 0.0}

    embeddings = get_retriever().embeddings
    # Nhúng cả 3 đoạn trong 1 lần gọi cho nhanh
    vecs = embeddings.embed_documents([answer, context if context else " ", question if question else " "])
    answer_vec, context_vec, question_vec = vecs[0], vecs[1], vecs[2]

    return {
        "faithfulness": _cosine_similarity(answer_vec, context_vec) if context else 0.0,
        "relevance": _cosine_similarity(answer_vec, question_vec) if question else 0.0,
    }
