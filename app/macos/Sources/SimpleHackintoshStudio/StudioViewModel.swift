import Combine
import Foundation
import StudioCore

@MainActor
final class StudioViewModel: ObservableObject {
    @Published var installers: [InstallerEntry] = []
    @Published var selectedVersion = ""
    @Published var hardwareReport = ""
    @Published var statusMessage = "Ready. Choose a task from the sidebar."
    @Published var isBusy = false
    @Published var lastOutputPath = ""
    @Published var downloadProgress: InstallerDownloadProgress?

    var selectedInstaller: InstallerEntry? {
        installers.first { $0.version == selectedVersion }
    }

    func refreshInstallers() async {
        guard !isBusy else { return }
        isBusy = true
        statusMessage = "Reading Apple's public full-installer catalog…"
        defer { isBusy = false }
        do {
            let entries = try await FullInstallerService.availableInstallers()
            installers = entries
            if !entries.contains(where: { $0.version == selectedVersion }) {
                selectedVersion = entries.first?.version ?? ""
            }
            statusMessage = "Loaded \(entries.count) full installer package(s) from Apple."
        } catch {
            statusMessage = error.localizedDescription
        }
    }

    func downloadInstaller(to destination: URL) async {
        guard !isBusy else { return }
        guard let installer = selectedInstaller else {
            statusMessage = "Choose a macOS version first."
            return
        }

        isBusy = true
        downloadProgress = nil
        statusMessage = "Streaming \(installer.title) directly to the selected drive…"
        defer { isBusy = false }

        do {
            let target = try await FullInstallerService.download(
                installer: installer,
                to: destination,
                progress: { [weak self] progress in
                    Task { @MainActor in
                        self?.downloadProgress = progress
                    }
                }
            )
            lastOutputPath = target.path
            statusMessage = "Full InstallAssistant package saved to: \(target.path)"
        } catch {
            statusMessage = error.localizedDescription
        }
    }

    func scanHardware() async {
        guard !isBusy else { return }
        isBusy = true
        statusMessage = "Collecting hardware details and redacting device identifiers…"
        defer { isBusy = false }
        do {
            let snapshot = try await Task.detached(priority: .utility) {
                try SystemHardwareScanner.scan()
            }.value
            hardwareReport = snapshot.redactedJSON
            statusMessage = "Hardware scan complete. Common serials, UUIDs and network addresses are redacted."
        } catch {
            statusMessage = error.localizedDescription
        }
    }

    func buildStarterEFI(to destination: URL) async {
        guard !isBusy else { return }
        isBusy = true
        statusMessage = "Checking the latest official OpenCore release and SHA-256 digest…"
        defer { isBusy = false }
        do {
            let result = try await OpenCoreEFIBuilder.buildStarterEFI(to: destination)
            lastOutputPath = result.folderURL.path
            statusMessage = "OpenCore \(result.version) starter created and verified: \(result.folderURL.path)"
        } catch {
            statusMessage = error.localizedDescription
        }
    }

    func exportHardwareReport(to destination: URL) {
        guard !hardwareReport.isEmpty else {
            statusMessage = "Run a hardware scan before exporting."
            return
        }
        do {
            try hardwareReport.write(to: destination, atomically: true, encoding: .utf8)
            lastOutputPath = destination.path
            statusMessage = "Redacted hardware report saved to: \(destination.path)"
        } catch {
            statusMessage = error.localizedDescription
        }
    }
}
