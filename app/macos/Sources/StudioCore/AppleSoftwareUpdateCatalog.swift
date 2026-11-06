import Foundation

public enum AppleSoftwareUpdateCatalog {
    /// Apple's public catalog family, newest public macOS releases first.
    /// Product records are independently filtered for the full InstallAssistant package.
    public static let publicCatalogURL = URL(
        string: "https://swscan.apple.com/content/catalogs/others/index-27-26-15-14-13-12-11-10.16-10.15-10.14-10.13-10.12-10.11-10.10-10.9-10.8-10.7-10.6-10.5.merged-1.sucatalog"
    )!

    public static func fetchAvailableInstallers() async throws -> [InstallerEntry] {
        let (catalogData, response) = try await URLSession.shared.data(from: publicCatalogURL)
        try validateHTTP(response)

        guard let catalog = try PropertyListSerialization.propertyList(
            from: catalogData, options: [], format: nil
        ) as? [String: Any],
        let products = catalog["Products"] as? [String: Any] else {
            throw catalogError("Apple's software update catalog could not be parsed.")
        }

        let candidates = products.compactMap { productID, rawProduct -> (String, [String: Any])? in
            guard let product = rawProduct as? [String: Any],
                  isFullInstaller(product),
                  installAssistantPackage(in: product) != nil else {
                return nil
            }
            return (productID, product)
        }
        .sorted {
            ($0.1["PostDate"] as? Date ?? .distantPast) >
            ($1.1["PostDate"] as? Date ?? .distantPast)
        }

        var newestByVersion: [String: InstallerEntry] = [:]
        for (productID, product) in candidates.prefix(100) {
            guard let package = installAssistantPackage(in: product),
                  let metadataText = product["ServerMetadataURL"] as? String,
                  let metadataURL = URL(string: metadataText),
                  isAppleHTTPS(metadataURL) else {
                continue
            }

            do {
                let (metadataData, metadataResponse) = try await URLSession.shared.data(from: metadataURL)
                try validateHTTP(metadataResponse)
                guard let metadata = try PropertyListSerialization.propertyList(
                    from: metadataData, options: [], format: nil
                ) as? [String: Any],
                let version = metadata["CFBundleShortVersionString"] as? String,
                !version.isEmpty else {
                    continue
                }

                let localization = metadata["localization"] as? [String: Any]
                let english = localization?["English"] as? [String: Any]
                let title = stringValue(english?["title"]) ?? "macOS (version)"
                let byteSize = package.expectedBytes
                let readableSize = byteSize.map {
                    ByteCountFormatter.string(fromByteCount: $0, countStyle: .file)
                }
                let entry = InstallerEntry(
                    title: title,
                    version: version,
                    size: readableSize,
                    build: nil,
                    productID: productID,
                    downloadURL: package.url,
                    expectedBytes: byteSize
                )

                // Candidates are sorted by Apple's PostDate, so the first product
                // for a marketing version is the newest one.
                if newestByVersion[version] == nil {
                    newestByVersion[version] = entry
                }
                if newestByVersion.count >= 20 { break }
            } catch {
                // A stale product metadata URL must not hide other available versions.
                continue
            }
        }

        let entries = newestByVersion.values.sorted {
            isNewerVersion($0.version, than: $1.version)
        }
        guard !entries.isEmpty else {
            throw catalogError("No full InstallAssistant packages were found in Apple's public catalog.")
        }
        return entries
    }

    /// Apple marks full installers with these InstallAssistant identifiers.
    public static func isFullInstaller(_ product: [String: Any]) -> Bool {
        let extended = product["ExtendedMetaInfo"] as? [String: Any]
        let identifiers = extended?["InstallAssistantPackageIdentifiers"] as? [String: Any]
        let osInstall = identifiers?["OSInstall"] as? String ?? ""
        let sharedSupport = identifiers?["SharedSupport"] as? String ?? ""
        return osInstall == "com.apple.mpkg.OSInstall"
            || sharedSupport.hasPrefix("com.apple.pkg.InstallAssistant")
    }

    public static func installAssistantPackage(
        in product: [String: Any]
    ) -> (url: URL, expectedBytes: Int64?)? {
        guard let packages = product["Packages"] as? [[String: Any]],
              let package = packages.first(where: { raw in
                  guard let address = raw["URL"] as? String,
                        let url = URL(string: address) else { return false }
                  return url.lastPathComponent.lowercased() == "installassistant.pkg"
              }),
              let address = package["URL"] as? String,
              let url = URL(string: address),
              isAppleHTTPS(url) else {
            return nil
        }

        let size = (package["Size"] as? NSNumber)?.int64Value
        return (url, size)
    }

    public static func isAppleHTTPS(_ url: URL) -> Bool {
        guard url.scheme?.lowercased() == "https",
              let host = url.host?.lowercased() else { return false }
        return host == "apple.com" || host.hasSuffix(".apple.com")
            || host == "cdn-apple.com" || host.hasSuffix(".cdn-apple.com")
    }

    private static func validateHTTP(_ response: URLResponse) throws {
        guard let http = response as? HTTPURLResponse,
              (200..<300).contains(http.statusCode) else {
            let status = (response as? HTTPURLResponse)?.statusCode ?? -1
            throw catalogError("Apple returned HTTP (status).")
        }
    }

    private static func stringValue(_ value: Any?) -> String? {
        if let value = value as? String, !value.isEmpty { return value }
        if let data = value as? Data,
           let value = String(data: data, encoding: .utf8),
           !value.isEmpty {
            return value
        }
        return nil
    }

    private static func isNewerVersion(_ lhs: String, than rhs: String) -> Bool {
        let left = lhs.split(separator: ".").compactMap { Int($0) }
        let right = rhs.split(separator: ".").compactMap { Int($0) }
        for index in 0..<max(left.count, right.count) {
            let a = index < left.count ? left[index] : 0
            let b = index < right.count ? right[index] : 0
            if a != b { return a > b }
        }
        return lhs > rhs
    }

    private static func catalogError(_ message: String) -> NSError {
        NSError(
            domain: "SimpleHackintoshStudio.AppleCatalog",
            code: 1,
            userInfo: [NSLocalizedDescriptionKey: message]
        )
    }
}
