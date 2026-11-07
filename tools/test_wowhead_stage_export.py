import unittest

import wowhead_stage_export as exporter


class ExportTests(unittest.TestCase):
    def test_parse_ids_and_ranges(self):
        self.assertEqual([4, 5, 6, 9], exporter.parse_ids(["4,6", "5-5"], 9, 9))
        self.assertEqual([10, 11, 12], exporter.parse_ids(["10-12"], None, None))

    def test_reject_invalid_ranges(self):
        with self.assertRaises(ValueError):
            exporter.parse_ids(["8-3"], None, None)
        with self.assertRaises(ValueError):
            exporter.parse_ids([], None, 4)

    def test_urls_capture_page_and_tooltip(self):
        urls = exporter.make_urls("www", "npc", 42, True)
        self.assertEqual(["tooltip", "page"], [kind for kind, _ in urls])
        self.assertIn("/tooltip/npc/42?", urls[0][1])
        self.assertIn("/npc=42", urls[1][1])

    def test_sql_is_idempotent_and_safe_for_quotes(self):
        raw = b'{"name":"O\'Brien \\ item"}'
        row = exporter.Snapshot("www", "item", 7, "tooltip", "https://example.test/?q=7", 200,
                                "2026-11-07 00:00:00", raw)
        sql = exporter.sql_for(row)
        self.assertIn("ON DUPLICATE KEY UPDATE", sql)
        self.assertIn(raw.hex(), sql)
        self.assertNotIn("O'Brien", sql)

    def test_create_database_is_standalone(self):
        self.assertIn("CREATE DATABASE IF NOT EXISTS `wowhead_stage`", exporter.DDL)
        self.assertIn("LONGTEXT NOT NULL", exporter.DDL)
        self.assertNotIn("INSERT INTO `creature_template`", exporter.DDL)


if __name__ == "__main__":
    unittest.main()
