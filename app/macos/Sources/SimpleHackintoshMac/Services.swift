import AppKit
import Combine
import CryptoKit
import Foundation

struct InstallerRecord: Identifiable, Hashable {
    let identifier: String
    let name: String
    let version: String
    let build: String
    let releaseDate: String
    let size: Int64

    var id: String { identifier.isEmpty ? "\(version)-\(build)" : identifier }
    var title: String { "\(name) \(version)" }
    var detail: String { "Build \(build) · \(releaseDate)" }
    var sizeText: String {
        guard size > 0 else { return "Size not reported" }
        let formatter = ByteCountFormatter()
        formatter.countStyle = .file
        return formatter.string(fromByteCount: size)
    }
}

struct HardwareItem: Identifiable {
    let name: String
    let value: String
    var id: String { name }
}

enum FindingLevel: Equatable {
    case pass
    case warning
    case failure
}

struct EFIFinding: Identifiable {
    let title: String
    let detail: String
    let level: FindingLevel
    var id: String { title }
}

struct VolumeInfo: Identifiable, Hashable {
    let name: String
    let path: String
    let capacityText: String

    var id: String { path }
    var displayTitle: String { "\(name) · \(capacityText)" }
}

enum AppFailure: LocalizedError {
    case missingHelper(String)
    case commandFailed(String)
    case invalidResponse(String)
    case invalidArchive(String)
    case unsafeDestination(String)
    case missingInstaller(String)

    var errorDescription: String? {
        switch self {
        case .missingHelper(let name): return "The bundled helper '\(name)' is missing."
        case .commandFailed(let detail): return detail
        case .invalidResponse(let detail): return detail
        case .invalidArchive(let detail): return detail
        case .unsafeDestination(let detail): return detail
        case .missingInstaller(let detail): return detail
        }
    }
}

enum ProcessRunner {
    static func run(executable: String, arguments: [String]) throws -> String {
        let process = Process()
        let pipe = Pipe()
        process.executableURL = URL(fileURLWithPath: executable)
        process.arguments = arguments
        process.standardOutput = pipe
        process.standardError = pipe

        do {
            try process.run()
        } catch {
            throw AppFailure.commandFailed("Could not launch \(executable): \(error.localizedDescription)")
        }

        let data = pipe.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        let output = String(decoding: data, as: UTF8.self).trimmingCharacters(in: .whitespacesAndNewlines)

        guard process.terminationStatus == 0 else {
            throw AppFailure.commandFailed(output.isEmpty ? "\(URL(fileURLWithPath: executable).lastPathComponent) exited with code \(process.terminationStatus)." : output)
        }
        return output
    }

    static func runPrivileged(executable: String, arguments: [String]) throws -> String {
        let command = ([executable] + arguments).map(shellQuote).joined(separator: " ")
        let script = "do shell script \(appleScriptQuote(command)) with administrator privileges"
        return try run(executable: "/usr/bin/osascript", arguments: ["-e", script])
    }

    private static func shellQuote(_ value: String) -> String {
        "'" + value.replacingOccurrences(of: "'", with: "'\\''") + "'"
    }

    private static func appleScriptQuote(_ value: String) -> String {
        let escaped = value
            .replacingOccurrences(of: "\\", with: "\\\\")
            .replacingOccurrences(of: "\"", with: "\\\"")
            .replacingOccurrences(of: "\n", with: "\\n")
            .replacingOccurrences(of: "\r", with: "")
        return "\"" + escaped + "\""
    }
}

enum InstallerService {
    private static var mistExecutable: String {
        Bundle.main.bundleURL.appendingPathComponent("Contents/Helpers/mist").path
    }

    static func listInstallers() throws -> [InstallerRecord] {
        guard FileManager.default.isExecutableFile(atPath: mistExecutable) else {
            throw AppFailure.missingHelper("mist")
        }
        let output = try ProcessRunner.run(
            executable: mistExecutable,
            arguments: ["list", "installer", "--output-type", "json", "--quiet", "--no-ansi"]
        )
        return try decodeInstallers(Data(output.utf8))
    }

