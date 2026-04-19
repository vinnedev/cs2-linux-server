import sys
sys.path.insert(0, "scripts")
from _common import log

from http.server import BaseHTTPRequestHandler, HTTPServer

PORT = 8000


class Handler(BaseHTTPRequestHandler):
    def do_GET(self):
        self.send_response(200)
        self.send_header("Content-Type", "text/plain")
        self.end_headers()
        self.wfile.write(b"Hello, World!")
        log.ok(f"{self.client_address[0]} {self.command} {self.path}")

    def log_message(self, *args):
        pass


if __name__ == "__main__":
    log.section(f"Healthcheck server listening on 0.0.0.0:{PORT}")
    try:
        HTTPServer(("0.0.0.0", PORT), Handler).serve_forever()
    except KeyboardInterrupt:
        log.warn("Stopped")
