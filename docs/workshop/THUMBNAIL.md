# Workshop artwork

`About/Preview.png` is a 1280 × 720 landscape cover shared by the in-game mod preview and Steam. It is under 1 MB. The original square identity artwork is retained as `docs/workshop/brand-square.png`.

Run `tools/Build-WorkshopAssets.ps1` to render the approved Cinema edition from `cover.html` using headless Microsoft Edge and rebuild the real-game screenshot crops. Pass `-BrowserPath` for another Chromium browser. Temporary browser profiles and logs stay in ignored `artifacts/`.

The approved cover uses the real `docs/screenshots/work-preview.png` capture, lowercase “mulld”, capitalized “Priorities”, charcoal/gold lettering, cinematic bars and subtle depth. `mascot.png` is a transparent original illustration generated with the built-in imagegen tool: an Asian man mulling wine, wearing an open black bomber jacket, white shirt, charcoal trousers and simplified flat oxblood boots. The character's visible legs, wine pot, stove and steam are preserved. The screenshot is never generated or redrawn.

Final mascot prompt: simplify only the boots to chunky flat oxblood ankle boots with bold outlines, one shadow plane and dark soles; no laces, stitching, leather texture or glossy highlights. Preserve the character, clothing, pose, pot and transparent background. Cover composition and typography are deterministic HTML/CSS.
