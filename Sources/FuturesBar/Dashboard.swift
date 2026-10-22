import AppKit
import FuturesCore
import SwiftUI

private let accent = Color(red: 0.95, green: 0.76, blue: 0.25)

struct Dashboard: View {
    @ObservedObject var store: MarketStore
    @State private var managing = false
    @State private var query = ""
    @State private var networkSettings = false
    @State private var proxyEnabled = false
    @State private var proxyPortText = ""

    private var results: [Contract] {
        let term = query.trimmingCharacters(in: .whitespacesAndNewlines).uppercased()
        let matching = store.contracts.filter { term.isEmpty || $0.symbol.contains(term) || $0.baseAsset.contains(term) }
        let favorites = ["BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "DOGEUSDT", "XRPUSDT"]
        return matching.sorted {
            let l = favorites.firstIndex(of: $0.symbol) ?? 999
            let r = favorites.firstIndex(of: $1.symbol) ?? 999
            return l == r ? $0.symbol < $1.symbol : l < r
        }
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 16) {
            HStack(spacing: 10) {
                Image(systemName: "waveform.path")
                    .font(.system(size: 19, weight: .bold)).foregroundStyle(accent)
                    .frame(width: 38, height: 38)
                    .background(accent.opacity(0.12), in: RoundedRectangle(cornerRadius: 11))
                VStack(alignment: .leading, spacing: 3) {
                    Text("合约行情").font(.system(size: 17, weight: .semibold))
                    Text("BINANCE  /  USDT 永续").font(.system(size: 10, weight: .medium)).foregroundStyle(.secondary)
                }
                Spacer()
                HStack(spacing: 5) {
                    Circle().fill(store.allFresh ? Color.green : Color.orange).frame(width: 5, height: 5)
                    Text(store.allFresh ? "LIVE" : "连接中").font(.system(size: 10, weight: .semibold))
                }.foregroundStyle(store.allFresh ? .green : .orange)
            }

            Picker("价格类型", selection: $store.kind) {
                ForEach(PriceKind.allCases, id: \.self) { Text($0.title).tag($0) }
            }.pickerStyle(.segmented)

            VStack(spacing: 8) {
                ForEach(Array(store.watchlist.symbols.enumerated()), id: \.element) { index, symbol in
                    quoteCard(symbol, index: index)
                }
            }

            HStack {
                Text("菜单栏品种").font(.system(size: 12, weight: .medium))
                Text("\(store.watchlist.symbols.count) / 3").font(.system(size: 11, design: .monospaced)).foregroundStyle(.secondary)
                Spacer()
                Button(managing ? "完成" : "管理品种") {
                    managing.toggle()
                    if managing { networkSettings = false }
                    query = ""
                }
                    .buttonStyle(.plain).foregroundStyle(accent)
            }

            if managing { selectionPanel }

            Divider().overlay(.white.opacity(0.04))
            VStack(spacing: 10) {
                Toggle("菜单栏紧凑价格", isOn: $store.compact)
                Toggle("开机启动", isOn: Binding(get: { store.loginEnabled }, set: { store.setLogin($0) }))
            }.toggleStyle(.switch).controlSize(.mini).font(.system(size: 11))

            Button {
                proxyEnabled = store.useProxy
                proxyPortText = store.proxyPort.map(String.init) ?? ""
                networkSettings.toggle()
                if networkSettings { managing = false }
            } label: {
                HStack {
                    Image(systemName: "network")
                    Text(store.useProxy ? "本机代理 · 127.0.0.1:\(store.proxyPort.map(String.init) ?? "")" : "网络 · 跟随系统")
                    Spacer()
                    Image(systemName: networkSettings ? "chevron.up" : "chevron.down")
                }.font(.system(size: 10))
            }.buttonStyle(.plain).foregroundStyle(.secondary)

            if networkSettings {
                VStack(alignment: .leading, spacing: 10) {
                    Text("默认跟随 macOS 系统网络设置；需要时可配置本机 HTTP / 混合代理。")
                        .font(.system(size: 10)).foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                    Toggle("手动配置本机代理", isOn: $proxyEnabled)
                        .toggleStyle(.switch).controlSize(.mini).font(.system(size: 11))
                    if proxyEnabled {
                        HStack {
                            Text("127.0.0.1 :").font(.system(size: 11, design: .monospaced))
                            TextField("端口", text: $proxyPortText).textFieldStyle(.roundedBorder)
                                .frame(width: 65).accessibilityLabel("本机代理端口")
                        }
                    }
                    HStack {
                        Spacer()
                        Button("保存并重连") {
                            store.applyProxy(enabled: proxyEnabled, portText: proxyPortText)
                            if store.proxyMessage == nil { networkSettings = false }
                        }.controlSize(.small)
                    }
                    if let message = store.proxyMessage { Text(message).font(.caption).foregroundStyle(.orange) }
                }
            }

            if let message = store.loginMessage {
                Text(message).font(.caption).foregroundStyle(.orange).fixedSize(horizontal: false, vertical: true)
            }

            HStack(spacing: 8) {
                Text(store.allFresh ? "每秒更新 · 价格单位 USDT" : store.status)
                    .font(.system(size: 10)).foregroundStyle(.secondary).lineLimit(1)
                Spacer(minLength: 0)
                Button { store.restart(); if store.contracts.isEmpty { store.loadContracts() } } label: {
                    Image(systemName: "arrow.clockwise")
                }.help("重新连接行情").accessibilityLabel("重新连接行情")
                Button { NSApp.terminate(nil) } label: { Image(systemName: "power") }
                    .help("退出合约行情").accessibilityLabel("退出合约行情")
            }.buttonStyle(.plain).foregroundStyle(.secondary)
        }
        .padding(20)
        .frame(width: 370)
        .fixedSize(horizontal: false, vertical: true)
        .background(Color(red: 0.075, green: 0.08, blue: 0.10))
        .preferredColorScheme(.dark)
        .tint(accent)
        #if DEBUG
        .onReceive(NotificationCenter.default.publisher(for: Notification.Name("FuturesBar.CheckLayout"))) { event in
            managing = event.userInfo?["managing"] as? Bool ?? false
            networkSettings = event.userInfo?["network"] as? Bool ?? false
        }
        #endif
    }

