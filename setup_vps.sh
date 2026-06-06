#!/bin/bash
# Script cài đặt môi trường ban đầu cho VPS Ubuntu/Debian

set -e

echo "=== 1. Cập nhật hệ thống ==="
sudo apt-get update && sudo apt-get upgrade -y

echo "=== 2. Cài đặt Docker ==="
if ! command -v docker &> /dev/null; then
    curl -fsSL https://get.docker.com -o get-docker.sh
    sudo sh get-docker.sh
    sudo usermod -aG docker $USER
    rm get-docker.sh
    echo "Đã cài đặt Docker thành công."
else
    echo "Docker đã được cài đặt."
fi

echo "=== 3. Cài đặt Docker Compose ==="
if ! docker compose version &> /dev/null; then
    sudo apt-get install -y docker-compose-plugin
    echo "Đã cài đặt Docker Compose."
else
    echo "Docker Compose đã được cài đặt."
fi

echo "=== 4. Cài đặt Nginx & Certbot (SSL) ==="
sudo apt-get install -y nginx certbot python3-certbot-nginx

echo "=== 5. Tạo thư mục chứa ứng dụng ==="
sudo mkdir -p /var/www/rag-chatbot-system
sudo chown -R $USER:$USER /var/www/rag-chatbot-system

echo "=== Môi trường VPS đã sẵn sàng! ==="
