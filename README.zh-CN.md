<div align="center">

# AcNote

<img src="src/AcNote.App/Assets/acnote.png" width="88" alt="AcNote logo"/>

### 按稿件要求排参考文献。

用来管理文献、套用可共享的版式，并在 Word 里更新引用。中文作者列表用「等」，英文用 et al.，不另建一套作者字段。

[GitHub 项目主页](https://github.com/DLWangSan/acnote) · [下载与更新](https://github.com/DLWangSan/acnote/releases) · [English](README.md) · [能做什么](#能做什么) · [文献库和版式](#文献库和版式) · [运行](#运行)

![Focus](https://img.shields.io/badge/focus-citation%20formatting-3949ab?style=flat-square)
![Languages](https://img.shields.io/badge/languages-English%20%2B%20Chinese-00897b?style=flat-square)

</div>

## 为什么做

收集和阅读论文，现有工具已经够用。难的是按学校或期刊把参考文献排出来，尤其是一篇稿子里中英文都有：

- 中文要「等」，英文要 et al.。为了这一处显示去给每条文献加冗余字段，很烦。
- 学校版式往往只改国标的几处。为了一个逗号或作者人数去改一大段样式，不值当。
- 缺卷号、期号、页码时，生成结果只是空一截，交稿前不容易发现。
- 引用进 Word 之后，换版式或增删文献，应该能更新，而不是留下一段不能再改的文字。

文献记录负责把出处记清楚。印成什么样，由版式文件决定。

## 能做什么

### 中英文作者列表

语种优先用条目上的设置；没设时看题名里是汉字多还是拉丁字母多。每条文献可以改成中文或 English。作者只存一份。

| 文献语种 | 示例 |
| --- | --- |
| 中文 | 张三, 李四, 王五, 等. … |
| 英文 | Smith J, Johnson A, Brown R, et al. … |

GB/T 7714：三人及以下全部列出，超过三人列前三位，再加「等」或 et al.。版式文件可以改人数，也可以改「等」前面要不要逗号。

中文名按录入原样输出。英文名若是 `Smith, John`，印成 `Smith J`。

Author-Year 的文中引用用姓：`(袁隆平, 1966)`、`(Watson and Crick, 1953)`。文中和文末的人数分开写在版式里。内置文中规则是三人及以上只留第一作者，再加「等」或 et al.。

### 文献类型

期刊论文、图书、学位论文、图书章节、会议论文、数据集。数据集保留网址。新建文献库时可以放入这些已发表的例子：袁隆平 1966、青蒿素结构论文 1979、Watson 与 Crick 1953、王国维《人间词话》、图灵 1938 年学位论文、Vaswani 等 2017、MNIST 数据集。

### Word

运行 AcNote 后，会在当前 Windows 用户下登记 Word 插件。**保存文档，退出所有 Word 窗口后重新打开**，即可加载新版功能区。插件按版本安装，不覆盖 Word 正在使用的旧文件。

1. 在主程序列表中勾选文献；支持复选框、Ctrl / Shift 多选。
2. 在 Word 正文中放好光标，点击 **AcNote → 插入已选文献**。直接使用主程序的选择，不再弹出选择列表。
3. 没有提前选文献时，使用旁边的 **查找并插入**。
4. 光标放在引用中，可 **编辑引用** 或 **移除此处引用**；移除后重新编号。
5. **更新引用和列表** 刷新整篇文档；版式下拉框用于切换格式。
6. **字体、段落与版式** 设置文中及文末字体、字号、上标，以及文末行距、段前段后、悬挂缩进和对齐。

参考文献列表在独立段落生成，与正文之间保留空行。插入后光标移出引用控件，便于继续写正文。删除最后一处引用后，更新会清理对应列表。

文档记录使用的文献库路径和版式。切换主程序文献库后，插件更新仍读取文档绑定的库。缺失条目时会停止更新，保留原有引用文字；移动文献库后需要恢复绑定路径。著者—出版年模式会在当前文档内统一生成 `2020a / 2020b`，文中与文末使用同一后缀，原始年份不改。

主程序快捷键：`Ctrl+2` 插入已选文献，`Ctrl+N` 新建文献，`Ctrl+S` 保存。右侧编辑当前条目，底部预览所选文献；预览中的年份消歧以当前选择为范围，Word 中以整篇文档为范围。

插件使用 Word 与 WPS 都支持的 CustomUI 基础命名空间，并登记 WPS COM 加载项白名单。安装或更新后，请完全退出并重开 WPS Writer；若仍未显示，请在 WPS 的 COM 加载项管理中检查 AcNote 是否启用。WPS 不同版本对第三方 COM 加载项的策略可能不同，本版本尚未在你的 WPS 环境中实测。

### 导入

可以导入 EndNote X9.3 及以后的 `.enl`、压缩的 `.enlx`，以及 EndNote XML。只读，不改原来的库。更早的 EndNote 库请先导出 XML。

## 文献库和版式

文献库是一个 `.acnote` 文件。**新建文献库** 建一份，**打开文献库** 换成另一份，下次启动会打开上次那份。同一个人可以分开做学位论文和期刊稿。文献加在当前打开的文件里。

缺哪些字段，由当前版式和文献类型决定，只列在文献库里。Word 里空字段会连同旁边多出来的标点一起省掉。

版式是扩展名为 `.style` 的 JSON。一个版式可以 `extends` 另一个，只写下不同的规则。**编辑版式** 打开当前这份；保存后写到 `%AppData%\AcNote\styles`，同 `id` 的文件会盖过软件自带的那份。**导入版式** 也是这样安装。

版式列表先是两套经典，后面是大学名：

- `GB/T 7714`：顺序编码
- `Author-Year`：著者-出版年，继承 GB/T 7714
- `中山大学`、`北京大学`、`清华大学`：各自继承 GB/T 7714，当前为待校验模板，并非已核对各校规范

```json
{
  "id": "sysu",
  "name": "中山大学",
  "extends": "gb-t-7714-2015-numeric",
  "group": "university"
}
```

## 程序怎么分

排版不写在 Word 插件里。打开的文献库和版式在主程序中。Word 负责选文献，再把排好的文字写回文档。

```mermaid
flowchart TD
    A[EndNote 库或 XML] --> B[.acnote 文献库]
    B --> C[语种与作者]
    C --> D[版式文件]
    D --> E[参考文献正文]
    D --> F[文献库中的缺项]
    E --> G[Word 功能区]
```

## 给谁用

在按某一套版式写学位论文或期刊稿，而且参考文献里中英文都有的时候用。不做 PDF 阅读，也不做检索、推荐和批注。

## 运行

普通用户请从 [GitHub Releases](https://github.com/DLWangSan/acnote/releases) 下载 Windows x64 安装包。安装程序会先检查 .NET 10 Desktop Runtime；缺少时会提示并启动微软签名的 Runtime 安装程序，完成后继续安装 AcNote。Release 也会单独附上 Runtime 安装程序及 SHA-256 校验文件，方便离线部署。当前版本要求 64 位 Windows。

从源码运行请安装 [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)，然后：

```powershell
dotnet run --project src/AcNote.App
```

第一次运行会把自带版式复制到 `%AppData%\AcNote\styles`，并登记 Word/WPS 功能区。之后请重启对应的文字处理程序。在工具栏里新建或打开 `.acnote` 文献库。

## 参与

最有用的是版式文件。学校版式继承 GB/T 7714，名字用大学名。报告问题时，请带上版式文件、文献记录和期望的著录字符串。

## 几个约定

1. 库存的是出处，版式文件决定印出来的样子。
2. 文献库是文件，不同的稿子可以分开。
3. 缺项只显示在文献库，不写进稿子。
4. 学校版式尽量只写和基础版式不同的几行。
5. Word 里保存的是文献编号，以后刷新可以改显示文字。

<div align="center">

[English](README.md)

</div>

## 开发验证

```powershell
dotnet build AcNote.sln -c Release
dotnet test AcNote.sln -c Release
```

安装了桌面 Word 的 Windows 环境还可手动运行 `dotnet run --project tests/AcNote.Word.Smoke`。它创建自己的临时文档，验证分段、重复刷新、消歧、字体段落、多篇插入和引用移除；不属于默认单元测试。功能区点击和 WPS 兼容性需要另行验证。操作验收清单见 [TESTING.md](TESTING.md)。