    private func quoteCard(_ symbol: String, index: Int) -> some View {
        let contract = store.contract(symbol)
        let quote = store.quotes[symbol]
        let fresh = quote?.isFresh(at: store.now) == true
        return VStack(alignment: .leading, spacing: 8) {
            HStack(alignment: .firstTextBaseline) {
                Text(contract.baseAsset).font(.system(size: 13, weight: .semibold))
                Text("/ USDT").font(.system(size: 10)).foregroundStyle(.secondary)
                Spacer()
                if managing {
                    HStack(spacing: 10) {
                        Button { store.move(symbol, by: -1) } label: { Image(systemName: "chevron.up") }
                            .disabled(index == 0).help("向前移动 \(contract.baseAsset)")
                        Button { store.move(symbol, by: 1) } label: { Image(systemName: "chevron.down") }
                            .disabled(index == store.watchlist.symbols.count - 1).help("向后移动 \(contract.baseAsset)")
                        Button { store.toggle(symbol) } label: { Image(systemName: "minus.circle") }
                            .disabled(store.watchlist.symbols.count == 1).help("移除 \(contract.baseAsset)")
                    }.font(.system(size: 11)).buttonStyle(.plain).foregroundStyle(.secondary)
                } else if let change = quote?.changePercent, fresh {
                    let value = NSDecimalNumber(decimal: change).doubleValue
                    Text(String(format: "%+.2f%%", value))
                        .font(.system(size: 11, weight: .medium, design: .monospaced))
                        .foregroundStyle(value >= 0 ? Color.green : Color.red)
                } else {
                    Text(store.kind == .mark ? "标记价" : "24h").font(.system(size: 10)).foregroundStyle(.secondary)
                }
            }
            HStack(alignment: .firstTextBaseline) {
                Text(fresh ? PriceText.format(quote!.price, compact: false) : "—")
                    .font(.system(size: 24, weight: .medium, design: .rounded)).monospacedDigit()
                    .foregroundStyle(fresh ? Color.primary : Color.secondary)
                Spacer()
                if let quote, fresh {
                    Text(quote.eventTime, style: .time).font(.system(size: 9, design: .monospaced)).foregroundStyle(.tertiary)
                } else {
                    Text(quote == nil ? "等待行情" : "行情已过期")
                        .font(.system(size: 10)).foregroundStyle(.orange)
                }
            }
        }
        .padding(13)
        .background(.white.opacity(0.045), in: RoundedRectangle(cornerRadius: 12))
        .overlay(RoundedRectangle(cornerRadius: 12).strokeBorder(.white.opacity(0.05)))
    }

    private var selectionPanel: some View {
        VStack(alignment: .leading, spacing: 9) {
            HStack(spacing: 7) {
                Image(systemName: "magnifyingglass").foregroundStyle(.secondary)
                TextField("搜索合约，如 BTC、DOGE", text: $query).textFieldStyle(.plain)
                    .accessibilityLabel("搜索合约品种")
                if !query.isEmpty { Button { query = "" } label: { Image(systemName: "xmark.circle.fill") }.buttonStyle(.plain) }
            }.padding(9).background(.white.opacity(0.055), in: RoundedRectangle(cornerRadius: 8))
            Text(store.watchlist.symbols.count == 3 ? "已选满 3 个，先取消一个再添加。" : "选择 1–3 个品种，上方箭头可调整顺序。")
                .font(.system(size: 10)).foregroundStyle(.secondary)
            if store.loadingCatalog {
                HStack { ProgressView().controlSize(.small); Text("正在加载 Binance 合约…").font(.caption) }.padding(.vertical, 10)
            } else if let error = store.catalogError {
                Text(error).font(.caption).foregroundStyle(.orange).fixedSize(horizontal: false, vertical: true)
                Button("重新加载品种") { store.loadContracts() }.buttonStyle(.plain).foregroundStyle(accent)
            } else if results.isEmpty {
                Text("没有匹配的 USDT 永续合约").font(.caption).foregroundStyle(.secondary).padding(.vertical, 10)
            } else {
                ScrollView {
                    LazyVStack(spacing: 0) {
                        ForEach(results) { contract in
                            let selected = store.watchlist.symbols.contains(contract.symbol)
                            Button { store.toggle(contract.symbol) } label: {
                                HStack {
                                    Text(contract.baseAsset).font(.system(size: 12, weight: .medium))
                                    Text("USDT 永续").font(.system(size: 10)).foregroundStyle(.secondary)
                                    Spacer()
                                    Image(systemName: selected ? "checkmark.circle.fill" : "plus.circle")
                                        .foregroundStyle(selected ? accent : .secondary)
                                }.padding(.vertical, 8).contentShape(Rectangle())
                            }
                            .buttonStyle(.plain)
                            .disabled(selected ? store.watchlist.symbols.count == 1 : store.watchlist.symbols.count == 3)
                            .accessibilityLabel("\(selected ? "取消" : "添加") \(contract.symbol)")
                        }
                    }
                }.frame(height: 150)
            }
        }
    }
}
