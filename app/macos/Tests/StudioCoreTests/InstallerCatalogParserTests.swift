import XCTest
@testable import StudioCore

final class InstallerCatalogParserTests: XCTestCase {
    func testParsesAppleFullInstallerOutput() {
        let output = """
        Finding available software
        Software Update found the following full installers:
        * Title: macOS Tahoe, Version: 26.1, Size: 15.6 GB, Build: 25B78
        * Title: macOS Sequoia, Version: 15.7.2, Size: 14.4 GB, Build: 24G325
        """
        let entries = InstallerCatalogParser.parse(output)
        XCTAssertEqual(entries.count, 2)
        XCTAssertEqual(entries[0].version, "26.1")
        XCTAssertEqual(entries[0].title, "macOS Tahoe")
        XCTAssertEqual(entries[0].size, "15.6 GB")
        XCTAssertEqual(entries[0].build, "25B78")
        XCTAssertEqual(entries[1].version, "15.7.2")
    }

    func testSortsNumericallyAndRemovesDuplicates() {
        let output = """
        * Title: macOS 15.10, Version: 15.10, Size: 1 GB, Build: A
        * Title: macOS 15.9, Version: 15.9, Size: 1 GB, Build: B
        * Title: macOS 15.10, Version: 15.10, Size: 1 GB, Build: A
        """
        XCTAssertEqual(
            InstallerCatalogParser.parse(output).map(\.version),
            ["15.10", "15.9"]
        )
    }

    func testReturnsEmptyForUnrecognizedOutput() {
        XCTAssertTrue(InstallerCatalogParser.parse("No full installers available").isEmpty)
    }
}
