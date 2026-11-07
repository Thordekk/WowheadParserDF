#!/usr/bin/env python3
"""Capture Wowhead tooltip/page responses into MySQL-compatible staging SQL.

This deliberately writes to isolated wowhead_stage tables; it does not generate
INSERTs into TrinityCore's live world tables. Wowhead and TrinityCore data are
not schema- or build-compatible by default.
"""
from __future__ import annotations

import argparse
import datetime as dt
import hashlib
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from dataclasses import dataclass
from typing import Iterable, Optional

CATEGORIES = ("item", "npc", "object", "quest", "zone")
LOCALES = {"www": 0, "fr": 2, "de": 3, "es": 6, "ru": 8, "pt": 9, "it": 10}
PAGE_PATH = {"item": "item", "npc": "npc", "object": "object", "quest": "quest", "zone": "zone"}


@dataclass
class Snapshot:
    locale: str
    category: str
    entity_id: int
    kind: str
    url: str
    status: int
    fetched_at: str
    payload: bytes


def mysql_hex(value: bytes) -> str:
    """SQL-mode-independent UTF-8 text literal using MySQL/MariaDB hex syntax."""
    return "CONVERT(0x" + value.hex() + " USING utf8mb4)"


def sql_for(snapshot: Snapshot) -> str:
    values = [
        mysql_hex(snapshot.locale.encode("utf-8")),
        mysql_hex(snapshot.category.encode("utf-8")),
        str(snapshot.entity_id),
        mysql_hex(snapshot.kind.encode("utf-8")),
        mysql_hex(snapshot.url.encode("utf-8")),
        str(snapshot.status),
        mysql_hex(snapshot.fetched_at.encode("ascii")),
        mysql_hex(snapshot.payload),
        mysql_hex(hashlib.sha256(snapshot.payload).hexdigest().encode("ascii")),
    ]
    return (
        "INSERT INTO `wowhead_stage`.`wowhead_snapshot` "
        "(`locale`,`category`,`entity_id`,`payload_kind`,`source_url`,`http_status`,"
        "`fetched_at_utc`,`payload`,`sha256`) VALUES (" + ",".join(values) + ") "
        "ON DUPLICATE KEY UPDATE `source_url`=VALUES(`source_url`),"
        "`http_status`=VALUES(`http_status`),`fetched_at_utc`=VALUES(`fetched_at_utc`),"
        "`payload`=VALUES(`payload`),`sha256`=VALUES(`sha256`);\n"
    )


DDL = """-- WowheadParser staging export (schema version 1)
-- Isolated archive tables; no TrinityCore production tables are modified.
CREATE DATABASE IF NOT EXISTS `wowhead_stage` CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
CREATE TABLE IF NOT EXISTS `wowhead_stage`.`wowhead_snapshot` (
  `locale` VARCHAR(8) NOT NULL,
  `category` VARCHAR(16) NOT NULL,
  `entity_id` INT UNSIGNED NOT NULL,
  `payload_kind` VARCHAR(16) NOT NULL,
  `source_url` VARCHAR(512) NOT NULL,
  `http_status` SMALLINT UNSIGNED NOT NULL,
  `fetched_at_utc` DATETIME NOT NULL,
  `payload` LONGTEXT NOT NULL,
  `sha256` CHAR(64) CHARACTER SET ascii NOT NULL,
  PRIMARY KEY (`locale`,`category`,`entity_id`,`payload_kind`),
  KEY `idx_wowhead_stage_id` (`category`,`entity_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

"""


def parse_ids(values: Iterable[str], first: Optional[int], last: Optional[int]) -> list[int]:
    ids: set[int] = set()
    for value in values:
        for part in value.split(","):
            part = part.strip()
            if not part:
                continue
            if "-" in part:
                a, b = part.split("-", 1)
                lo, hi = int(a), int(b)
                if lo > hi:
                    raise ValueError(f"invalid descending range: {part}")
                ids.update(range(lo, hi + 1))
            else:
                ids.add(int(part))
    if (first is None) != (last is None):
        raise ValueError("--from-id and --to-id must be provided together")
    if first is not None and last is not None:
        if first > last:
            raise ValueError("--from-id must be <= --to-id")
        ids.update(range(first, last + 1))
    if not ids or min(ids) < 1 or max(ids) > 2**32 - 1:
        raise ValueError("provide one or more positive 32-bit IDs")
    return sorted(ids)


