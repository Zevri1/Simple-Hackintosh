// swift-tools-version: 6.0
import PackageDescription

let package = Package(
    name: "SimpleHackintoshMac",
    platforms: [.macOS(.v13)],
    products: [
        .executable(name: "SimpleHackintoshMac", targets: ["SimpleHackintoshMac"])
    ],
    targets: [
        .executableTarget(name: "SimpleHackintoshMac")
    ],
    swiftLanguageModes: [.v5]
)
