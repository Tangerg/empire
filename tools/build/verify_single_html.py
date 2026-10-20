#!/usr/bin/env python3
"""Verify a late-inline build against its original-byte resource audit log."""
import argparse
import base64
import hashlib
import json
import re
from pathlib import Path


def verify(html_path: Path, build_log: Path) -> dict:
    html_bytes = html_path.read_bytes()
    html = html_bytes.decode('utf8')
    audit_lines = [
        line.split('ORIGINAL_RESOURCE_AUDIT ', 1)[1]
        for line in build_log.read_text().splitlines()
        if 'ORIGINAL_RESOURCE_AUDIT ' in line
    ]
    if len(audit_lines) != 1:
        raise ValueError('Expected exactly one original-resource audit')
    references = json.loads(audit_lines[0])
    embedded = {}
    for match in re.finditer(r'data:[\w/+.-]+;base64,([A-Za-z0-9+/=]+)', html):
        original = base64.b64decode(match[1], validate=True)
        embedded[hashlib.sha256(original).hexdigest()] = len(original)
    missing = [
        item for item in references
        if embedded.get(item['sha256']) != item['byteLength']
    ]
    if missing:
        raise ValueError(f'Original resources missing or altered: {missing}')
    if '__EMPIRE_INLINE_ASSET_' in html:
        raise ValueError('Unresolved binary references')
    markup = re.sub(r'<script\b[^>]*>[\s\S]*?</script>', '', html)
    external = [
        match[1] for match in re.finditer(r'(?:src|href)=[\"\']([^\"\']+)', markup)
        if not match[1].startswith(('data:', '#'))
    ]
    if external:
        raise ValueError(f'External HTML resources: {external}')
    css_urls = re.findall(r'url\([\"\']?([^\)\"\']+)', markup)
    if any(not url.startswith(('data:', '#')) for url in css_urls):
        raise ValueError('External CSS resources')
    if [path.name for path in html_path.parent.iterdir()] != [html_path.name]:
        raise ValueError('Output is not a single file')
    return {
        'html': str(html_path),
        'htmlSha256': hashlib.sha256(html_bytes).hexdigest(),
        'htmlBytes': len(html_bytes),
        'auditedOriginalResources': len(references),
        'verifiedOriginalBytes': sum(item['byteLength'] for item in references),
        'uniqueEmbeddedResourceHashes': len(embedded),
        'allAuditedResourceBytesPresent': True,
        'unresolvedAssetTokens': 0,
        'externalHtmlOrCssResources': external,
        'singleFileOutput': True,
        'originalResources': references,
        'boundary': 'Static resource and exact byte integrity verification; not browser execution or rendering verification',
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--html', type=Path, required=True)
    parser.add_argument('--build-log', type=Path, required=True)
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    report = verify(args.html, args.build_log)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + '\n')
    print(json.dumps({key: value for key, value in report.items() if key != 'originalResources'}, indent=2))


if __name__ == '__main__':
    main()
