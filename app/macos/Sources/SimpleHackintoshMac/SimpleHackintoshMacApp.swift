import SwiftUI

@main
struct SimpleHackintoshMacApp: App {
    var body: some Scene {
        WindowGroup {
            MainView()
                .frame(minWidth: 1080, minHeight: 720)
                .preferredColorScheme(.dark)
                .tint(Color(red: 0.72, green: 0.95, blue: 0.42))
        }
        .windowStyle(.hiddenTitleBar)
    }
}
