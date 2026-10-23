# FuturesBar 0.2.1 · Windows 悬浮行情条预览版

## 下载与安装

下载 `FuturesBar-0.2.1-win-x64-preview.zip`，完整解压到固定文件夹，双击 `FuturesBar.exe`。

- 适用于 Windows 11 x64，已包含运行时，无需另装 .NET。
- 这是未签名预览版，尚未完成 Windows 实机界面验收。
- 需要网络能够访问 Binance 行情服务；代理默认跟随系统，也可设置自己的本机 HTTP / 混合代理端口。

## 本版改进

- 支持 Binance TradFi USDT 永续合约，包括 KORU、SPCX（SpaceX）、TSLA、NVDA 等。
- 支持搜索 SpaceX / SPCX 和 KORU / 韩国，并与加密货币永续混合选择，最多三个品种。
- 通过 BTC、KORU、SPCX 的最新成交价及标记价真实行情验证。

## 功能

- 悬浮条最多显示三个 Binance USDT 永续合约，支持搜索、更换和排序。
- 最新成交价、标记价格切换；行情每秒汇总刷新，断线自动重连。
- 拖动、锁定、置顶和透明度；默认不抢键盘焦点。
- 设置面板、系统托盘、显示 / 隐藏和当前用户登录后启动。
- 不需要 Binance 账号或 API Key，只读取公开行情，无账户查询或交易功能。

## 验证状态

已完成 Windows x64 交叉构建、8 项核心检查，以及 Binance 品种列表和 BTC / ETH / SOL 两类真实行情检查。

拖动、多屏缩放、位置恢复、托盘、开机启动和睡眠恢复仍需在 Windows 实机上验收。

## 源码与校验

`FuturesBar-0.2.1-source-preview.zip` 包含 Mac 和 Windows 源码，采用 MIT 许可证。安装包与源码包的 SHA-256 校验值见 `FuturesBar-0.2.1-preview-SHA256SUMS.txt`。

Mac 用户请使用 [v0.1.2 的 DMG 安装包](https://github.com/LuoNova94/FuturesBar/releases/tag/v0.2.1)。本次 Release 的 ZIP 为 Windows 预览版。
