// swift-tools-version: 5.10
import PackageDescription

let package = Package(
    name: "SimpleHackintoshMac",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "SimpleHackintoshCore", targets: ["SimpleHackintoshCore"]),
        .executable(name: "SimpleHackintoshMacApp", targets: ["SimpleHackintoshMacApp"])
    ],
    targets: [
        .target(name: "SimpleHackintoshCore"),
        .executableTarget(name: "SimpleHackintoshMacApp", dependencies: ["SimpleHackintoshCore"]),
        .testTarget(name: "SimpleHackintoshCoreTests", dependencies: ["SimpleHackintoshCore"])
    ]
)
