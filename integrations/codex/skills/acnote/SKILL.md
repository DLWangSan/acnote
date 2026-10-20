---
name: acnote
description: Create or open AcNote literature libraries, import EndNote and other supported reference files, and find, verify, insert, or update citations in an active Word document. Apply when organizing source libraries or writing a Word manuscript with AcNote references; do not invent references or edit the .acnote database directly.
---

# AcNote citation workflow

Use the AcNote MCP tools as the only interface to the user's library and AcNote-managed Word citations. Do not read or write the `.acnote` SQLite file directly.

## Prepare a library

- Call `get_status` first. Reuse the current library when it is the user's intended target.
- Use `create_library` only with an explicit absolute `.acnote` path. It never overwrites an existing library; do not delete or rename an existing target to force creation.
- Use `open_library` for an existing `.acnote` file.
- Use `import_references` only after the intended target library is open. Pass absolute paths and preserve each EndNote `.enl` file beside its matching `.Data` directory.
- Review every per-file result. Report added and updated counts, warnings, and failures. A partial import may have already added valid records; do not retry the whole batch blindly.

## Find and verify references

1. Call `get_status` before a citation workflow. If AcNote, its intended library, or the required Word document is unavailable, tell the user exactly what to open.
2. Search with meaningful title, author, year, DOI, or topic terms. Treat search results as candidates, not automatic proof that a source supports a claim.
3. Call `get_references` for the final candidate IDs. Never invent or reconstruct an ID.
4. Report material missing fields before insertion. Prefer a complete matching record when duplicates exist; ask the user when identity or relevance is genuinely ambiguous.

When the user explicitly asks to correct or enrich library metadata, use `update_reference` with the complete record returned by `get_references`, preserving its stable ID, then read it back to verify the saved fields. Use `install_style` only when the user asks to install or update a style, and verify the active style and preview afterward.

## Choose the Word location

Call `get_word_context` before inserting.

- Use `placement: selection` only when the user intentionally positioned a collapsed cursor and asked to insert there.
- For a location described in prose, use a short exact excerpt from the document as `anchor`, then insert `before` or `after` it.
- Prefer an anchor that occurs once. If the tool reports multiple matches, inspect more context and use `occurrence` only after identifying the intended occurrence.
- Never select, replace, delete, or rewrite manuscript text merely to place a citation.
- Use `narrative` only when the authors are grammatically part of the sentence. Otherwise use `parenthetical`.

When deciding whether a claim needs a citation or which source supports it, explain the proposed source and location before a batch of insertions unless the user already gave an explicit source-to-location mapping.

## Apply and check

Insert with stable IDs returned by AcNote. After multiple insertions, call `update_citations` once to normalize numbering, author-year disambiguation, and the bibliography. Then call `get_word_context` around important changed locations when practical and summarize the inserted sources, target anchors, missing metadata, and unresolved ambiguity.

AcNote citations are managed Word controls. Do not convert them to plain text, manually type citation numbers, or manually edit the generated bibliography.
