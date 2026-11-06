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

    func refreshInstallers() async {
        guard !isBusy else { return }
        isBusy = true
        defer { isBusy = false }
        do {
            let entries = try await Task.detached(priority: .utility) {
                try FullInstallerService.availableInstallers()
            }.value
            installers = entries
            if !entries.contains(where: { $0.version == selectedVersion }) {
                selectedVersion = entries.first?.version ?? ""
            }
            statusMessage = "Loaded \(entries.count) full installer version(s) from Apple Software Update."
        } catch {
            statusMessage = error.localizedDescription
        }
    }

    func downloadInstaller(to destination: URL) async {
        guard !isBusy else { return }
        guard !selectedVersion.isEmpty else {
            statusMessage = "Choose a macOS version first."
            return
        }
        isBusy = true
        statusMessage = "Downloading the full macOS installer through Apple's Software Update service…"
        defer { isBusy = false }
        do {
            let version = selectedVersion
            let target = try await Task.detached(priority: .utility) {
                try FullInstallerService.download(version: version, to: destination)
            }.value
            lastOutputPath = target.path
            statusMessage = "Full installer saved to: \(target.path)"
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
