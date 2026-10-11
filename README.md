# AcNote

参考文献排版工具。文献库里管理条目和学校版式，Word 功能区显示 AcNote，可以插入引用并按当前版式整篇更新。

中文文献超过三人用「等」，英文文献用 et al.，按题名文字或语种判断，不用另建一套作者字段。版式缺了卷号、页码这类内容时，只在文献库里提示，不写进 Word。

## 运行

需要 [.NET 9 桌面运行时](https://dotnet.microsoft.com/download/dotnet/9.0)。

```powershell
dotnet run --project src/AcNote.App
```

第一次运行会登记 Word 插件。关掉已经打开的 Word，再打开时功能区会出现 **AcNote**，里面是：插入引用、编辑引用、更新引用、版式。

主程序和插件共用同一份文献库。插件随主程序一起登记，不需要单独安装。

## 版式

内置 `styles/gb-t-7714-2015-numeric.style`。学校版式用同名 `id` 覆盖，例如 `SYSU.style` 继承国标，核对写作指南后再把 `status` 改成 `verified`。

导入版式，或把 `.style` 文件放到 `%AppData%\AcNote\styles`。后放入的同名 `id` 会盖过软件自带版式。

## 导入 EndNote

可以直接选择 `.enl` 或 `.enlx`。支持 EndNote X9.3 及以后的库，读取时不会改原文件。更早的库请在 EndNote 里导出 EndNote XML 再导入。