    static func decodeInstallers(_ data: Data) throws -> [InstallerRecord] {
        let root: Any
        do {
            root = try JSONSerialization.jsonObject(with: data, options: [.fragmentsAllowed])
        } catch {
            throw AppFailure.invalidResponse("The Apple installer catalog returned invalid JSON: \(error.localizedDescription)")
        }

        let rows: [[String: Any]]
        if let array = root as? [[String: Any]] {
            rows = array
        } else if
            let dictionary = root as? [String: Any],
            let array = (dictionary["installers"] ?? dictionary["Installers"]) as? [[String: Any]] {
            rows = array
        } else {
            throw AppFailure.invalidResponse("The installer catalog did not contain a list of installers.")
        }

        return rows.compactMap { row in
            guard
                let version = stringValue(row["Version"] ?? row["version"]),
                !version.isEmpty else { return nil }

            let packages = row["Packages"] as? [[String: Any]] ?? []
            let packageSize = packages.reduce(Int64(0)) { partial, item in
                partial + integerValue(item["Size"] ?? item["size"])
            }
            let explicitSize = integerValue(row["Size"] ?? row["size"])
            let identifier = stringValue(row["Identifier"] ?? row["identifier"]) ?? ""
            let name = stringValue(row["Name"] ?? row["name"]) ?? "macOS"
            let build = stringValue(row["Build"] ?? row["build"]) ?? "Unknown build"
            let date = stringValue(row["PostDate"] ?? row["date"]) ?? "Date not listed"

            return InstallerRecord(
                identifier: identifier,
                name: name,
                version: version,
                build: build,
                releaseDate: date,
                size: explicitSize > 0 ? explicitSize : packageSize
            )
        }
    }

    static func download(_ installer: InstallerRecord, to outputDirectory: URL) throws -> URL {
        guard FileManager.default.fileExists(atPath: outputDirectory.path) else {
            throw AppFailure.unsafeDestination("Choose an existing destination folder before downloading.")
        }
        guard FileManager.default.isExecutableFile(atPath: mistExecutable) else {
            throw AppFailure.missingHelper("mist")
        }

        let temporaryDirectory = outputDirectory.appendingPathComponent(".simple-hackintosh-cache", isDirectory: true)
        let searchKey = installer.build == "Unknown build" ? installer.version : installer.build

        _ = try ProcessRunner.runPrivileged(
            executable: mistExecutable,
            arguments: [
                "download", "installer", searchKey, "application",
                "--output-directory", outputDirectory.path,
                "--temporary-directory", temporaryDirectory.path,
                "--quiet", "--no-ansi"
            ]
        )

        let candidates = try FileManager.default.contentsOfDirectory(
            at: outputDirectory,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        ).filter { $0.pathExtension.caseInsensitiveCompare("app") == .orderedSame }

        guard let application = candidates.first(where: {
            $0.lastPathComponent.localizedCaseInsensitiveContains(installer.name)
                || $0.lastPathComponent.localizedCaseInsensitiveContains(installer.version)
        }) ?? candidates.first else {
            throw AppFailure.missingInstaller("Mist completed, but no macOS installer application was found in \(outputDirectory.path).")
        }
        return application
    }

    static func createBootableUSB(installerApp: URL, volume: VolumeInfo) throws {
        let createInstallMedia = installerApp
            .appendingPathComponent("Contents/Resources/createinstallmedia")
        guard FileManager.default.isExecutableFile(atPath: createInstallMedia.path) else {
            throw AppFailure.missingInstaller("The selected installer does not contain Apple's createinstallmedia tool.")
        }
        guard volume.path.hasPrefix("/Volumes/"), volume.path != "/", FileManager.default.fileExists(atPath: volume.path) else {
            throw AppFailure.unsafeDestination("The selected target is not a mounted external volume.")
        }

        let targetRoot = URL(fileURLWithPath: volume.path, isDirectory: true).standardizedFileURL.path
        let targetPrefix = targetRoot.hasSuffix("/") ? targetRoot : targetRoot + "/"
        let installerPath = installerApp.standardizedFileURL.path
        guard !installerPath.hasPrefix(targetPrefix) else {
            throw AppFailure.unsafeDestination("The installer application is stored on the same volume selected for erasure. Move the installer to a different disk and choose a separate USB target.")
        }

        _ = try ProcessRunner.runPrivileged(
            executable: createInstallMedia.path,
            arguments: ["--volume", volume.path, "--nointeraction"]
        )
    }
}

enum OpenCoreService {
    private struct GitHubRelease: Decodable {
        let tag_name: String
        let assets: [ReleaseAsset]
    }

    private struct ReleaseAsset: Decodable {
        let name: String
        let browser_download_url: URL
        let digest: String?
        let size: Int64
    }

