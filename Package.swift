// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "FuturesBar",
    platforms: [.macOS(.v13)],
    products: [
        .executable(name: "FuturesBar", targets: ["FuturesBar"]),
        .executable(name: "FuturesProbe", targets: ["FuturesProbe"])
    ],
    targets: [
        .target(name: "FuturesCore"),
        .executableTarget(name: "FuturesBar", dependencies: ["FuturesCore"]),
        .executableTarget(name: "FuturesProbe", dependencies: ["FuturesCore"]),
        .testTarget(name: "FuturesCoreTests", dependencies: ["FuturesCore"]),
        .testTarget(name: "FuturesBarLayoutTests", dependencies: ["FuturesBar"])
    ]
)
