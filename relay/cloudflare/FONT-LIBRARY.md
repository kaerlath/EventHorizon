# Font library — 0.9.0

The announcement editor now offers 18 additional families alongside Cinzel and the three generic choices. Search by family or category. Existing size, bold, italic, underline, strike, outline, glow, color and spacing controls apply to all families.

| Style | Families |
|---|---|
| Futuristic | Orbitron, Rajdhani, Exo 2, Chakra Petch, Oxanium |
| Modern / readable | Space Grotesk, Inter, Nunito Sans |
| Fantasy / literary / elegant | Cormorant Garamond, Uncial Antiqua, Lora, Playfair Display |
| Gothic | Grenze Gotisch |
| Script / handwritten | Great Vibes, Dancing Script, Caveat |
| Monospace / typewriter | JetBrains Mono, Special Elite |

## Deployment

From the existing PowerShell relay directory, run `& $ehPnpm exec wrangler deploy`.
Wrangler runs `node prepare-fonts.mjs` first. This downloads the unmodified font files and each family's license (SIL OFL, or Apache 2.0 for Special Elite) from the official google/fonts repository into assets/fonts/shared, then uploads them with the Worker using the FONTS static-assets binding. Completed font files are reused after an interrupted download. Temporary network failures retry up to three times. Download failures stop deployment without removing completed files. No new secrets, separate font service, or external font request during rendering is needed.

The development sandbox cannot download these assets; the first collection download and live rendering check must be completed from the user's PowerShell environment. The plugin builds independently. Deploy the relay before testing its new font selections.

Fonts are loaded only for selected families. Static regular/bold and italic faces are included when available; variable faces retain their weight ranges. Where a family lacks a requested face, Chromium simulates bold/slant. The shared fonts are supplied to the cloud renderer, not installed as Windows system fonts. In-game UI lettering still uses its existing fonts; the rendered announcement preview shows these announcement faces accurately.

## Importing your own

Connect Discord, edit an event, expand Announcement typography & effects, then Import custom font / My font library. Choose a TTF, OTF, WOFF or WOFF2 file, label it, confirm its license permits uploading/embedding, and click Upload and use font. Use Render preview before publishing. Each imported file is a separate selector entry; regular/bold/italic files are not grouped into a family automatically. Variable imported files currently use their default face.

Limits: 1 MB per file, 20 unique files per Discord account, 2,000 files across the beta relay. Font collections (.ttc) are unsupported. File signatures, table bounds and declared decompressed sizes are checked; Chromium's font loader must also accept the font before a screenshot can complete. If a font cannot load, choose a different file rather than publishing a fallback.

Imports are retained in Durable Object storage, chunked below per-value limits, and scoped to the Discord account and relay. Repeated imports of identical files reuse their IDs. Refresh my font library restores the list after reconnect/reload. Drafts/templates retain IDs, not local file paths or font bytes. Switching account/relay requires importing your own copy or choosing a shared font. Copying another organizer's event does not grant access to that organizer's private font files.

The upload API is authenticated POST /fonts; GET /fonts returns only the caller's labels/IDs/formats. There is no public custom-font download route. The renderer accepts only built-in names and opaque imported IDs, never arbitrary CSS or font URLs. Selected fonts are embedded into the generated HTML as data URLs and a readiness check prevents capturing before font loading completes.

## Verification

After deployment, /health reports version 0.9.0 and fontLibrary: true (feature availability, not a render test). Reload the plugin, preview an announcement using a shared family such as Lora in both normal and italic, then import a permitted local font and preview it. Reconnect Discord and refresh the font library; the import should remain. Publish/update only in the test server until the visual result is confirmed.
