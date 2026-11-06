#if os(macOS)
import SwiftUI

@main
struct SimpleHackintoshMacApp: App {
    var body: some Scene {
        WindowGroup {
            ContentView()
                .preferredColorScheme(.dark)
        }
        .windowStyle(.automatic)
        .defaultSize(width: 1120, height: 760)
    }
}
#else
import Foundation

@main
enum SimpleHackintoshMacApp {
    static func main() {
        print("Simple Hackintosh for macOS must be built and run on macOS.")
    }
}
#endif
