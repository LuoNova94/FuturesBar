import Foundation

public struct ProxySettings {
    public private(set) var enabled: Bool
    public private(set) var port: Int?

    public init(defaults: UserDefaults) {
        let savedEnabled = defaults.object(forKey: "useProxy") as? Bool
        let savedPort = defaults.integer(forKey: "proxyPort")
        // Versions through 0.1.1 implicitly used this local proxy. Preserve it
        // only for existing installations; new installations follow macOS.
        let legacyInstall = savedEnabled == nil && defaults.bool(forKey: "hasOpenedPanel")
        port = (1...65535).contains(savedPort) ? savedPort : (legacyInstall ? 7892 : nil)
        enabled = (savedEnabled ?? legacyInstall) && port != nil
    }

    public var sessionPort: Int? { enabled ? port : nil }

    public func updated(enabled: Bool, portText: String) throws -> ProxySettings {
        var settings = self
        settings.enabled = enabled
        if enabled {
            guard let port = Int(portText.trimmingCharacters(in: .whitespacesAndNewlines)),
                  (1...65535).contains(port) else { throw ProxyError.invalidPort }
            settings.port = port
        }
        return settings
    }

    public func save(to defaults: UserDefaults) {
        defaults.set(enabled, forKey: "useProxy")
        if let port { defaults.set(port, forKey: "proxyPort") }
        else { defaults.removeObject(forKey: "proxyPort") }
    }
}

public enum ProxyError: LocalizedError {
    case invalidPort
    public var errorDescription: String? { "端口需要填写 1–65535 之间的整数。" }
}