    static func createStarterEFI(in parentDirectory: URL) async throws -> (URL, String) {
        guard FileManager.default.fileExists(atPath: parentDirectory.path) else {
            throw AppFailure.unsafeDestination("Choose an existing destination folder.")
        }

        let apiURL = URL(string: "https://api.github.com/repos/acidanthera/OpenCorePkg/releases/latest")!
        var apiRequest = URLRequest(url: apiURL)
        apiRequest.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        apiRequest.setValue("Simple-Hackintosh-Studio", forHTTPHeaderField: "User-Agent")

        let (metadata, metadataResponse) = try await URLSession.shared.data(for: apiRequest)
        guard let metadataHTTP = metadataResponse as? HTTPURLResponse, (200..<300).contains(metadataHTTP.statusCode) else {
            throw AppFailure.invalidResponse("GitHub did not return the official OpenCore release metadata.")
        }

        let release = try JSONDecoder().decode(GitHubRelease.self, from: metadata)
        guard let asset = release.assets.first(where: {
            $0.name.contains("RELEASE.zip") && $0.digest?.hasPrefix("sha256:") == true
        }), let reportedDigest = asset.digest else {
            throw AppFailure.invalidResponse("The official OpenCore release has no RELEASE ZIP with a published SHA-256 digest.")
        }

        guard
            asset.browser_download_url.scheme == "https",
            asset.browser_download_url.host == "github.com",
            asset.browser_download_url.path.contains("/acidanthera/OpenCorePkg/releases/download/") else {
            throw AppFailure.invalidResponse("The OpenCore download URL was not an official GitHub release asset.")
        }

        let destination = parentDirectory.appendingPathComponent("EFI", isDirectory: true)
        guard !FileManager.default.fileExists(atPath: destination.path) else {
            throw AppFailure.unsafeDestination("An EFI folder already exists at the selected destination. Choose another folder; the app will not overwrite it.")
        }

        let temporaryRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("SimpleHackintosh-OpenCore-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: temporaryRoot, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: temporaryRoot) }

        let (downloadURL, downloadResponse) = try await URLSession.shared.download(from: asset.browser_download_url)
        guard let downloadHTTP = downloadResponse as? HTTPURLResponse, (200..<300).contains(downloadHTTP.statusCode) else {
            throw AppFailure.invalidResponse("The OpenCore release download did not complete successfully.")
        }

        let downloadedSize = Int64(try downloadURL.resourceValues(forKeys: [.fileSizeKey]).fileSize ?? 0)
        guard downloadedSize > 0, downloadedSize == asset.size else {
            throw AppFailure.invalidArchive("The OpenCore archive size did not match GitHub's published asset size.")
        }

        let archiveData = try Data(contentsOf: downloadURL, options: [.mappedIfSafe])
        let digest = SHA256.hash(data: archiveData).map { String(format: "%02x", $0) }.joined()
        let expectedDigest = String(reportedDigest.dropFirst("sha256:".count)).lowercased()
        guard digest == expectedDigest else {
            throw AppFailure.invalidArchive("SHA-256 verification failed. The OpenCore archive was not extracted.")
        }

        let archiveURL = temporaryRoot.appendingPathComponent(asset.name)
        try FileManager.default.moveItem(at: downloadURL, to: archiveURL)

        let extractionDirectory = temporaryRoot.appendingPathComponent("extracted", isDirectory: true)
        try FileManager.default.createDirectory(at: extractionDirectory, withIntermediateDirectories: true)
        _ = try ProcessRunner.run(
            executable: "/usr/bin/ditto",
            arguments: ["-x", "-k", archiveURL.path, extractionDirectory.path]
        )

        let sourceEFI = extractionDirectory.appendingPathComponent("X64/EFI", isDirectory: true)
        let samplePlist = extractionDirectory.appendingPathComponent("Docs/Sample.plist")
        let bootFile = sourceEFI.appendingPathComponent("BOOT/BOOTx64.efi")
        let openCoreFile = sourceEFI.appendingPathComponent("OC/OpenCore.efi")

        guard
            FileManager.default.fileExists(atPath: bootFile.path),
            FileManager.default.fileExists(atPath: openCoreFile.path),
            FileManager.default.fileExists(atPath: samplePlist.path) else {
            throw AppFailure.invalidArchive("The official OpenCore archive did not contain the expected X64/EFI and Docs/Sample.plist files.")
        }

        try FileManager.default.copyItem(at: sourceEFI, to: destination)
        try FileManager.default.copyItem(
            at: samplePlist,
            to: destination.appendingPathComponent("OC/config.plist")
        )

        let notice = """
        SIMPLE HACKINTOSH — EFI STARTER

        OpenCore release: \(release.tag_name)

        This folder is a starter only. config.plist is the upstream Sample.plist.
        It has NOT been configured for your CPU, motherboard, firmware, GPU,
        audio, network devices, or storage controller and must not be treated
        as boot-ready.

        Before booting:
        1. Read the matching OpenCore documentation.
        2. Select hardware-specific ACPI tables, Kexts, drivers, quirks and SMBIOS.
        3. Keep personal SMBIOS identifiers private.
        4. Validate config.plist with ocvalidate from this exact OpenCore release.
        5. Keep a backup of any working EFI.
        """
        try notice.write(
            to: destination.appendingPathComponent("START-HERE.txt"),
            atomically: true,
            encoding: .utf8
        )

        return (destination, release.tag_name)
    }
}

enum HardwareService {
    static func collect() throws -> [HardwareItem] {
        let output = try ProcessRunner.run(
            executable: "/usr/sbin/system_profiler",
            arguments: ["-json", "SPHardwareDataType", "SPDisplaysDataType"]
        )
        guard
            let data = output.data(using: .utf8),
            let root = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw AppFailure.invalidResponse("system_profiler returned an unexpected hardware report.")
        }

