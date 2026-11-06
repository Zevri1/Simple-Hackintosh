#if os(macOS)
import Foundation
import SimpleHackintoshCore

struct InstallerVolume: Identifiable, Hashable {
    let url: URL
    let name: String
    let capacity: Int64?
    var id: String { url.path }
    var capacityDescription: String {
        guard let capacity else { return "Capacity unavailable" }
        return ByteCountFormatter.string(fromByteCount: capacity, countStyle: .file)
    }
}

struct InstalledInstaller {
    let appURL: URL
    let version: String
    let title: String
}

enum MacOSInstallerError: LocalizedError {
    case unsupportedOperatingSystem
    case noInstallersFound
    case installerApplicationNotFound(String)
    case installerAlreadyExists(URL)
    case missingCreateInstallMedia(URL)
    case selectedVolumeIsNotEjectable
    case installerIsStoredOnTargetVolume

    var errorDescription: String? {
        switch self {
        case .unsupportedOperatingSystem:
            return "Full-installer downloads require macOS Catalina 10.15 or newer."
        case .noInstallersFound:
            return "Apple did not return any full installers for this Mac. Check the macOS version and network connection."
        case let .installerApplicationNotFound(name):
            return "The download completed, but the expected installer app was not found in /Applications: \(name)"
        case let .installerAlreadyExists(url):
            return "A file already exists at the destination: \(url.path). Choose another destination or rename the existing item."
        case let .missingCreateInstallMedia(url):
            return "The selected installer does not contain createinstallmedia: \(url.path)"
        case .selectedVolumeIsNotEjectable:
            return "For safety, this action is limited to an ejectable external volume."
        case .installerIsStoredOnTargetVolume:
            return "The installer app is stored on the same drive you selected to erase. Move the installer to a different drive or folder, then choose the USB again."
        }
    }
}

final class MacOSInstallerService {
    func listFullInstallers() async throws -> [InstallerOption] {
        let version = ProcessInfo.processInfo.operatingSystemVersion
        guard version.majorVersion > 10 || (version.majorVersion == 10 && version.minorVersion >= 15) else {
            throw MacOSInstallerError.unsupportedOperatingSystem
        }
        let output = try await ProcessRunner.runChecked(
            "/usr/sbin/softwareupdate",
            arguments: ["--list-full-installers"]
        )
        let options = InstallerCatalogParser.parse(output)
        guard !options.isEmpty else { throw MacOSInstallerError.noInstallersFound }
        return options
    }

    func downloadFullInstaller(_ option: InstallerOption, to destinationDirectory: URL) async throws -> InstalledInstaller {
        _ = try await ProcessRunner.runChecked(
            "/usr/sbin/softwareupdate",
            arguments: ["--fetch-full-installer", "--full-installer-version", option.version]
        )
        let expectedName = "Install \(option.title).app"
        let applications = URL(fileURLWithPath: "/Applications", isDirectory: true)
        let expectedURL = applications.appendingPathComponent(expectedName, isDirectory: true)
        let sourceURL: URL
        if FileManager.default.fileExists(atPath: expectedURL.path) {
            sourceURL = expectedURL
        } else {
            let candidates = (try? FileManager.default.contentsOfDirectory(
                at: applications,
                includingPropertiesForKeys: [.contentModificationDateKey],
                options: [.skipsHiddenFiles]
            ))?.filter {
                $0.pathExtension == "app" && $0.lastPathComponent.hasPrefix("Install macOS ")
            } ?? []
            let newest = candidates.sorted {
                let lhs = (try? $0.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
                let rhs = (try? $1.resourceValues(forKeys: [.contentModificationDateKey]).contentModificationDate) ?? .distantPast
                return lhs > rhs
            }
            guard let first = newest.first else {
                throw MacOSInstallerError.installerApplicationNotFound(expectedName)
            }
            sourceURL = first
        }
        let destinationURL = destinationDirectory.appendingPathComponent(sourceURL.lastPathComponent, isDirectory: true)
        guard !FileManager.default.fileExists(atPath: destinationURL.path) else {
            throw MacOSInstallerError.installerAlreadyExists(destinationURL)
        }
        _ = try await ProcessRunner.runChecked("/usr/bin/ditto", arguments: [sourceURL.path, destinationURL.path])
        return InstalledInstaller(appURL: destinationURL, version: option.version, title: option.title)
    }

    func ejectableVolumes() -> [InstallerVolume] {
        let keys: [URLResourceKey] = [.volumeNameKey, .volumeIsEjectableKey, .volumeTotalCapacityKey]
        let urls = FileManager.default.mountedVolumeURLs(
            includingResourceValuesForKeys: keys,
            options: [.skipHiddenVolumes]
        ) ?? []
        return urls.compactMap { url in
            guard let values = try? url.resourceValues(forKeys: Set(keys)),
                  values.volumeIsEjectable == true else { return nil }
            return InstallerVolume(
                url: url,
                name: values.volumeName ?? url.lastPathComponent,
                capacity: values.volumeTotalCapacity.map(Int64.init)
            )
        }.sorted { $0.name.localizedCaseInsensitiveCompare($1.name) == .orderedAscending }
    }

    func createBootableInstaller(installer: InstalledInstaller, on volume: InstallerVolume) async throws -> String {
        guard FileManager.default.fileExists(atPath: volume.url.path) else {
            throw MacOSInstallerError.selectedVolumeIsNotEjectable
        }
        let values = try volume.url.resourceValues(forKeys: [.volumeIsEjectableKey])
        guard values.volumeIsEjectable == true else { throw MacOSInstallerError.selectedVolumeIsNotEjectable }
        guard !PathSafety.isInside(childPath: installer.appURL.path, parentPath: volume.url.path) else {
            throw MacOSInstallerError.installerIsStoredOnTargetVolume
        }
        let createInstallMedia = installer.appURL
            .appendingPathComponent("Contents/Resources/createinstallmedia")
        guard FileManager.default.isExecutableFile(atPath: createInstallMedia.path) else {
            throw MacOSInstallerError.missingCreateInstallMedia(createInstallMedia)
        }
        return try await ProcessRunner.runWithAdministratorPrivileges(
            createInstallMedia.path,
            arguments: ["--volume", volume.url.path, "--nointeraction"]
        )
    }
}
#endif
