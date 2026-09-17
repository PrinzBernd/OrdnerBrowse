// swift-tools-version: 6.2

import PackageDescription

let package = Package(
    name: "WebUI.MAC",
    platforms: [
        .macOS(.v15)
    ],
    products: [
        .executable(
            name: "WebUI.MAC",
            targets: ["WebUIMac"])
    ],
    targets: [
        .executableTarget(
            name: "WebUIMac",
            path: "Sources/WebUIMac")
    ])
