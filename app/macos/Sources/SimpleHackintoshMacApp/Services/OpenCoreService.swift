#if os(macOS)
import CryptoKit
import Foundation
import SimpleHackintoshCore

struct EFIStarterResult {
    let directory: URL
    let openCoreVersion: String
    let validationOutput: String
}

enum OpenCoreServiceError: LocalizedError {
    case invalidReleaseResponse
    case noReleaseAsset
    case missingOrInvalidDigest
    case checksumMismatch
    case missingEFIContents
    case missingSampleConfig
    case outputAlreadyExists(URL)
    case validationFailed(String)

    var errorDescription: String? {
        switch self {
        case .invalidReleaseResponse:
            return "GitHub returned an invalid OpenCore release response."
        case .noReleaseAsset:
            return "The latest stable OpenCore release does not contain the expected RELEASE ZIP."
        case .missingOrInvalidDigest:
            return "GitHub did not provide a usable SHA-256 digest for the OpenCore release asset. The download was not used."
        case .checksumMismatch:
            return "The downloaded OpenCore archive did not match the SHA-256 digest published by GitHub."
        case .missingEFIContents:
            return "The official archive did not contain the expected X64/EFI folder."
        case .missingSampleConfig:
            return "The official archive did not contain Docs/Sample.plist."
        case let .outputAlreadyExists(url):
            return "The output directory already exists: \(url.path). Choose another destination."
        case let .validationFailed(message):
            return "The generated config.plist did not pass the matching ocvalidate tool.\n\(message)"
        }
    }
}

private struct GitHubRelease: Decodable {
    let tagName: String
    let draft: Bool
    let prerelease: Bool
    let assets: [GitHubReleaseAsset]
}

private struct GitHubReleaseAsset: Decodable {
    let name: String
    let browserDownloadUrl: URL
    let digest: String?
}

final class OpenCoreService {
    private let session: URLSession
    private let fileManager = FileManager.default

    init(session: URLSession = .shared) {
        self.session = session
    }

