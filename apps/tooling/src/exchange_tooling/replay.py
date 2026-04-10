from __future__ import annotations

import json
import time
from pathlib import Path
from urllib import request

from .instruments import InstrumentCatalog


def replay_file(path: str, endpoint: str, speed: float = 1.0, dry_run: bool = False) -> None:
    catalog = InstrumentCatalog.default()
    entries = []
    replay_path = Path(path)

    for line in replay_path.read_text(encoding="utf-8").splitlines():
        if not line.strip():
            continue
        entries.append(json.loads(line))

    previous_offset = 0.0
    for entry in entries:
        offset = float(entry.get("offset_seconds", 0))
        wait_time = max((offset - previous_offset) / max(speed, 0.001), 0)
        previous_offset = offset
        time.sleep(wait_time)

        payload = json.dumps(catalog.normalize_payload(entry["order"])).encode("utf-8")
        if dry_run:
            print(payload.decode("utf-8"))
            continue

        req = request.Request(
            f"{endpoint.rstrip('/')}/api/orders",
            data=payload,
            headers={"Content-Type": "application/json"},
            method="POST",
        )
        with request.urlopen(req, timeout=5) as response:
            print(response.status, response.read().decode("utf-8"))
