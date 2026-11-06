import XCTest
@testable import SimpleHackintoshCore

final class SimpleHackintoshCoreTests: XCTestCase {
    func testInstallerCatalogParserReadsMultipleVersionsAndDeduplicates() {
        let output = """
        Finding available software
        Software Update found the following full installers:
        * Title: macOS Tahoe, Version: 26.0.1, Size: 16.4 GB, Build: 25A123
        * Title: macOS Sequoia, Version: 15.7.4, Size: 15.2 GB, Build: 24G99
        * Title: macOS Sequoia, Version: 15.7.4, Size: 15.2 GB, Build: 24G99
        """
        let installers = InstallerCatalogParser.parse(output)
        XCTAssertEqual(installers, [
            InstallerOption(title: "macOS Tahoe", version: "26.0.1"),
            InstallerOption(title: "macOS Sequoia", version: "15.7.4")
        ])
    }

    func testInstallerCatalogParserReturnsEmptyForUnrecognizedOutput() {
        XCTAssertTrue(InstallerCatalogParser.parse("No full installers found.").isEmpty)
    }

    func testShellArgumentEscapingProtectsSpacesAndSingleQuotes() {
        XCTAssertEqual(CommandEscaping.shellArgument("USB Drive's"), "'USB Drive'\\''s'")
    }

    func testAppleScriptStringEscapesQuotesBackslashesAndNewlines() {
        XCTAssertEqual(CommandEscaping.appleScriptString("a\"b\\c\nnext"), "\"a\\\"b\\\\c\\nnext\"")
    }

    func testPathSafetyUsesPathComponentsInsteadOfStringPrefixes() {
        XCTAssertTrue(PathSafety.isInside(childPath: "/Volumes/USB/Install macOS.app", parentPath: "/Volumes/USB"))
        XCTAssertFalse(PathSafety.isInside(childPath: "/Volumes/USB2/Install macOS.app", parentPath: "/Volumes/USB"))
        XCTAssertFalse(PathSafety.isInside(childPath: "/Applications/Install macOS.app", parentPath: "/Volumes/USB"))
    }

    func testSHA256DigestComparisonIsStrict() {
        let digest = String(repeating: "a", count: 64)
        XCTAssertTrue(SHA256Digest.matches(githubDigest: "sha256:\(digest)", actualHex: digest))
        XCTAssertFalse(SHA256Digest.matches(githubDigest: nil, actualHex: digest))
        XCTAssertFalse(SHA256Digest.matches(githubDigest: "sha1:\(digest)", actualHex: digest))
        XCTAssertFalse(SHA256Digest.matches(githubDigest: "sha256:short", actualHex: digest))
        XCTAssertFalse(SHA256Digest.matches(
            githubDigest: "sha256:\(String(repeating: "b", count: 64))",
            actualHex: digest
        ))
    }
}