def get_response(url: str, timeout: float, retries: int, delay: float) -> tuple[int, bytes]:
    request = urllib.request.Request(
        url,
        headers={
            "User-Agent": "WowheadParserDF-staging-export/1.0 (polite archival client)",
            "Accept": "application/json,text/html;q=0.9,*/*;q=0.5",
        },
    )
    last_error: Optional[Exception] = None
    for attempt in range(retries + 1):
        if attempt:
            time.sleep(min(30.0, delay * (2 ** (attempt - 1))))
        try:
            with urllib.request.urlopen(request, timeout=timeout) as response:
                body = response.read()
                # Decode/encode once so the SQL payload is valid UTF-8 text.
                body = body.decode("utf-8", errors="replace").encode("utf-8")
                return response.status, body
        except urllib.error.HTTPError as exc:
            last_error = exc
            if exc.code not in (429, 500, 502, 503, 504):
                return exc.code, exc.read().decode("utf-8", errors="replace").encode("utf-8")
            if exc.code == 429 and exc.headers.get("Retry-After"):
                try:
                    time.sleep(min(60.0, float(exc.headers["Retry-After"])))
                except ValueError:
                    pass
        except (urllib.error.URLError, TimeoutError, OSError) as exc:
            last_error = exc
    raise RuntimeError(f"request failed after {retries + 1} attempts: {url}: {last_error}")


def make_urls(host: str, category: str, entity_id: int, include_page: bool) -> list[tuple[str, str]]:
    root = f"https://{host}.wowhead.com"
    tooltip = f"{root}/tooltip/{category}/{entity_id}?{urllib.parse.urlencode({'dataEnv': 1, 'locale': LOCALES[host]})}"
    urls = [("tooltip", tooltip)]
    if include_page:
        urls.append(("page", f"{root}/{PAGE_PATH[category]}={entity_id}"))
    return urls


def main(argv: Optional[list[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--category", required=True, choices=(*CATEGORIES, "all"), help="Wowhead entity type, or all existing parser categories")
    parser.add_argument("--ids", action="append", default=[], help="IDs, comma-list, or range (e.g. 10,12-20); repeatable")
    parser.add_argument("--from-id", type=int, help="inclusive first ID")
    parser.add_argument("--to-id", type=int, help="inclusive last ID")
    parser.add_argument("--locale", choices=sorted(LOCALES), default="www")
    parser.add_argument("--output", default="wowhead-stage.sql", help="output SQL filename; use - for stdout")
    parser.add_argument("--no-page", action="store_true", help="capture tooltip endpoint only (default captures page and tooltip)")
    parser.add_argument("--delay", type=float, default=1.0, help="minimum delay between requests, seconds (default: 1)")
    parser.add_argument("--timeout", type=float, default=30.0)
    parser.add_argument("--retries", type=int, default=3)
    args = parser.parse_args(argv)
    if args.delay < 0 or args.timeout <= 0 or args.retries < 0:
        parser.error("delay/retries must be nonnegative and timeout must be positive")
    try:
        ids = parse_ids(args.ids, args.from_id, args.to_id)
    except (ValueError, OverflowError) as exc:
        parser.error(str(exc))

    out = sys.stdout if args.output == "-" else open(args.output, "w", encoding="utf-8", newline="\n")
    try:
        out.write(DDL)
        categories = CATEGORIES if args.category == "all" else (args.category,)
        total = len(ids) * len(categories) * (1 if args.no_page else 2)
        done = 0
        for category in categories:
            for entity_id in ids:
                for kind, url in make_urls(args.locale, category, entity_id, not args.no_page):
                    status, payload = get_response(url, args.timeout, args.retries, args.delay)
                    timestamp = dt.datetime.now(dt.timezone.utc).strftime("%Y-%m-%d %H:%M:%S")
                    out.write(sql_for(Snapshot(args.locale, category, entity_id, kind, url, status,
                                               timestamp, payload)))
                    done += 1
                    if args.output != "-":
                        out.flush()
                    print(f"[{done}/{total}] {kind} {category}={entity_id}: HTTP {status}", file=sys.stderr)
                    time.sleep(args.delay)
    except Exception as exc:
        print(f"error: {exc}", file=sys.stderr)
        return 1
    finally:
        if out is not sys.stdout:
            out.close()
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
