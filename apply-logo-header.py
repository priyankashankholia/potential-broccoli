#!/usr/bin/env python3
"""Replaces the typeset wordmark with the actual logo mark beside the name."""
import re, pathlib, sys

WEB = pathlib.Path("rent-manager-web/src")
HTML = WEB / "app/app.html"
CSS = WEB / "app/app.css"

for p in (HTML, CSS):
    if not p.exists():
        sys.exit(f"Missing {p}. Run this from the repo root.")

NEW = ('<h1 class="brand">'
       '<img class="brand-mark" src="icon-192.png" alt="">'
       '<span class="brand-name">Narera Complex</span>'
       '</h1>')

html = HTML.read_text(encoding="utf-8")

if "brand-mark" in html:
    print("  skip  app.html (already using the logo)")
else:
    pattern = re.compile(
        r'<h1 class="wordmark">\s*'
        r'<span class="wordmark-name">Narera</span>\s*'
        r'<span class="wordmark-sub">Complex</span>\s*'
        r'</h1>')
    html, n = pattern.subn(NEW, html)
    if n != 2:
        sys.exit(f"Expected 2 wordmarks, replaced {n}. Nothing was changed.")
    HTML.write_text(html, encoding="utf-8")
    print(f"  ok    app.html ({n} headings replaced)")

MARKER = "/* ---- Brand lockup"

BLOCK = """

/* ---- Brand lockup -------------------------------------------------- */

.brand {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 0 0 10px;
}

.brand-mark {
  width: 44px;
  height: 44px;
  flex-shrink: 0;
  /* The PNG has a white background; blending hides it on light cards. */
  mix-blend-mode: multiply;
}

.brand-name {
  font-size: 22px;
  font-weight: 600;
  letter-spacing: -0.2px;
  color: #232a33;
  line-height: 1.2;
}

.login-card .brand {
  flex-direction: column;
  gap: 10px;
}

.login-card .brand-mark {
  width: 64px;
  height: 64px;
}

.login-card .brand-name {
  font-size: 25px;
}

@media (max-width: 640px) {
  .brand-mark { width: 38px; height: 38px; }
  .brand-name { font-size: 20px; }
}
"""

css = CSS.read_text(encoding="utf-8")
if MARKER in css:
    print("  skip  app.css (already styled)")
else:
    CSS.write_text(css.rstrip() + BLOCK, encoding="utf-8")
    print("  ok    app.css (brand styles added)")

print("\nHard refresh with Ctrl+Shift+R.")