import XCTest
@testable import StudioCore

final class AppleSoftwareUpdateCatalogTests: XCTestCase {
    func testRecognizesOSInstallFullInstallerMarker() {
        let product: [String: Any] = [
            "ExtendedMetaInfo": [
                "InstallAssistantPackageIdentifiers": [
                    "OSInstall": "com.apple.mpkg.OSInstall"
                ]
            ]
        ]
        XCTAssertTrue(AppleSoftwareUpdateCatalog.isFullInstaller(product))
    }

    func testRecognizesSharedSupportFullInstallerMarker() {
        let product: [String: Any] = [
            "ExtendedMetaInfo": [
                "InstallAssistantPackageIdentifiers": [
                    "SharedSupport": "com.apple.pkg.InstallAssistantSomeVersion"
                ]
            ]
        ]
        XCTAssertTrue(AppleSoftwareUpdateCatalog.isFullInstaller(product))
    }

    func testDoesNotClassifyRecoveryOnlyProductAsFullInstaller() {
        let product: [String: Any] = [
            "Packages": [
                ["URL": "https://updates.cdn-apple.com/RecoveryHDUpdate.pkg", "Size": 100]
            ]
        ]
        XCTAssertFalse(AppleSoftwareUpdateCatalog.isFullInstaller(product))
    }

    func testFindsInstallAssistantPackageAndCatalogSize() throws {
        let product: [String: Any] = [
            "Packages": [
                ["URL": "https://updates.cdn-apple.com/content/InstallAssistant.pkg", "Size": 15_000_000_000]
            ]
        ]
        let package = try XCTUnwrap(AppleSoftwareUpdateCatalog.installAssistantPackage(in: product))
        XCTAssertEqual(package.url.lastPathComponent, "InstallAssistant.pkg")
        XCTAssertEqual(package.expectedBytes, 15_000_000_000)
    }

    func testRejectsHTTPAndLookalikeDomains() {
        XCTAssertFalse(AppleSoftwareUpdateCatalog.isAppleHTTPS(
            URL(string: "http://updates.apple.com/InstallAssistant.pkg")!
        ))
        XCTAssertFalse(AppleSoftwareUpdateCatalog.isAppleHTTPS(
            URL(string: "https://apple.com.attacker.example/InstallAssistant.pkg")!
        ))
        XCTAssertTrue(AppleSoftwareUpdateCatalog.isAppleHTTPS(
            URL(string: "https://updates.cdn-apple.com/InstallAssistant.pkg")!
        ))
    }
}
