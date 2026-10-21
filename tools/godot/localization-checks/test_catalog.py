import unittest
from validate_catalog import CatalogError, read_catalog

HEADER = 'msgid ""\nmsgstr ""\n"Language: zh_CN\\n"\n"Content-Type: text/plain; charset=UTF-8\\n"\n\n'
ENTRY = 'msgid "ui.example"\nmsgstr "示例"\n'


class CatalogSourceTests(unittest.TestCase):
    def test_multiline_and_escaped_content(self):
        source = HEADER + 'msgid "ui.example"\nmsgstr "第一行\\n"\n"第二行 \\\"引号\\\""\n'
        self.assertEqual(read_catalog(source), {"ui.example": '第一行\n第二行 "引号"'})

    def test_duplicate_key_is_rejected(self):
        with self.assertRaisesRegex(CatalogError, "Duplicate message"):
            read_catalog(HEADER + ENTRY + "\n" + ENTRY)

    def test_duplicate_without_blank_line_is_rejected(self):
        with self.assertRaisesRegex(CatalogError, "Duplicate message"):
            read_catalog(HEADER + ENTRY + ENTRY)

    def test_empty_and_whitespace_are_rejected(self):
        for value in ['""', '" \\n"']:
            with self.subTest(value=value), self.assertRaisesRegex(CatalogError, "Empty translation"):
                read_catalog(HEADER + 'msgid "ui.example"\nmsgstr ' + value)

    def test_fuzzy_is_rejected(self):
        with self.assertRaisesRegex(CatalogError, "Fuzzy"):
            read_catalog(HEADER + "#, csharp-format, fuzzy\n" + ENTRY)

    def test_malformed_quote_and_missing_value_are_rejected(self):
        for value in ['msgid "ui.example"\nmsgstr "unclosed', 'msgid "ui.example"']:
            with self.subTest(value=value), self.assertRaises(CatalogError):
                read_catalog(HEADER + value)

    def test_missing_and_wrong_locale_are_rejected(self):
        for source in [ENTRY, (HEADER + ENTRY).replace("zh_CN", "en")]:
            with self.subTest(source=source), self.assertRaisesRegex(CatalogError, "zh_CN"):
                read_catalog(source)

    def test_obsolete_and_nonsemantic_keys_are_rejected(self):
        for source in [HEADER + "#~ " + ENTRY, HEADER + ENTRY.replace("ui.example", "示例")]:
            with self.subTest(source=source), self.assertRaises(CatalogError):
                read_catalog(source)

    def test_empty_catalog_is_rejected(self):
        with self.assertRaisesRegex(CatalogError, "no messages"):
            read_catalog(HEADER)


if __name__ == "__main__":
    unittest.main()
