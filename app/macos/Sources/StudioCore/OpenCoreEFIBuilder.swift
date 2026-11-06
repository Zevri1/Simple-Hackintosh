import CryptoKit
import Foundation

private struct GitHubRelease: Decodable {
    let tag_name: String
    let draft: Bool
    let prerelease: Bool
    let assets: [GitHubAsset]
}

private struct GitHubAsset: Decodable {
    let name: String
    let browser_download_url: URL
    let digest: String?
}

public struct EFIBuildResult: Sendable {
    public let version: String
    public let folderURL: URL
    public let sha256: String
}

public enum OpenCoreEFIBuilder {
    private static let releaseAPI = URL(
        string: "https://api.github.com/repos/acidanthera/OpenCorePkg/releases/latest"
    )!

    public static func buildStarterEFI(to destination: URL) async throws -> EFIBuildResult {
        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(atPath: destination.path, isDirectory: &isDirectory),
              isDirectory.boolValue else {
            throw makeError("Choose an existing destination folder.")
        }

        let targetEFI = destination.appendingPathComponent("EFI", isDirectory: true)
        guard !FileManager.default.fileExists(atPath: targetEFI.path) else {
            throw makeError("An EFI folder already exists at the destination. Back it up or choose another folder.")
        }

        var request = URLRequest(url: releaseAPI)
        request.setValue("application/vnd.github+json", forHTTPHeaderField: "Accept")
        request.setValue("Simple-Hackintosh-Studio", forHTTPHeaderField: "User-Agent")
        let (releaseData, _) = try await URLSession.shared.data(for: request)
        let release = try JSONDecoder().decode(GitHubRelease.self, from: releaseData)

        guard !release.draft && !release.prerelease else {
            throw makeError("GitHub did not return a stable OpenCore release.")
        }
        guard let asset = release.assets.first(where: { $0.name.hasSuffix("-RELEASE.zip") }) else {
            throw makeError("The latest OpenCore release has no RELEASE ZIP asset.")
        }
        guard let digest = asset.digest, digest.lowercased().hasPrefix("sha256:") else {
            throw makeError("GitHub did not provide a SHA-256 digest. Download stopped.")
        }
        guard asset.browser_download_url.host == "github.com",
              asset.browser_download_url.path.hasPrefix("/acidanthera/OpenCorePkg/releases/download/") else {
            throw makeError("The download URL did not match the official OpenCore release path.")
        }

        let expectedHash = String(digest.dropFirst("sha256:".count)).lowercased()
        let temporaryRoot = FileManager.default.temporaryDirectory
            .appendingPathComponent("SimpleHackintoshStudio-\(UUID().uuidString)", isDirectory: true)
        try FileManager.default.createDirectory(at: temporaryRoot, withIntermediateDirectories: true)
        defer { try? FileManager.default.removeItem(at: temporaryRoot) }

        let (downloadURL, _) = try await URLSession.shared.download(from: asset.browser_download_url)
        let localArchive = temporaryRoot.appendingPathComponent(asset.name)
        try FileManager.default.moveItem(at: downloadURL, to: localArchive)

        let actualHash = try sha256(of: localArchive)
        guard actualHash == expectedHash else {
            throw makeError("OpenCore archive checksum mismatch. No EFI folder was created.")
        }

        let extraction = temporaryRoot.appendingPathComponent("extracted", isDirectory: true)
        try FileManager.default.createDirectory(at: extraction, withIntermediateDirectories: true)
        _ = try CommandRunner.run(
            "/usr/bin/ditto",
            arguments: ["-x", "-k", localArchive.path, extraction.path]
        )

        let sourceEFI = extraction.appendingPathComponent("X64/EFI", isDirectory: true)
        let bootFile = sourceEFI.appendingPathComponent("BOOT/BOOTx64.efi")
        let coreFile = sourceEFI.appendingPathComponent("OC/OpenCore.efi")
        let sampleCandidates = [
            extraction.appendingPathComponent("Docs/Sample.plist"),
            extraction.appendingPathComponent("X64/Docs/Sample.plist")
        ]
        guard FileManager.default.fileExists(atPath: bootFile.path),
              FileManager.default.fileExists(atPath: coreFile.path),
              let sample = sampleCandidates.first(where: { FileManager.default.fileExists(atPath: $0.path) }) else {
            throw makeError("The archive is missing expected x64 EFI files or Docs/Sample.plist.")
        }

        do {
            try FileManager.default.copyItem(at: sourceEFI, to: targetEFI)
            try FileManager.default.copyItem(
                at: sample,
                to: targetEFI.appendingPathComponent("OC/config.plist")
            )
            try "OpenCore \(release.tag_name) — official RELEASE package\n".write(
                to: targetEFI.appendingPathComponent("OpenCore-Version.txt"),
                atomically: true,
                encoding: .utf8
            )

            let warning = """
            SIMPLE HACKINTOSH STUDIO — EFI STARTER

            OpenCore version: \(release.tag_name)
            Archive SHA-256: \(actualHash)

            IMPORTANT: This is a verified OpenCore starter folder, NOT a ready-to-boot
            computer-specific EFI. Sample.plist is a template. Configure ACPI, Drivers,
            Kexts, Kernel, PlatformInfo/SMBIOS, NVRAM and UEFI for the exact target
            computer, then validate with ocvalidate from this same OpenCore release.

            Do not copy another computer's SMBIOS identifiers. Keep a backup of any
            existing EFI and test from a removable drive before changing internal boot files.
            """
            try warning.write(
                to: targetEFI.appendingPathComponent("READ-ME-FIRST.txt"),
                atomically: true,
                encoding: .utf8
            )
        } catch {
            try? FileManager.default.removeItem(at: targetEFI)
            throw error
        }

        return EFIBuildResult(version: release.tag_name, folderURL: targetEFI, sha256: actualHash)
    }

    private static func sha256(of fileURL: URL) throws -> String {
        let handle = try FileHandle(forReadingFrom: fileURL)
        defer { try? handle.close() }
        var hasher = SHA256()
        while true {
            let chunk = try handle.read(upToCount: 1024 * 1024) ?? Data()
            if chunk.isEmpty { break }
            hasher.update(data: chunk)
        }
        return hasher.finalize().map { String(format: "%02x", $0) }.joined()
    }

    private static func makeError(_ message: String) -> NSError {
        NSError(
            domain: "SimpleHackintoshStudio.OpenCore",
            code: 1,
            userInfo: [NSLocalizedDescriptionKey: message]
        )
    }
}