        let hardware = (root["SPHardwareDataType"] as? [[String: Any]])?.first ?? [:]
        let display = (root["SPDisplaysDataType"] as? [[String: Any]])?.first ?? [:]

        let model = stringValue(hardware["machine_name"]) ?? stringValue(hardware["machine_model"]) ?? "Unknown Mac model"
        let chip = stringValue(hardware["chip_type"]) ?? stringValue(hardware["cpu_type"]) ?? stringValue(hardware["processor_name"]) ?? "Not reported"
        let memory = stringValue(hardware["physical_memory"]) ?? stringValue(hardware["memory"]) ?? "Not reported"
        let graphics = stringValue(display["sppci_model"]) ?? stringValue(display["sptype"]) ?? "Not reported"
        let osVersion = try? ProcessRunner.run(executable: "/usr/bin/sw_vers", arguments: ["-productVersion"])
        let architecture = try? ProcessRunner.run(executable: "/usr/bin/uname", arguments: ["-m"])

        return [
            HardwareItem(name: "System", value: model),
            HardwareItem(name: "CPU / Chip", value: chip),
            HardwareItem(name: "Graphics", value: graphics),
            HardwareItem(name: "Memory", value: memory),
            HardwareItem(name: "macOS", value: osVersion ?? "Unknown"),
            HardwareItem(name: "Architecture", value: architecture ?? "Unknown")
        ]
    }
}

enum EFIValidationService {
    static func inspect(_ selectedURL: URL) -> [EFIFinding] {
        let fileManager = FileManager.default
        var root = selectedURL
        if root.lastPathComponent.caseInsensitiveCompare("EFI") != .orderedSame {
            let nested = root.appendingPathComponent("EFI", isDirectory: true)
            if fileManager.fileExists(atPath: nested.path) { root = nested }
        }

        let required = [
            ("BOOT/BOOTx64.efi", "UEFI fallback loader"),
            ("OC/OpenCore.efi", "OpenCore boot loader"),
            ("OC/config.plist", "OpenCore configuration")
        ]

        var findings: [EFIFinding] = required.map { relative, title in
            let exists = fileManager.fileExists(atPath: root.appendingPathComponent(relative).path)
            return EFIFinding(
                title: title,
                detail: exists ? "Found \(relative)" : "Missing \(relative)",
                level: exists ? .pass : .failure
            )
        }

        let configURL = root.appendingPathComponent("OC/config.plist")
        guard
            let data = try? Data(contentsOf: configURL),
            let config = (try? PropertyListSerialization.propertyList(from: data, options: [], format: nil)) as? [String: Any] else {
            findings.append(EFIFinding(
                title: "config.plist parsing",
                detail: "The configuration could not be parsed as an XML or binary property list.",
                level: .failure
            ))
            return findings
        }

        findings.append(EFIFinding(
            title: "config.plist parsing",
            detail: "The property list is readable.",
            level: .pass
        ))

        appendConfiguredPaths(
            config: config, section: "ACPI", child: "Add", pathKey: "Path",
            folder: "ACPI", root: root, findings: &findings
        )
        appendConfiguredPaths(
            config: config, section: "Kernel", child: "Add", pathKey: "BundlePath",
            folder: "Kexts", root: root, findings: &findings
        )
        appendConfiguredPaths(
            config: config, section: "UEFI", child: "Drivers", pathKey: "Path",
            folder: "Drivers", root: root, findings: &findings
        )

        findings.append(EFIFinding(
            title: "Boot-readiness",
            detail: "A structural check cannot confirm hardware compatibility or prove that this EFI will boot.",
            level: .warning
        ))
        return findings
    }

