# WowheadParser for TrinityCore

The original Windows Forms parser scrapes Wowhead pages and emits SQL intended for older TrinityCore/world database layouts. Its entity parsers and table names are tied to old WoW client builds; the checked-in UI lists 7.3.5, 8.0.1, and 9.2.0. Do not assume those direct SQL files are valid for current TrinityCore master.

## Safe staging export (new)

`tools/wowhead_stage_export.py` captures Wowhead tooltip JSON and the corresponding page HTML for the existing entity categories (`item`, `npc`, `object`, `quest`, `zone`) and writes idempotent MySQL/MariaDB SQL into a separate `wowhead_stage` schema. It preserves source URL, locale, HTTP status, fetch time, payload, and SHA-256. It does **not** update TrinityCore's live world tables.

Requires Python 3.9+ and no third-party packages. Examples:

```sh
# One item, both tooltip and full page
python3 tools/wowhead_stage_export.py --category item --ids 19019 --output item-stage.sql

# All of the parser's categories for a small ID range (page + tooltip for each)
python3 tools/wowhead_stage_export.py --category all --from-id 1 --to-id 20 --output stage.sql

# Repeatable IDs and ranges; tooltip-only mode
python3 tools/wowhead_stage_export.py --category npc --ids 100,120-125 --no-page --output npc-stage.sql

# Import into a disposable MySQL/MariaDB instance or database:
mysql < item-stage.sql
```

The exporter is deliberately throttled (one-second delay by default), retries transient HTTP errors, and stores 404/error response bodies with their status rather than silently turning them into game records. `--locale` accepts `www`, `fr`, `de`, `es`, `ru`, `it`, and `pt`. `--delay` can increase the delay; do not remove rate limiting or run broad automated crawls against Wowhead. Large ID ranges take a long time and include gaps/nonexistent IDs. Wowhead does not provide a supported endpoint that enumerates every record, and this is not a complete site-wide database dump.

Validate output before importing it anywhere important. SQL text is hex-encoded so quotes and backslashes in page content are not interpreted by MySQL SQL modes. Re-running an export replaces the matching staging snapshot by its `(locale, category, entity_id, payload_kind)` key.

## TrinityCore compatibility boundary

TrinityCore's current `master` target is the 3.3.5 line; this repository's UI only advertises much newer Wowhead game versions. Retail/Dragonflight/Shadowlands page data cannot be made into valid WotLK game data simply by changing SQL table names. A staged raw capture is useful for manual research or a later reviewed conversion, but it is not import-ready game content. For native SQL, mappings must be written and validated against the exact TrinityCore checkout's `sql/base/dev/world_database.sql`, client-data build, and server-side conventions; many client records come from the matching client data rather than Wowhead. The legacy direct-SQL exporter remains in the app for historical use and is not certified against current master.

## Tests

```sh
python3 -m unittest discover -s tools -p 'test_*.py'
```

## Legacy parser notes

Supported source categories shown by the original application: Black Market (unimplemented in the UI), Creature, Gameobject, Item, Quest, and Zone. The original README advertised 7.3.5, 8.0.1, and 9.2.0. This is not an exhaustive or current Wowhead API client.
