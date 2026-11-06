import Foundation

public struct InstallerDownloadProgress: Sendable {
    public let receivedBytes: Int64
    public let expectedBytes: Int64?

    public var fractionCompleted: Double? {
        guard let expectedBytes, expectedBytes > 0 else { return nil }
        return min(1, Double(receivedBytes) / Double(expectedBytes))
    }
}

public enum ApplePackageDownloader {
    /// Streams the Apple-signed InstallAssistant package to the selected folder.
    /// No Recovery payload or /Applications staging directory is involved.
    public static func download(
        _ installer: InstallerEntry,
        to destinationFolder: URL,
        progress: (@Sendable (InstallerDownloadProgress) -> Void)? = nil
    ) async throws -> URL {
        guard let sourceURL = installer.downloadURL,
              AppleSoftwareUpdateCatalog.isAppleHTTPS(sourceURL) else {
            throw downloadError("This installer entry has no trusted Apple package URL.")
        }

        var isDirectory: ObjCBool = false
        guard FileManager.default.fileExists(
            atPath: destinationFolder.path,
            isDirectory: &isDirectory
        ), isDirectory.boolValue else {
            throw downloadError("The selected destination folder does not exist.")
        }

        let safeVersion = installer.version.filter { $0.isNumber || $0 == "." || $0 == "-" }
        let fileName = "InstallAssistant-(safeVersion).pkg"
        let finalURL = destinationFolder.appendingPathComponent(fileName)
        let partialURL = destinationFolder.appendingPathComponent(fileName + ".partial")

        guard !FileManager.default.fileExists(atPath: finalURL.path),
              !FileManager.default.fileExists(atPath: partialURL.path) else {
            throw downloadError("A package or partial download with this name already exists. Rename or move it before trying again.")
        }

        let (bytes, response) = try await URLSession.shared.bytes(from: sourceURL)
        guard let http = response as? HTTPURLResponse,
              (200..<300).contains(http.statusCode) else {
            let code = (response as? HTTPURLResponse)?.statusCode ?? -1
            throw downloadError("Apple's package server returned HTTP (code).")
        }

        let expectedSize = installer.expectedBytes ?? (
            response.expectedContentLength > 0 ? response.expectedContentLength : nil
        )
        guard FileManager.default.createFile(atPath: partialURL.path, contents: nil) else {
            throw downloadError("Could not create the partial package file on the selected drive.")
        }

        do {
            let handle = try FileHandle(forWritingTo: partialURL)
            var written: Int64 = 0
            var buffer = Data()
            buffer.reserveCapacity(1024 * 1024)

            do {
                for try await byte in bytes {
                    buffer.append(byte)
                    if buffer.count >= 1024 * 1024 {
                        try handle.write(contentsOf: buffer)
                        written += Int64(buffer.count)
                        buffer.removeAll(keepingCapacity: true)
                        progress?(InstallerDownloadProgress(
                            receivedBytes: written,
                            expectedBytes: expectedSize
                        ))
                    }
                }
                if !buffer.isEmpty {
                    try handle.write(contentsOf: buffer)
                    written += Int64(buffer.count)
                }
                try handle.synchronize()
                try handle.close()
            } catch {
                try? handle.close()
                throw error
            }

            if let expectedSize, expectedSize > 0, written != expectedSize {
                throw downloadError(
                    "The downloaded size does not match Apple's catalog. Expected (expectedSize) bytes, received (written) bytes."
                )
            }

            try FileManager.default.moveItem(at: partialURL, to: finalURL)
            progress?(InstallerDownloadProgress(
                receivedBytes: written,
                expectedBytes: expectedSize ?? written
            ))
            return finalURL
        } catch {
            try? FileManager.default.removeItem(at: partialURL)
            throw error
        }
    }

    private static func downloadError(_ message: String) -> NSError {
        NSError(
            domain: "SimpleHackintoshStudio.AppleDownload",
            code: 1,
            userInfo: [NSLocalizedDescriptionKey: message]
        )
    }
}
