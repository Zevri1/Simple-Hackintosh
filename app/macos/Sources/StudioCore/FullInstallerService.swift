import Foundation

public enum FullInstallerService {
    public static func availableInstallers() throws -> [InstallerEntry] {
        let result = try CommandRunner.run(
            "/usr/sbin/softwareupdate",
            arguments: ["--list-full-installers"]
        )
        let entries = InstallerCatalogParser.parse(result.text)
        guard !entries.isEmpty else {
            throw NSError(
                domain: "SimpleHackintoshStudio.Installer",
                code: 1,
                userInfo: [
                    NSLocalizedDescriptionKey:
                        "Apple Software Update did not return any full installers for this Mac."
                ]
            )
        }
        return entries
    }

    /// Apple downloads into /Applications first; Studio copies the completed .app bundle
    /// to the destination selected by the user.
    public static func download(version: String, to destination: URL) throws -> URL {
        guard !version.isEmpty else {
            throw makeError(2, "Choose a macOS version first.")
        }
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: destination.path, isDirectory: &isDirectory),
              isDirectory.boolValue else {
            throw makeError(3, "The selected destination folder does not exist.")
        }

        let previousInstaller = installerApplication(for: version)
        _ = try CommandRunner.run(
            "/usr/sbin/softwareupdate",
            arguments: ["--fetch-full-installer", "--full-installer-version", version]
        )

        guard let installer = installerApplication(for: version) else {
            throw makeError(
                4,
                "Apple's download command finished, but the matching installer app was not found in /Applications."
            )
        }

        let target = destination.appendingPathComponent(installer.lastPathComponent)
        guard !FileManager.default.fileExists(atPath: target.path) else {
            throw makeError(
                5,
                "An installer with this name already exists at the destination. Move or rename it first."
            )
        }

        try FileManager.default.copyItem(at: installer, to: target)
        if previousInstaller == nil {
            try? FileManager.default.removeItem(at: installer)
        }
        return target
    }

    private static func installerApplication(for version: String) -> URL? {
        let applications = URL(fileURLWithPath: "/Applications", isDirectory: true)
        guard let candidates = try? FileManager.default.contentsOfDirectory(
            at: applications,
            includingPropertiesForKeys: [.isDirectoryKey, .contentModificationDateKey],
            options: [.skipsHiddenFiles]
        ) else { return nil }

        return candidates
            .filter { $0.lastPathComponent.hasPrefix("Install macOS ") }
            .first { candidate in
                guard let bundle = Bundle(url: candidate) else { return false }
                return bundle.infoDictionary?["CFBundleShortVersionString"] as? String == version
            }
    }

    private static func makeError(_ code: Int, _ message: String) -> NSError {
        NSError(
            domain: "SimpleHackintoshStudio.Installer",
            code: code,
            userInfo: [NSLocalizedDescriptionKey: message]
        )
    }
}
