# 合约行情 · FuturesBar

轻量的 Binance 合约行情工具，支持普通永续及股票、ETF 等 TradFi USDT 永续合约，最多显示三个品种。无需 Binance 账号或 API Key。

## 选择你的平台

| 平台 | 下载 | 独立使用文档 | 系统要求 |
| --- | --- | --- | --- |
| **macOS 菜单栏版** | [下载 DMG · 0.1.3](https://github.com/LuoNova94/FuturesBar/releases/download/v0.2.1/FuturesBar-0.1.3-arm64-unnotarized.dmg) | **[Mac 安装、使用与构建](docs/mac.md)** | Apple 芯片，macOS 13+ |
| **Windows 悬浮行情条** | [下载 ZIP · 0.2.1 预览版](https://github.com/LuoNova94/FuturesBar/releases/download/v0.2.1/FuturesBar-0.2.1-win-x64-preview.zip) | **[Windows 安装、使用与构建](docs/windows.md)** | Windows 11 x64，无需另装 .NET |

Mac 安装包尚未公证，Windows 安装包为未签名预览版，首次打开和验证状态见各平台文档。

[本次发布说明、源码包与校验文件](https://github.com/LuoNova94/FuturesBar/releases/tag/v0.2.1) · [所有版本](https://github.com/LuoNova94/FuturesBar/releases)

## 功能

- 最多选择三个品种，支持搜索、更换与排序。
- 支持最新成交价和标记价格。
- 支持 `KORU`、`SPCX / SpaceX`、`TSLA`、`NVDA` 等当前可交易的 TradFi USDT 永续，可与 BTC 等混合显示。
- 默认跟随系统网络，也可配置本机 HTTP / 混合代理；需要网络能够访问 Binance 行情服务。

## 界面预览

**macOS 菜单栏版**

![Mac 菜单栏实时行情](docs/screenshots/mac-menubar.png)

[查看 Mac 完整面板截图与说明](docs/mac.md#界面预览)

**Windows 悬浮行情条（实机截图）**

![Windows 悬浮行情条实机效果](docs/screenshots/windows-floating-bar.png)

[查看 Windows 设置与使用说明](docs/windows.md)

截图中的价格仅为示例。两端均只读取公开行情，无账户查询或交易功能。

## 许可证

[MIT License](LICENSE)
