#!/usr/bin/env bash
# Локальная раздача WebGL-сборки для проверки с телефона по Wi-Fi.
#
#   scripts/serve-webgl.sh [папка_сборки] [порт]
#
# По умолчанию: Builds/WebGL, порт 8080. Слушает 0.0.0.0 и печатает адрес в локальной сети.
# Для файлов *.br / *.gz отдаёт Content-Encoding и Content-Type исходного файла
# (.wasm → application/wasm, .js → application/javascript, .data → application/octet-stream).
# Без SDK Яндекс Игр игра идёт через NullPlatformServices — это ожидаемо.

set -euo pipefail

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIR="${1:-$ROOT/Builds/WebGL}"
PORT="${2:-8080}"

if [ ! -f "$DIR/index.html" ]; then
  echo "Нет $DIR/index.html — сначала scripts/build-webgl.sh" >&2
  exit 1
fi

IP="$(ipconfig getifaddr en0 2>/dev/null || ipconfig getifaddr en1 2>/dev/null || hostname -I 2>/dev/null | awk '{print $1}' || true)"
echo "[serve] папка: $DIR"
echo "[serve] на Маке:     http://localhost:$PORT"
echo "[serve] с телефона:  http://${IP:-<IP Мака>}:$PORT   (та же Wi-Fi сеть)"
echo "[serve] Ctrl+C — остановить"

exec python3 -I - "$DIR" "$PORT" <<'PY'
import http.server, mimetypes, os, sys

root, port = sys.argv[1], int(sys.argv[2])
TYPES = {".wasm": "application/wasm", ".js": "application/javascript",
         ".data": "application/octet-stream", ".json": "application/json"}
ENCODINGS = {".br": "br", ".gz": "gzip"}

class Handler(http.server.SimpleHTTPRequestHandler):
    def __init__(self, *a, **kw):
        super().__init__(*a, directory=root, **kw)

    def guess_type(self, path):
        base, ext = os.path.splitext(path)
        if ext in ENCODINGS:
            path = base
            ext = os.path.splitext(path)[1]
        return TYPES.get(ext) or mimetypes.guess_type(path)[0] or "application/octet-stream"

    def end_headers(self):
        ext = os.path.splitext(self.path.split("?")[0])[1]
        if ext in ENCODINGS:
            self.send_header("Content-Encoding", ENCODINGS[ext])
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

http.server.ThreadingHTTPServer(("0.0.0.0", port), Handler).serve_forever()
PY