    private static func appendConfiguredPaths(
        config: [String: Any],
        section: String,
        child: String,
        pathKey: String,
        folder: String,
        root: URL,
        findings: inout [EFIFinding]
    ) {
        guard
            let sectionValues = config[section] as? [String: Any],
            let entries = sectionValues[child] as? [[String: Any]] else { return }

        for entry in entries where (entry["Enabled"] as? Bool) == true {
            guard let path = entry[pathKey] as? String, !path.isEmpty else { continue }
            let file = root.appendingPathComponent("OC/\(folder)/\(path)")
            let exists = FileManager.default.fileExists(atPath: file.path)
            findings.append(EFIFinding(
                title: "\(section) entry: \(path)",
                detail: exists ? "Enabled file reference exists." : "Enabled file reference is missing.",
                level: exists ? .pass : .failure
            ))
        }
    }
}

enum VolumeService {
    static func removableVolumes() -> [VolumeInfo] {
        let keys: Set<URLResourceKey> = [
            .volumeNameKey, .volumeIsRemovableKey, .volumeIsEjectableKey,
            .volumeAvailableCapacityKey
        ]
        let volumes = FileManager.default.mountedVolumeURLs(
            includingResourceValuesForKeys: Array(keys),
            options: [.skipHiddenVolumes]
        ) ?? []

        return volumes.compactMap { url in
            guard url.path.hasPrefix("/Volumes/") else { return nil }
            guard let values = try? url.resourceValues(forKeys: keys) else { return nil }
            guard values.volumeIsRemovable == true || values.volumeIsEjectable == true else { return nil }
            let name = values.volumeName ?? url.lastPathComponent
            let capacity = values.volumeAvailableCapacity.map { ByteCountFormatter.string(fromByteCount: Int64($0), countStyle: .file) } ?? "Capacity unknown"
            return VolumeInfo(name: name, path: url.path, capacityText: capacity)
        }.sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }
}

private func stringValue(_ value: Any?) -> String? {
    if let value = value as? String { return value }
    if let value = value as? NSNumber { return value.stringValue }
    return nil
}

private func integerValue(_ value: Any?) -> Int64 {
    if let value = value as? NSNumber { return value.int64Value }
    if let value = value as? String, let number = Int64(value) { return number }
    return 0
}

