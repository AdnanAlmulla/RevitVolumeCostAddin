# A stand-in for the website's update endpoints, for a trial run of the uploader. Serves one release zip.
import hashlib, json, os, sys
from http.server import BaseHTTPRequestHandler, HTTPServer

ZIP_PATH = sys.argv[1]
PORT = int(sys.argv[2])
CORRUPT = len(sys.argv) > 3 and sys.argv[3] == 'corrupt'
VERSION = os.path.basename(ZIP_PATH).rsplit('-', 1)[1][:-4]
ZIP_BYTES = open(ZIP_PATH, 'rb').read()
PASSWORD = 'trial-password'
STANDARD = {
    "version": "a1b2c3d4e5f6", "agreedOn": "28 Nov 2026",
    "parameters": [{"name": "Division_Shared", "records": "Which division.", "allowedValues": ["Piles", "Roof"],
                    "otherValuesNote": None, "categories": ["Walls"], "fillOn": "Every element.", "ifBlank": "Division \"Unassigned\""}],
    "parameterSettings": "Shared.", "rules": ["Spell exactly."], "examples": [{"element": "A core wall", "values": "Division_Shared = Tower"}],
}

class Handler(BaseHTTPRequestHandler):
    def log_message(self, *args):
        sys.stderr.write("%s %s\n" % (self.path, self.headers.get('X-Site-Password')))

    def send(self, status, body, content_type='application/json'):
        self.send_response(status)
        self.send_header('Content-Type', content_type)
        self.send_header('Content-Length', str(len(body)))
        self.end_headers()
        self.wfile.write(body)

    def do_GET(self):
        if self.headers.get('X-Site-Password') != PASSWORD:
            return self.send(401, b'{}')
        if self.path == '/api/addin/latest':
            body = {"version": VERSION, "sha256": hashlib.sha256(ZIP_BYTES).hexdigest(), "size": len(ZIP_BYTES), "releasedAt": "2026-11-27T11:00:00Z"}
            return self.send(200, json.dumps(body).encode())
        if self.path == '/api/addin/download/' + VERSION:
            payload = ZIP_BYTES[:-10] + b'0123456789' if CORRUPT else ZIP_BYTES
            return self.send(200, payload, 'application/zip')
        if self.path == '/api/parameter-standard':
            return self.send(200, json.dumps(STANDARD).encode())
        return self.send(404, b'{}')

HTTPServer(('127.0.0.1', PORT), Handler).serve_forever()
