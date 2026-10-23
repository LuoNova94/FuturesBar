# FuturesBar · Windows 悬浮行情条

[返回项目首页](../README.md) · [macOS 文档](mac.md)

在 Windows 桌面以悬浮行情条查看 Binance USDT 永续合约价格，最多显示三个品种。

## 下载与安装

**[下载 Windows 11 x64 预览版（ZIP · v0.2.1）](https://github.com/LuoNova94/FuturesBar/releases/download/v0.2.1/FuturesBar-0.2.1-win-x64-preview.zip)**

将 ZIP 完整解压到固定文件夹，双击 `FuturesBar.exe`，无需另装 .NET。使用说明见 [INSTALL.txt](../windows/INSTALL.txt)，版本说明、源码包与 SHA-256 校验文件见 [Windows 预览版 Release](https://github.com/LuoNova94/FuturesBar/releases/tag/v0.2.1)。

此版本为未签名预览版；已有实机运行截图，新版完整界面验收仍待完成。

## 实机效果

![Windows 悬浮行情条实机效果](screenshots/windows-floating-bar.png)

截图由项目作者提供，展示三个品种的最新成交价和涨跌幅。价格仅为截图时的示例。

## 使用

1. 解压后运行 `FuturesBar.exe`，行情条默认显示 BTC、ETH、SOL。
2. 点击齿轮、双击或右键行情条打开设置，搜索并选择 1–3 个品种，可调整顺序和价格类型。
3. 拖动行情区域移动位置，在设置中调整锁定、置顶和透明度。
4. 点击 × 隐藏行情条；托盘图标可显示或隐藏，托盘右键菜单可重新连接或退出。
5. 如需代理，在设置中填写自己的本机 HTTP / 混合代理端口；默认跟随系统网络。
6. 启用登录后启动前，将程序放在固定文件夹；移动程序后需重新设置。升级前先从托盘退出旧版。

## 股票与 ETF 合约

支持 Binance 交易中的 TradFi USDT 永续合约。搜索 `KORU`、`SpaceX`（实际合约代码 `SPCXUSDT`）、`TSLA`、`NVDA` 等即可添加；可与 BTC 等品种混合选择，合计最多三个。价格来自 Binance 合约行情，单位为 USDT。

## 功能

- 单行悬浮条，最多三个 Binance USDT 永续合约；价格每秒汇总刷新。
- 拖动、锁定、置顶、透明度；默认不抢键盘焦点。
- 齿轮、双击或右键打开设置；托盘控制显示、隐藏、退出。
- 品种搜索、排序、价格类型、本机 HTTP / 混合代理、当前用户登录后启动。
- 设置保存在 `%LOCALAPPDATA%\FuturesBar\settings.json`。
- 代理默认跟随系统，不包含任何个人网络或账号配置。
- 使用 WebSocket PING/PONG 心跳判定连接健康，安静行情不会触发重连；过期价格以「—」显示。

## 构建

Windows 源码与检查位于 `windows/`，与 `macos/` 并列。使用 C# / .NET 10 / WPF，托盘通过 Windows Forms NotifyIcon 实现，无浏览器运行时或第三方 NuGet 库。

推荐使用 Windows 11 x64 和 .NET 10 SDK。以下命令均在仓库根目录执行：

```powershell
powershell -File windows/build.ps1
```

输出为 `output/windows/win-x64/FuturesBar.exe`，ZIP 为 `output/FuturesBar-0.2.1-win-x64-preview.zip`。发布时包含运行时，用户不需要另装 .NET。建议先解压到固定目录再启用登录后启动。

命令行检查与单独构建：

```sh
dotnet run --project windows/FuturesBar.Checks -c Release
dotnet publish windows/FuturesBar.Windows -c Release -r win-x64 --self-contained true -o output/windows/win-x64
```

可用 `FUTURES_PROXY_PORT` 环境变量指定本机代理，并为检查程序加 `--live` 验证两个行情频道。该变量仅用于检查，不会写入应用默认设置。

## 验证状态

已完成 Windows x64 交叉构建和 8 项核心检查；原有 BTC / ETH / SOL 行情与新增 BTC / KORU / SPCX 混合订阅的最新成交价、标记价格真实推送检查均通过。

Windows UI 需要在 Windows 实机上验证拖动、多屏缩放、位置恢复、托盘、开机启动和睡眠恢复。跨平台编译成功与行情检查通过，不等于这些 UI 行为已通过验收。预览包尚未签名。

## 许可证

本项目使用 [MIT License](../LICENSE)。

## 行情契约

- 品种：`GET https://fapi.binance.com/fapi/v1/exchangeInfo`
- 最新成交价：`wss://fstream.binance.com/market/stream?streams=btcusdt@ticker/...`，字段 `c` 为价格、`P` 为百分比涨跌。
- 标记价格：同一路由，stream 为 `btcusdt@markPrice@1s`，字段 `p` 为价格。
- 校验事件类型与事件时间；收到过期、错序或其他品种数据时不显示为实时行情。
- 官方说明：[WebSocket 连接](https://developers.binance.com/en/docs/products/derivatives-trading-usds-futures/websocket-market-streams/Connect)、[行情字段](https://developers.binance.com/en/docs/catalog/core-trading-derivatives-trading-usd-s-m-futures/api/ws-streams/market)、[品种列表](https://developers.binance.com/en/docs/catalog/core-trading-derivatives-trading-usd-s-m-futures/api/rest-api/market-data)。

