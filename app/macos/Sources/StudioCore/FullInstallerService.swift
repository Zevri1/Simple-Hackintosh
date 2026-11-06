import Foundation

public enum FullInstallerService {
    public static func availableInstallers() async throws -> [InstallerEntry] {
        try await AppleSoftwareUpdateCatalog.fetchAvailableInstallers()
    }

    /// Streams Apple's InstallAssistant.pkg directly into the selected folder.
    public static func download(
        installer: InstallerEntry,
        to destination: URL,
        progress: (@Sendable (InstallerDownloadProgress) -> Void)? = nil
    ) async throws -> URL {
        try await ApplePackageDownloader.download(installer, to: destination, progress: progress)
    }
}
