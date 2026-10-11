<div align="center">

# AcNote

### Citations, styled for the manuscript.

A reference manager organized around citation styles: Chinese and English author lists, shared style files, and citations that can be updated in Word.

[简体中文](README.zh-CN.md) · [What it does](#what-it-does-now) · [How it is put together](#how-it-is-put-together) · [Roadmap](#roadmap) · [Contributing](#contributing)

![Status](https://img.shields.io/badge/status-early%20prototype-orange?style=flat-square)
![Focus](https://img.shields.io/badge/focus-citation%20formatting-3949ab?style=flat-square)
![Languages](https://img.shields.io/badge/languages-English%20%2B%20Chinese-00897b?style=flat-square)

</div>

> [!NOTE]
> **Early prototype.** You can build it from source. There is no installer yet. The library, GB/T 7714 numeric formatting, Chinese/English author lists, style files, EndNote import, and a Word ribbon are in this repository. The visual style editor, BibTeX/RIS import, and tested WPS support are not. Anything described as planned below is not a promise that it already works.

## Why this project

Collecting papers is a solved problem in several existing tools. Formatting the bibliography for a thesis or a journal is still awkward, especially when one document mixes Chinese and English:

- Chinese author lists often need **等**, and English lists need **et al.** Doing that with an extra field on every record is tedious.
- A school style is usually a small change to a national standard. Editing a long stylesheet for a comma or an author cutoff is more work than the change itself.
- Missing volume, issue, or pages shows up as a broken reference, and it is easy to miss until submission.
- After the citations are in Word, changing the style or adding a paper should update the document, not leave a block of static text.

AcNote keeps the bibliographic record as a description of the source. The style file decides how that record is printed.

## What it does now

### Chinese and English author lists

The language comes from the record when it is set, otherwise from the title (Han characters or Latin letters). You can override it per record. Author names stay in one field.

| Record language | Example |
| --- | --- |
| Chinese | 张三, 李四, 王五, 等. … |
| English | Smith J, Johnson A, Brown R, et al. … |

The default GB/T 7714-2015 numeric style lists every author when there are three or fewer, and the first three plus 等 / et al. when there are more. A style file can change the cutoff and the suffix, including whether a comma appears before 等.

Names are not rewritten in the library to force this. Chinese names stay as entered. Western names stored as `Smith, John` are printed as `Smith J`.

### Style files

A style is a JSON file with a `.style` extension. One style can `extend` another and override only the rules that differ. A file in the user style folder with the same `id` replaces the built-in one, which is the intended way to share a school style.

Included now:

- `styles/gb-t-7714-2015-numeric.style` — sequential numbering, based on GB/T 7714-2015
- `styles/SYSU.style` — a draft that only inherits that style. It is **not** checked against a Sun Yat-sen University handbook. Set `"status": "verified"` after that check.

There is no visual style editor yet. The file is the editor.

```json
{
  "id": "sysu-thesis",
  "name": "中山大学学位论文",
  "extends": "gb-t-7714-2015-numeric",
  "status": "draft",
  "etAl": { "minCount": 4, "useFirst": 3, "zh": ", 等", "en": ", et al." }
}
```

### Missing fields, in the library only

Required fields come from the active style and the reference type. A journal article can be missing its volume while a book is missing its publisher. The library shows that list. The Word document does not: empty fields are omitted, and the gap is not written into the manuscript.

The checker does not yet separate “missing”, “not applicable”, and “please confirm”. An article number with no page range is still treated as a missing page when the style requires pages.

### Word

Running AcNote registers its Word add-in in the current user account. Close Word and open it again. The ribbon tab is **AcNote**, with 插入引用, 编辑引用, 更新引用, and 版式.

The desktop program and the add-in share one library. Insert stores the record id in the document and refreshes the visible citation and bibliography from the current style. This path is implemented and covered by formatting tests; it still needs more trials on real thesis files.

WPS is not supported yet. A registry value is written so a later build can look for the same add-in. That has not been tested, and it should not be treated as WPS support.

### Import

EndNote X9.3 and later `.enl` libraries, compressed `.enlx` libraries, and EndNote XML can be imported. The original library is only read. Older EndNote libraries need an EndNote XML export.

BibTeX, RIS, and CSL JSON are not imported yet.

## How it is put together

The formatter does not live inside the Word add-in. The desktop program owns the library and the style files. Word asks it to pick records and then writes the formatted text back into the document.

```mermaid
flowchart TD
    A[EndNote library or XML] --> B[Reference library]
    B --> C[Language and author formatting]
    C --> D[Style file]
    D --> E[Bibliography text]
    D --> F[Missing fields in the library]
    E --> G[Word ribbon]
```

## Who it is for

People formatting a thesis or a paper against a specific style, especially when the bibliography mixes Chinese and English. It is not a PDF reader, and it is not trying to cover search, recommendations, or annotation.

## Roadmap

Done in this repository, with the limits noted above:

- [x] Reference library, GB/T 7714-2015 numeric output, and Chinese/English author rules
- [x] `.style` files with inheritance and override
- [x] Style-based missing-field list in the library
- [x] EndNote `.enl`, `.enlx`, and XML import
- [x] Word ribbon registered by the desktop program

Not done:

- [ ] Visual style editor
- [ ] BibTeX, RIS, and CSL JSON import
- [ ] Clearer handling of article numbers versus page ranges
- [ ] WPS tested against specific versions
- [ ] An installer and a versioned release

A useful next checkpoint is a thesis file that can be saved, closed, reopened, and refreshed after a style change.

## Running it

Install the [.NET 9 desktop runtime](https://dotnet.microsoft.com/download/dotnet/9.0), then:

```powershell
dotnet run --project src/AcNote.App
```

The first launch copies built-in styles into `%AppData%\AcNote\styles` and registers the Word ribbon. Restart Word after that. Sample records are added only when the library database is created.

## Contributing

Style files are the most useful contribution. A school style should say which handbook it follows, and it should not be marked `verified` until the formatted samples match that handbook.

Bug reports are most useful with the style file, the record, and the expected string. Word issues should include the Word version and whether the document was saved and reopened.

## Design choices

1. The stored record describes the source. The style file describes the printed form.
2. Missing fields are shown in the library, not inserted into the manuscript.
3. A school style should be a small file that extends a base style, so a shared correction does not have to be copied into every school file.
4. Word keeps the record id, so a later refresh can change the printed text.

## License

No license file is in the repository yet. Publishing this code without a `LICENSE` does not by itself grant reuse rights. A license will be added when one is chosen.

<div align="center">

[简体中文](README.zh-CN.md)

</div>
