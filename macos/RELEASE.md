# 合约行情 0.1.3 · Apple 芯片试用版

轻量的 macOS 菜单栏工具，查看 Binance USDT 永续合约行情。

## 下载与安装

下载 `FuturesBar-0.1.3-arm64-unnotarized.dmg`，打开后将「合约行情.app」拖入「Applications / 应用程序」。

- 系统要求：Apple 芯片 Mac（M1 及更新芯片），macOS 13 或更新版本。
- 此版本暂不支持 Intel Mac。
- 此安装包使用本机临时签名，尚未取得 Apple Developer ID 签名及 Apple 公证。
- 首次打开可能遇到系统拦截。确认来源可信后，按 [Apple 官方说明](https://support.apple.com/102445) 在「系统设置 → 隐私与安全性」中确认打开对应应用。

## 功能

- 菜单栏最多显示 3 个品种，支持搜索、更换和排序。
- 可切换最新成交价与标记价格。
- 默认跟随系统网络，可手动配置本机 HTTP / 混合代理端口。
- 支持断线重连、Mac 唤醒恢复和开机启动。
- 不需要 Binance 账号或 API Key，无账户查询或交易功能。

## 本版改进

- 支持 Binance 的 TradFi USDT 永续合约，包括 KORU、SPCX（SpaceX）、TSLA、NVDA 等当前处于交易状态的品种。
- 搜索 SpaceX 或 SPCX 都能找到 SPCXUSDT；搜索 KORU 或韩国可找到 KORUUSDT。
- 可与 BTC 等普通永续混合选择，仍最多显示三个品种。
- 接入的是 Binance 合约行情，价格单位为 USDT；不包含 USD1、USDC 或交割合约。

## 源码与校验

源代码以 MIT 许可证发布。安装包和源码归档的 SHA-256 校验值见 `SHA256SUMS.txt`。

需要本机网络能够访问 Binance 行情服务；设置、代理软件和网络费用由使用者自行管理。
