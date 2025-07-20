#!/bin/bash

# CheckDiffTable Docker環境管理スクリプト

set -e

DOCKER_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$DOCKER_DIR")"

# 色付きログ出力
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 関数定義
start_db() {
    log_info "PostgreSQLデータベースを起動中..."
    cd "$DOCKER_DIR"
    docker-compose up -d postgres
    log_success "PostgreSQLが起動しました"
    
    # ヘルスチェック待機
    log_info "データベースの準備完了を待機中..."
    for i in {1..30}; do
        if docker-compose exec -T postgres pg_isready -U postgres -d checkdiff_db > /dev/null 2>&1; then
            log_success "データベースの準備が完了しました"
            return 0
        fi
        echo -n "."
        sleep 1
    done
    log_error "データベースの起動がタイムアウトしました"
    return 1
}

start_all() {
    log_info "全サービス（PostgreSQL + pgAdmin）を起動中..."
    cd "$DOCKER_DIR"
    docker-compose up -d
    log_success "全サービスが起動しました"
    log_info "pgAdmin: http://localhost:8080"
    log_info "PostgreSQL: localhost:5432"
}

stop() {
    log_info "サービスを停止中..."
    cd "$DOCKER_DIR"
    docker-compose down
    log_success "サービスが停止しました"
}

restart() {
    log_info "サービスを再起動中..."
    stop
    start_db
}

status() {
    log_info "サービス状態を確認中..."
    cd "$DOCKER_DIR"
    docker-compose ps
}

logs() {
    log_info "ログを表示中（Ctrl+Cで終了）..."
    cd "$DOCKER_DIR"
    docker-compose logs -f "${2:-postgres}"
}

reset_data() {
    log_warning "⚠️  データベースの全データが削除されます"
    read -p "続行しますか？ (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        log_info "データベースをリセット中..."
        cd "$DOCKER_DIR"
        docker-compose down -v
        docker-compose up -d postgres
        log_success "データベースがリセットされました"
    else
        log_info "リセットがキャンセルされました"
    fi
}

run_sql() {
    if [ -z "$2" ]; then
        log_error "SQLファイルパスを指定してください"
        echo "使用方法: $0 sql <SQLファイルパス>"
        return 1
    fi
    
    local sql_file="$2"
    if [ ! -f "$sql_file" ]; then
        log_error "SQLファイルが見つかりません: $sql_file"
        return 1
    fi
    
    log_info "SQLファイルを実行中: $sql_file"
    cd "$DOCKER_DIR"
    docker-compose exec -T postgres psql -U postgres -d checkdiff_db -f "/tmp/$(basename "$sql_file")" < "$sql_file"
    log_success "SQLファイルの実行が完了しました"
}

show_connection() {
    log_info "データベース接続情報:"
    echo "  Host: localhost"
    echo "  Port: 5432"
    echo "  Database: checkdiff_db"
    echo "  Username: postgres"
    echo "  Password: password"
    echo ""
    log_info "接続文字列:"
    echo "  Host=localhost;Database=checkdiff_db;Username=postgres;Password=password"
}

show_help() {
    echo "CheckDiffTable Docker環境管理スクリプト"
    echo ""
    echo "使用方法:"
    echo "  $0 <command> [options]"
    echo ""
    echo "コマンド:"
    echo "  start-db     PostgreSQLのみ起動"
    echo "  start-all    PostgreSQL + pgAdmin起動"
    echo "  stop         全サービス停止"
    echo "  restart      サービス再起動"
    echo "  status       サービス状態確認"
    echo "  logs [service] ログ表示（デフォルト: postgres）"
    echo "  reset        データベースリセット（全データ削除）"
    echo "  sql <file>   SQLファイル実行"
    echo "  connection   接続情報表示"
    echo "  help         このヘルプを表示"
    echo ""
    echo "例:"
    echo "  $0 start-db"
    echo "  $0 logs postgres"
    echo "  $0 sql ../database_schema.sql"
}

# メイン処理
case "$1" in
    "start-db"|"db")
        start_db
        ;;
    "start-all"|"all")
        start_all
        ;;
    "stop")
        stop
        ;;
    "restart")
        restart
        ;;
    "status")
        status
        ;;
    "logs")
        logs "$@"
        ;;
    "reset")
        reset_data
        ;;
    "sql")
        run_sql "$@"
        ;;
    "connection"|"conn")
        show_connection
        ;;
    "help"|"--help"|"-h")
        show_help
        ;;
    "")
        log_error "コマンドを指定してください"
        show_help
        exit 1
        ;;
    *)
        log_error "不正なコマンド: $1"
        show_help
        exit 1
        ;;
esac
