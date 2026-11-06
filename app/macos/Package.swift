// swift-tools-version: 5.9
import PackageDescription

let package = Package(
    name: "SimpleHackintoshStudio",
    platforms: [.macOS(.v13)],
    products: [
        .library(name: "StudioCore", targets: ["StudioCore"]),
        .executable(name: "SimpleHackintoshStudio", targets: ["SimpleHackintoshStudio"])
    ],
    targets: [
        .target(name: "StudioCore"),
        .executableTarget(name: "SimpleHackintoshStudio", dependencies: ["StudioCore"]),
        .testTarget(name: "StudioCoreTests", dependencies: ["StudioCore"])
    ]
)
