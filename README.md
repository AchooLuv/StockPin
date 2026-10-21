# StockPin

**轻量、透明、始终置顶的桌面股票盯盘小工具** · A股 / 港股 / 美股 · Windows 10 / 11

[![Platform](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011-5b6ea8?style=flat-square)](../../releases/latest)
[![.NET](https://img.shields.io/badge/.NET-8.0-5b6ea8?style=flat-square)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-MIT-5b6ea8?style=flat-square)](LICENSE)

![StockPin](docs/hero.png)

## 功能

- **一屏看清**：股票名、代码、现价、涨跌幅；`A` / `港` / `美` 标签区分市场。
- **透明置顶小面板**：无边框圆角卡片，可拖动、松手自动贴边、位置自动记住，始终浮在其他窗口之上。
- **休市自动置灰**：按各市场交易时段判断，非交易时间整行变灰，价格保留最后成交价。
- **模拟持仓**：为自选股填写数量与成本价，面板顶部实时显示持仓市值、总盈亏、今日盈亏、今日涨跌幅。
- **多币种折算**：港股、美股持仓按实时汇率折算为人民币，混合持仓也能算总账。
- **添加 / 移除 / 排序**：输入代码或中文名即可添加（`601872`、`00700`、`FRO`、`招商轮船`），支持拖动排序和右键菜单。
- **4 套主题**：雾灰、蓝调、深海、暮紫；界面配色可换，涨跌始终红涨绿跌。
- **省资源**：原生 WPF，不内嵌浏览器；一次请求拉取全部行情；全市场休市自动降到 60 秒刷新，面板隐藏时暂停轮询。
- **基础设置**：开机自启、刷新频率、背景不透明度、窗口置顶、锁定位置、托盘常驻。

## 下载

到 [Releases](../../releases/latest) 下载 **StockPin.exe**，双击即用：

- Windows 10 / 11 x64，单文件，无需安装 .NET 运行时
- 免安装，配置保存在 `%APPDATA%\StockPin\config.json`
- 若首次运行出现 SmartScreen 提示，点「更多信息 → 仍要运行」

## 使用

1. 双击运行，面板默认停在屏幕右上角。
2. 鼠标移入面板，右上角出现 **刷新 / 添加 / 设置 / 隐藏** 按钮。
3. **右键面板**：添加股票、设置、锁定位置、窗口置顶、隐藏、退出。
4. **设置 → 自选股票**：输入代码或名称添加；在右侧填写数量、成本价即可看到模拟持仓。
5. 点 **×** 只是收进托盘；托盘图标右键可再次显示面板或退出。

![设置](docs/settings.png)

## 主题

四套配色只改变界面与面板底色，涨跌颜色始终是红涨绿跌。

![主题](docs/appearance.png)

## 数据来源

行情来自东方财富公开 HTTP 接口，免费、无需密钥，数据可能有延迟。
本项目仅用于个人盯盘与学习，**不构成任何投资建议**。

## 从源码构建

需要 .NET 8 SDK：

```bash
dotnet publish -c Release -r win-x64 --self-contained true -o dist
```

产物为 `dist/StockPin.exe`（单文件，内含运行时）。

## 项目结构

```
Views/        面板与设置窗口
ViewModels/   状态、轮询、持仓计算
Services/     行情接口、配置、开机自启
Core/         主题与数据模型
tools/        图标、截图生成脚本
```

## License

[MIT](LICENSE)
