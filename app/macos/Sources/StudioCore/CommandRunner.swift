import Foundation

public struct CommandOutput: Sendable {
    public let text: String
    public let exitCode: Int32

    public init(text: String, exitCode: Int32) {
        self.text = text
        self.exitCode = exitCode
    }
}

public enum CommandRunnerError: LocalizedError {
    case commandFailed(path: String, exitCode: Int32, output: String)

    public var errorDescription: String? {
        switch self {
        case let .commandFailed(path, exitCode, output):
            let detail = output.trimmingCharacters(in: .whitespacesAndNewlines)
            return detail.isEmpty
                ? "Command failed (\(exitCode)): \(path)"
                : "Command failed (\(exitCode)): \(path)\n\(detail)"
        }
    }
}

public enum CommandRunner {
    /// Runs a fixed executable with an argument array; no shell is involved.
    public static func run(
        _ executablePath: String,
        arguments: [String] = [],
        requireSuccess: Bool = true
    ) throws -> CommandOutput {
        let process = Process()
        let outputPipe = Pipe()
        process.executableURL = URL(fileURLWithPath: executablePath)
        process.arguments = arguments
        process.standardOutput = outputPipe
        process.standardError = outputPipe
        try process.run()

        let data = outputPipe.fileHandleForReading.readDataToEndOfFile()
        process.waitUntilExit()
        let text = String(data: data, encoding: .utf8) ?? String(decoding: data, as: UTF8.self)
        let result = CommandOutput(text: text, exitCode: process.terminationStatus)

        if requireSuccess && result.exitCode != 0 {
            throw CommandRunnerError.commandFailed(
                path: executablePath,
                exitCode: result.exitCode,
                output: result.text
            )
        }
        return result
    }
}
