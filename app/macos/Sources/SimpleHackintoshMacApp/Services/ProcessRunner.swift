#if os(macOS)
import Foundation
import SimpleHackintoshCore

struct CommandResult {
    let output: String
    let exitCode: Int32
}

enum ProcessRunner {
    static func run(_ executable: String, arguments: [String]) async throws -> CommandResult {
        try await Task.detached(priority: .userInitiated) {
            let process = Process()
            let pipe = Pipe()
            process.executableURL = URL(fileURLWithPath: executable)
            process.arguments = arguments
            process.standardOutput = pipe
            process.standardError = pipe
            do {
                try process.run()
            } catch {
                throw ProcessRunnerError.launchFailed(executable, error.localizedDescription)
            }
            let data = pipe.fileHandleForReading.readDataToEndOfFile()
            process.waitUntilExit()
            let output = String(data: data, encoding: .utf8) ?? String(decoding: data, as: UTF8.self)
            return CommandResult(output: output, exitCode: process.terminationStatus)
        }.value
    }

    static func runChecked(_ executable: String, arguments: [String]) async throws -> String {
        let result = try await run(executable, arguments: arguments)
        guard result.exitCode == 0 else {
            throw ProcessRunnerError.commandFailed(executable, result.exitCode, String(result.output.suffix(6000)))
        }
        return result.output
    }

    static func runWithAdministratorPrivileges(_ executable: String, arguments: [String]) async throws -> String {
        let command = ([executable] + arguments).map(CommandEscaping.shellArgument).joined(separator: " ")
        let script = "do shell script \(CommandEscaping.appleScriptString(command)) with administrator privileges"
        return try await runChecked("/usr/bin/osascript", arguments: ["-e", script])
    }
}

enum ProcessRunnerError: LocalizedError {
    case launchFailed(String, String)
    case commandFailed(String, Int32, String)

    var errorDescription: String? {
        switch self {
        case let .launchFailed(executable, reason):
            return "Could not start \(executable): \(reason)"
        case let .commandFailed(executable, code, output):
            return "\(URL(fileURLWithPath: executable).lastPathComponent) exited with code \(code).\n\(output)"
        }
    }
}
#endif