@MainActor
final class AppModel: ObservableObject {
    @Published var installers: [InstallerRecord] = []
    @Published var selectedInstallerID = ""
    @Published var installerFolder = FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask).first?.path ?? NSHomeDirectory()
    @Published var isLoadingInstallers = false
    @Published var isDownloadingInstaller = false
    @Published var installerStatus = "Load the Apple installer catalog to begin."
    @Published var downloadedInstallerPath = ""
    @Published var volumes: [VolumeInfo] = []
    @Published var selectedVolumePath = ""
    @Published var isCreatingUSB = false
    @Published var usbStatus = "A bootable installer can be created after downloading a full installer."
    @Published var efiFolder = FileManager.default.urls(for: .downloadsDirectory, in: .userDomainMask).first?.path ?? NSHomeDirectory()
    @Published var isBuildingEFI = false
    @Published var efiStatus = "Download the official OpenCore release to create a starter EFI."
    @Published var selectedEFIPath = ""
    @Published var efiFindings: [EFIFinding] = []
    @Published var hardwareItems: [HardwareItem] = []
    @Published var isLoadingHardware = false
    @Published var hardwareStatus = "Hardware details are read locally from this Mac."

    var selectedInstaller: InstallerRecord? {
        installers.first(where: { $0.id == selectedInstallerID })
    }

    var selectedVolume: VolumeInfo? {
        volumes.first(where: { $0.path == selectedVolumePath })
    }

    func refreshInstallers() {
        guard !isLoadingInstallers else { return }
        isLoadingInstallers = true
        installerStatus = "Reading Apple's standard software update catalog…"

        DispatchQueue.global(qos: .userInitiated).async {
            do {
                let records = try InstallerService.listInstallers()
                DispatchQueue.main.async {
                    self.installers = records
                    if !records.contains(where: { $0.id == self.selectedInstallerID }) {
                        self.selectedInstallerID = records.first?.id ?? ""
                    }
                    self.installerStatus = records.isEmpty
                        ? "No installers were returned by the catalog."
                        : "Found \(records.count) full installer entries."
                    self.isLoadingInstallers = false
                }
            } catch {
                DispatchQueue.main.async {
                    self.installerStatus = error.localizedDescription
                    self.isLoadingInstallers = false
                }
            }
        }
    }

    func downloadSelectedInstaller() {
        guard let record = selectedInstaller else {
            installerStatus = "Choose a macOS version first."
            return
        }
        let destination = URL(fileURLWithPath: installerFolder, isDirectory: true)
        isDownloadingInstaller = true
        downloadedInstallerPath = ""
        installerStatus = "Requesting administrator authorization, then downloading the full installer to the selected drive…"

        DispatchQueue.global(qos: .userInitiated).async {
            do {
                let app = try InstallerService.download(record, to: destination)
                DispatchQueue.main.async {
                    self.downloadedInstallerPath = app.path
                    self.installerStatus = "Full installer created: \(app.path)"
                    self.isDownloadingInstaller = false
                }
            } catch {
                DispatchQueue.main.async {
                    self.installerStatus = error.localizedDescription
                    self.isDownloadingInstaller = false
                }
            }
        }
    }

    func refreshVolumes() {
        volumes = VolumeService.removableVolumes()
        if !volumes.contains(where: { $0.path == selectedVolumePath }) {
            selectedVolumePath = volumes.first?.path ?? ""
        }
        usbStatus = volumes.isEmpty
            ? "No mounted removable volumes were detected. Connect the USB drive and refresh."
            : "Select the exact USB volume. Creating the installer erases that volume."
    }

    func createBootableUSB() {
        guard
            !downloadedInstallerPath.isEmpty,
            let volume = selectedVolume,
            FileManager.default.fileExists(atPath: downloadedInstallerPath) else {
            usbStatus = "Download a full macOS installer and select a mounted removable volume first."
            return
        }

        let installerPath = downloadedInstallerPath
        isCreatingUSB = true
        usbStatus = "The macOS installer is being written to \(volume.name)…"

        DispatchQueue.global(qos: .userInitiated).async {
            do {
                try InstallerService.createBootableUSB(
                    installerApp: URL(fileURLWithPath: installerPath),
                    volume: volume
                )
                DispatchQueue.main.async {
                    self.usbStatus = "Bootable installer created on \(volume.name)."
                    self.isCreatingUSB = false
                }
            } catch {
                DispatchQueue.main.async {
                    self.usbStatus = error.localizedDescription
                    self.isCreatingUSB = false
                }
            }
        }
    }

    func buildStarterEFI() {
        let destination = URL(fileURLWithPath: efiFolder, isDirectory: true)
        isBuildingEFI = true
        selectedEFIPath = ""
        efiStatus = "Fetching the latest official OpenCore release and verifying its SHA-256 digest…"

        Task {
            do {
                let (folder, version) = try await OpenCoreService.createStarterEFI(in: destination)
                selectedEFIPath = folder.path
                efiStatus = "Starter EFI created with OpenCore \(version): \(folder.path)"
                isBuildingEFI = false
            } catch {
                efiStatus = error.localizedDescription
                isBuildingEFI = false
            }
        }
    }

    func inspectEFI(at path: String) {
        selectedEFIPath = path
        efiFindings = EFIValidationService.inspect(URL(fileURLWithPath: path, isDirectory: true))
    }

    func refreshHardware() {
        guard !isLoadingHardware else { return }
        isLoadingHardware = true
        hardwareStatus = "Collecting hardware data locally…"

        DispatchQueue.global(qos: .userInitiated).async {
            do {
                let items = try HardwareService.collect()
                DispatchQueue.main.async {
                    self.hardwareItems = items
                    self.hardwareStatus = "Inventory complete. No serial numbers or hardware UUIDs are requested."
                    self.isLoadingHardware = false
                }
            } catch {
                DispatchQueue.main.async {
                    self.hardwareStatus = error.localizedDescription
                    self.isLoadingHardware = false
                }
            }
        }
    }
}
