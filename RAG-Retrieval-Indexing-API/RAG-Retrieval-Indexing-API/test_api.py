import httpx
import json
import time

BASE_URL = "http://localhost:8001"

def test_retrieval_api():
    # 1. Prepare sample documents
    documents = [
        {
            "page_content": "The Burj Khalifa is the tallest building in the world, located in Dubai.",
            "metadata": {"source": "wiki", "id": "doc1"}
        },
        {
            "page_content": "The Eiffel Tower is a wrought-iron lattice tower on the Champ de Mars in Paris, France.",
            "metadata": {"source": "wiki", "id": "doc2"}
        },
        {
            "page_content": "The Great Wall of China is a series of fortifications built along the northern borders of China.",
            "metadata": {"source": "wiki", "id": "doc3"}
        },
        {
            "page_content": "Python is a high-level, interpreted programming language known for its readability.",
            "metadata": {"source": "dev", "id": "doc4"}
        }
    ]

    print(f"--- Testing /index endpoint ---")
    try:
        index_response = httpx.post(
            f"{BASE_URL}/index",
            json={"documents": documents, "rebuild_cache": True},
            timeout=30.0
        )
        print(f"Index Status: {index_response.status_code}")
        print(f"Index Response: {index_response.json()}")
    except Exception as e:
        print(f"Index failed: {e}")
        return

    # Small delay to ensure FAISS/BM25 cache is handled (though it's synchronous in code)
    time.sleep(1)

    print(f"\n--- Testing /retrieve endpoint ---")
    query = "Tell me about the tallest building"
    try:
        retrieve_response = httpx.post(
            f"{BASE_URL}/retrieve",
            json={
                "query": query,
                "top_k": 2,
                "semantic_weight": 0.7,
                "lexical_weight": 0.3,
                "enable_rerank": True
            },
            timeout=30.0
        )
        print(f"Query: {query}")
        print(f"Retrieve Status: {retrieve_response.status_code}")
        
        results = retrieve_response.json()
        for i, doc in enumerate(results.get("documents", [])):
            score = results["scores"][i]
            print(f"Result {i+1} (Score: {score:.4f}): {doc['page_content']}")
        
        print(f"Trace: {results.get('trace')}")
    except Exception as e:
        print(f"Retrieve failed: {e}")

if __name__ == "__main__":
    test_retrieval_api()