    func buildStarterEFI(in destinationDirectory: URL) async throws -> EFIStarterResult {
        var request = URLRequest(url: URL(string: "https://api.github.com/repos/acidanthera/OpenCorePkg/releases/latest")!)
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        request.setValue("Simple-Hackintosh-macOS-App", forHTTPHeaderField: "User-Agent")
        let (releaseData, response) = try await session.data(for: request)
        guard let http = response as? HTTPURLResponse, (200..<300).contains(http.statusCode) else {
            throw OpenCoreServiceError.invalidReleaseResponse
        }

        let decoder = JSONDecoder()
        decoder.keyDecodingStrategy = .convertFromSnakeCase
        let release = try decoder.decode(GitHubRelease.self, from: releaseData)
        guard !release.draft, !release.prerelease else { throw OpenCoreServiceError.invalidReleaseResponse }
        guard let asset = release.assets.first(where: {
            $0.name.hasPrefix("OpenCore-") && $0.name.hasSuffix("-RELEASE.zip")
        }) else {
            throw OpenCoreServiceError.noReleaseAsset
        }
        guard let publishedDigest = asset.digest, publishedDigest.lowercased().hasPrefix("sha256:") else {
            throw OpenCoreServiceError.missingOrInvalidDigest
        }

        let workingDirectory = fileManager.temporaryDirectory
            .appendingPathComponent("SimpleHackintosh-OpenCore-\(UUID().uuidString)", isDirectory: true)
        try fileManager.createDirectory(at: workingDirectory, withIntermediateDirectories: true)
        defer { try? fileManager.removeItem(at: workingDirectory) }

        let (temporaryDownload, downloadResponse) = try await session.download(from: asset.browserDownloadUrl)
        if let http = downloadResponse as? HTTPURLResponse, !(200..<300).contains(http.statusCode) {
            throw OpenCoreServiceError.invalidReleaseResponse
        }
        let archiveURL = workingDirectory.appendingPathComponent(asset.name)
        try fileManager.moveItem(at: temporaryDownload, to: archiveURL)

        let archiveData = try Data(contentsOf: archiveURL, options: [.mappedIfSafe])
        let actualDigest = SHA256.hash(data: archiveData).map { String(format: "%02x", $0) }.joined()
        guard SHA256Digest.matches(githubDigest: publishedDigest, actualHex: actualDigest) else {
            throw OpenCoreServiceError.checksumMismatch
        }

        let extractionDirectory = workingDirectory.appendingPathComponent("extracted", isDirectory: true)
        try fileManager.createDirectory(at: extractionDirectory, withIntermediateDirectories: true)
        _ = try await ProcessRunner.runChecked(
            "/usr/bin/ditto",
            arguments: ["-x", "-k", archiveURL.path, extractionDirectory.path]
        )

        guard let efiSource = findEFI(in: extractionDirectory) else {
            throw OpenCoreServiceError.missingEFIContents
        }
        guard let sampleConfig = findSampleConfig(in: extractionDirectory) else {
            throw OpenCoreServiceError.missingSampleConfig
        }

        let outputURL = destinationDirectory.appendingPathComponent(
            "SimpleHackintosh-OpenCore-\(release.tagName)-EFI",
            isDirectory: true
        )
        guard !fileManager.fileExists(atPath: outputURL.path) else {
            throw OpenCoreServiceError.outputAlreadyExists(outputURL)
        }

        let efiDestination = outputURL.appendingPathComponent("EFI", isDirectory: true)
        try fileManager.createDirectory(at: outputURL, withIntermediateDirectories: true)
        try fileManager.copyItem(at: efiSource, to: efiDestination)
        let configDestination = efiDestination.appendingPathComponent("OC/config.plist")
        if fileManager.fileExists(atPath: configDestination.path) {
            try fileManager.removeItem(at: configDestination)
        }
        try fileManager.copyItem(at: sampleConfig, to: configDestination)

        let validationOutput: String
        if let validator = findOCValidate(in: extractionDirectory) {
            let result = try await ProcessRunner.run(validator.path, arguments: [configDestination.path])
            guard result.exitCode == 0 else {
                throw OpenCoreServiceError.validationFailed(result.output)
            }
            validationOutput = result.output.isEmpty ? "ocvalidate exited successfully." : result.output
        } else {
            validationOutput = "The official archive did not include an executable ocvalidate; the folder structure was checked only."
        }

        return EFIStarterResult(
            directory: outputURL,
            openCoreVersion: release.tagName,
            validationOutput: validationOutput
        )
    }

    private func findEFI(in root: URL) -> URL? {
        let enumerator = fileManager.enumerator(
            at: root,
            includingPropertiesForKeys: [.isDirectoryKey],
            options: [.skipsHiddenFiles]
        )
        while let url = enumerator?.nextObject() as? URL {
            guard url.lastPathComponent.caseInsensitiveCompare("EFI") == .orderedSame,
                  url.deletingLastPathComponent().lastPathComponent.caseInsensitiveCompare("X64") == .orderedSame,
                  fileManager.fileExists(atPath: url.appendingPathComponent("BOOT/BOOTx64.efi").path),
                  fileManager.fileExists(atPath: url.appendingPathComponent("OC/OpenCore.efi").path) else { continue }
            return url
        }
        return nil
    }

    private func findSampleConfig(in root: URL) -> URL? {
        let enumerator = fileManager.enumerator(at: root, includingPropertiesForKeys: nil, options: [.skipsHiddenFiles])
        while let url = enumerator?.nextObject() as? URL {
            if url.lastPathComponent == "Sample.plist" && url.deletingLastPathComponent().lastPathComponent == "Docs" {
                return url
            }
        }
        return nil
    }

    private func findOCValidate(in root: URL) -> URL? {
        let enumerator = fileManager.enumerator(at: root, includingPropertiesForKeys: [.isRegularFileKey], options: [.skipsHiddenFiles])
        while let url = enumerator?.nextObject() as? URL {
            if url.lastPathComponent == "ocvalidate" && fileManager.isExecutableFile(atPath: url.path) {
                return url
            }
        }
        return nil
    }
}
#endif
