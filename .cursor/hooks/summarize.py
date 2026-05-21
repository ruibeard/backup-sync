#!/usr/bin/env python3
"""After each agent turn, auto-submit /summarize to compact context."""
import json
import sys

payload = json.loads(sys.stdin.read() or "{}")
if payload.get("status") == "completed":
    print(json.dumps({"followup_message": "/summarize"}))
else:
    print("{}")
