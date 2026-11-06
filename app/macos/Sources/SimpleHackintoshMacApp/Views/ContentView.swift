#if os(macOS)
import AppKit
import SwiftUI
import UniformTypeIdentifiers
import SimpleHackintoshCore

private enum AppSection: String, CaseIterable, Identifiable {
    case overview, installer, efi, hardware
    var id: String { rawValue }
    var title: String {
        switch self {
        case .overview: return "Overview"
        case .installer: return "macOS Installer"
        case .efi: return "EFI Builder"
        case .hardware: return "Hardware"
        }
    }
    var symbol: String {
        switch self {
        case .overview: return "square.grid.2x2"
        case .installer: return "arrow.down.circle"
        case .efi: return "folder.badge.gearshape"
        case .hardware: return "cpu"
        }
    }
}

@MainActor
private final class AppModel: ObservableObject {
    @Published var installerOptions: [InstallerOption] = []
    @Published var selectedVersion = ""
    @Published var installerDestination: URL?
    @Published var installedInstaller: InstalledInstaller?
    @Published var volumes: [InstallerVolume] = []
    @Published var selectedVolumePath = ""
    @Published var efiDestination: URL?
    @Published var generatedEFI: URL?
    @Published var hardwareReport = "Select Detect hardware to collect this Mac's local hardware inventory."
    @Published var statusMessage = "Ready"
    @Published var logText = ""
    @Published var isBusy = false
    @Published var alertMessage: String?

    private let installerService = MacOSInstallerService()
    private let openCoreService = OpenCoreService()
    private let hardwareService = HardwareReportService()

    var selectedInstaller: InstallerOption? {
        installerOptions.first { $0.version == selectedVersion }
    }
    var selectedVolume: InstallerVolume? {
        volumes.first { $0.id == selectedVolumePath }
    }

    func refreshInstallerCatalog() async {
        guard !isBusy else { return }
        isBusy = true
        statusMessage = "Checking Apple's full-installer catalog…"
        defer { isBusy = false }
        do {
            installerOptions = try await installerService.listFullInstallers()
            if !installerOptions.contains(where: { $0.version == selectedVersion }) {
                selectedVersion = installerOptions.first?.version ?? ""
            }
            statusMessage = "Found \(installerOptions.count) full installer version(s)."
        } catch {
            statusMessage = "Installer catalog check failed."
            alertMessage = error.localizedDescription
        }
    }

    func refreshVolumes() {
        volumes = installerService.ejectableVolumes()
        if !volumes.contains(where: { $0.id == selectedVolumePath }) {
            selectedVolumePath = volumes.first?.id ?? ""
        }
    }

    func downloadInstaller() async {
        guard !isBusy else { return }
        guard let option = selectedInstaller else {
            alertMessage = "Choose an installer version first."
            return
        }
        guard let destination = installerDestination else {
            alertMessage = "Choose where the downloaded installer app should be copied."
            return
        }
        isBusy = true
        statusMessage = "Downloading the full macOS installer from Apple…"
        logText = "Apple Software Update is downloading version \(option.version). macOS first places the installer app in /Applications, then this app copies it to your selected destination.\n"
        defer { isBusy = false }
        do {
            let result = try await installerService.downloadFullInstaller(option, to: destination)
            installedInstaller = result
            logText += "\nInstaller copied to:\n\(result.appURL.path)\n"
            statusMessage = "Full installer downloaded and copied."
        } catch {
            statusMessage = "Download or copy failed."
            alertMessage = error.localizedDescription
            logText += "\n\(error.localizedDescription)\n"
        }
    }

    func createInstallMedia() async {
        guard !isBusy else { return }
        guard let installer = installedInstaller, let volume = selectedVolume else {
            alertMessage = "Choose a downloaded installer and an external USB volume."
            return
        }
        isBusy = true
        statusMessage = "Preparing the bootable installer…"
        logText += "\nCreating install media on \(volume.name) at \(volume.url.path). macOS will ask for administrator authorization.\n"
        defer { isBusy = false }
        do {
            let output = try await installerService.createBootableInstaller(installer: installer, on: volume)
            logText += output + "\n"
            statusMessage = "Bootable installer creation finished."
            refreshVolumes()
        } catch {
            statusMessage = "Bootable installer creation failed or was cancelled."
            alertMessage = error.localizedDescription
            logText += "\n\(error.localizedDescription)\n"
        }
    }

    func buildEFIStarter() async {
        guard !isBusy else { return }
        guard let destination = efiDestination else {
            alertMessage = "Choose an output folder for the new EFI starter."
            return
        }
        isBusy = true
        statusMessage = "Downloading and verifying the official OpenCore release…"
        logText = "Fetching the latest stable OpenCore RELEASE archive from acidanthera/OpenCorePkg. GitHub's published SHA-256 digest is checked before extraction.\n"
        defer { isBusy = false }
        do {
            let result = try await openCoreService.buildStarterEFI(in: destination)
            generatedEFI = result.directory
            logText += "\nOpenCore: \(result.openCoreVersion)\nEFI folder: \(result.directory.path)\n\nValidator output:\n\(result.validationOutput)\n"
            statusMessage = "EFI starter created. Hardware customization is still required."
        } catch {
            statusMessage = "EFI starter could not be created."
            alertMessage = error.localizedDescription
            logText += "\n\(error.localizedDescription)\n"
        }
    }

    func detectHardware() async {
        guard !isBusy else { return }
        isBusy = true
        statusMessage = "Collecting local hardware information…"
        defer { isBusy = false }
        do {
            hardwareReport = try await hardwareService.createReport()
            statusMessage = "Hardware report ready."
        } catch {
            statusMessage = "Hardware inventory failed."
            alertMessage = error.localizedDescription
        }
    }
}

struct ContentView: View {
    @StateObject private var model = AppModel()
    @State private var selection: AppSection? = .overview
    @State private var confirmErase = false

    private let accent = Color(red: 0.72, green: 0.94, blue: 0.42)
    private let background = Color(red: 0.055, green: 0.075, blue: 0.09)
    private let panel = Color(red: 0.085, green: 0.115, blue: 0.14)

    var body: some View {
        NavigationSplitView {
            List(selection: $selection) {
                Section("WORKSPACE") {
                    ForEach(AppSection.allCases) { section in
                        Label(section.title, systemImage: section.symbol)
                            .tag(Optional(section))
                    }
                }
            }
            .listStyle(.sidebar)
            .navigationTitle("Simple Hackintosh")
            .safeAreaInset(edge: .bottom) {
                VStack(alignment: .leading, spacing: 6) {
                    Label("LOCAL PROCESSING", systemImage: "lock.shield")
                        .font(.system(size: 10, weight: .bold, design: .monospaced))
                        .foregroundStyle(accent)
                    Text("Your EFI and hardware report stay on this Mac. OpenCore is downloaded from its official GitHub release.")
                        .font(.caption)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                    Text("MAC APP · 0.1.0")
                        .font(.system(size: 9, design: .monospaced))
                        .foregroundStyle(.tertiary)
                }
                .padding(14)
                .frame(maxWidth: .infinity, alignment: .leading)
                .background(panel)
                .clipShape(RoundedRectangle(cornerRadius: 12))
                .padding(12)
            }
        } detail: {
            Group {
                switch selection ?? .overview {
                case .overview: overviewPage
                case .installer: installerPage
                case .efi: efiPage
                case .hardware: hardwarePage
                }
            }
            .frame(maxWidth: .infinity, maxHeight: .infinity)
            .background(background)
        }
        .frame(minWidth: 980, minHeight: 680)
        .preferredColorScheme(.dark)
        .alert("Operation needs attention", isPresented: Binding(
            get: { model.alertMessage != nil },
            set: { if !$0 { model.alertMessage = nil } }
        )) {
            Button("OK", role: .cancel) { model.alertMessage = nil }
        } message: {
            Text(model.alertMessage ?? "")
        }
        .confirmationDialog(
            "Erase \(model.selectedVolume?.name ?? "the selected USB") and create the macOS installer?",
            isPresented: $confirmErase,
            titleVisibility: .visible
        ) {
            Button("Erase USB and create installer", role: .destructive) {
                Task { await model.createInstallMedia() }
            }
            Button("Cancel", role: .cancel) { }
        } message: {
            Text("Everything currently stored on \(model.selectedVolume?.url.path ?? "the selected volume") will be erased. Verify the drive name and capacity before continuing.")
        }
        .task {
            await model.refreshInstallerCatalog()
            model.refreshVolumes()
        }
    }

    private var overviewPage: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                pageHeader(
                    eyebrow: "OPENCORE / INSTALLER WORKSPACE",
                    title: "Prepare macOS.\nBuild with evidence.",
                    subtitle: "A native macOS workspace for full installers, OpenCore EFI starters, and hardware diagnostics."
                )
                HStack(spacing: 14) {
                    summaryCard("Full macOS installer", detail: "Apple Software Update catalog", symbol: "arrow.down.circle", tint: accent) {
                        selection = .installer
                    }
                    summaryCard("OpenCore EFI starter", detail: "Official release · SHA-256 checked", symbol: "folder.badge.gearshape", tint: .cyan) {
                        selection = .efi
                    }
                    summaryCard("Hardware report", detail: "System Profiler · local only", symbol: "cpu", tint: .orange) {
                        selection = .hardware
                    }
                }
                infoPanel(title: "What this first Mac build does", symbol: "checkmark.seal") {
                    bullet("Lists full macOS installers exposed by Apple's Software Update catalog.")
                    bullet("Downloads the selected full installer, then copies the installer app to your chosen folder or external drive.")
                    bullet("Uses Apple's createinstallmedia tool to create a bootable USB, with an explicit erase confirmation and administrator prompt.")
                    bullet("Downloads the official OpenCore RELEASE archive, verifies GitHub's published SHA-256 digest, and creates a starter EFI folder.")
                }
                warningPanel(
                    title: "EFI starter is not a ready-to-boot EFI",
                    detail: "The generated folder contains official OpenCore files and Sample.plist. It still needs device-specific ACPI, Kexts, drivers, config.plist settings, and SMBIOS review. Never boot it as a finished configuration without completing those steps."
                )
            }
            .padding(30)
        }
    }

    private var installerPage: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                pageHeader(
                    eyebrow: "APPLE SOFTWARE UPDATE",
                    title: "macOS Installer",
                    subtitle: "Download the full installer inside this app — no Recovery download workflow is used here."
                )
                infoPanel(title: "1 · Choose a full installer", symbol: "arrow.down.circle") {
                    Text("The catalog is provided by macOS and may show only versions available to this Mac. The download is handled by Apple's softwareupdate tool.")
                        .font(.subheadline).foregroundStyle(.secondary)
                    HStack {
                        Picker("macOS version", selection: $model.selectedVersion) {
                            ForEach(model.installerOptions) { option in
                                Text("\(option.title) · \(option.version)").tag(option.version)
                            }
                        }
                        .labelsHidden()
                        .frame(maxWidth: 430)
                        Button("Refresh catalog", systemImage: "arrow.clockwise") {
                            Task { await model.refreshInstallerCatalog() }
                        }
                        .disabled(model.isBusy)
                    }
                    HStack {
                        Label(model.installerDestination?.path ?? "No destination selected", systemImage: "externaldrive")
                            .font(.caption).foregroundStyle(.secondary).lineLimit(2)
                        Spacer()
                        Button("Choose destination…") {
                            if let url = chooseFolder("Choose where to store the downloaded installer app") {
                                model.installerDestination = url
                            }
                        }
                    }
                    Button {
                        Task { await model.downloadInstaller() }
                    } label: {
                        Label("Download full installer", systemImage: "arrow.down.to.line")
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(accent)
                    .disabled(model.isBusy || model.selectedInstaller == nil || model.installerDestination == nil)
                }
                infoPanel(title: "2 · Create a bootable USB", symbol: "externaldrive.badge.plus") {
                    Text("Select an external, ejectable volume. Apple's createinstallmedia utility erases the selected volume and writes the installer.")
                        .font(.subheadline).foregroundStyle(.secondary)
                    HStack {
                        Picker("External volume", selection: $model.selectedVolumePath) {
                            ForEach(model.volumes) { volume in
                                Text("\(volume.name) · \(volume.capacityDescription)").tag(volume.id)
                            }
                        }
                        .labelsHidden()
                        .frame(maxWidth: 430)
                        Button("Refresh drives", systemImage: "arrow.clockwise") { model.refreshVolumes() }
                    }
                    HStack {
                        Button(role: .destructive) {
                            confirmErase = true
                        } label: {
                            Label("Erase and create installer…", systemImage: "externaldrive.badge.exclamationmark")
                        }
                        .disabled(model.isBusy || model.installedInstaller == nil || model.selectedVolume == nil)
                        if let installed = model.installedInstaller {
                            Button("Reveal installer in Finder", systemImage: "folder") {
                                NSWorkspace.shared.activateFileViewerSelecting([installed.appURL])
                            }
                        }
                    }
                    if let installed = model.installedInstaller {
                        Text("Selected installer: \(installed.title) \(installed.version) · \(installed.appURL.path)")
                            .font(.caption).foregroundStyle(.secondary).textSelection(.enabled)
                    }
                }
                activityPanel
            }
            .padding(30)
        }
    }

    private var efiPage: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                pageHeader(
                    eyebrow: "ACIDANTHERA / OPENCOREPKG",
                    title: "EFI Builder",
                    subtitle: "Create a clean OpenCore EFI starter from the latest stable official release."
                )
                infoPanel(title: "Build an EFI starter", symbol: "shippingbox") {
                    Text("The app downloads OpenCorePkg from acidanthera, checks the SHA-256 digest published in GitHub release metadata, extracts the x64 EFI files, copies Docs/Sample.plist to config.plist, and runs the matching ocvalidate when available.")
                        .font(.subheadline).foregroundStyle(.secondary)
                    HStack {
                        Label(model.efiDestination?.path ?? "No output folder selected", systemImage: "folder")
                            .font(.caption).foregroundStyle(.secondary).lineLimit(2)
                        Spacer()
                        Button("Choose output folder…") {
                            if let url = chooseFolder("Choose where the new EFI starter folder should be created") {
                                model.efiDestination = url
                            }
                        }
                    }
                    Button {
                        Task { await model.buildEFIStarter() }
                    } label: {
                        Label("Build EFI starter", systemImage: "hammer")
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(accent)
                    .disabled(model.isBusy || model.efiDestination == nil)
                    if let generated = model.generatedEFI {
                        HStack {
                            Label(generated.path, systemImage: "checkmark.circle.fill")
                                .font(.caption).foregroundStyle(.green).textSelection(.enabled)
                            Spacer()
                            Button("Reveal", systemImage: "folder") {
                                NSWorkspace.shared.activateFileViewerSelecting([generated])
                            }
                        }
                    }
                }
                warningPanel(
                    title: "Before using this EFI",
                    detail: "This is a starter folder, not a personalized configuration. Add only the ACPI tables, Kexts, UEFI drivers, quirks, and device properties that match your exact hardware. Create and verify a backup before replacing any existing EFI. The app does not overwrite a pre-existing output directory or your current boot partition."
                )
                activityPanel
            }
            .padding(30)
        }
    }

    private var hardwarePage: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                pageHeader(
                    eyebrow: "SYSTEM INFORMATION",
                    title: "Hardware Inspector",
                    subtitle: "Read hardware details from this Mac and prepare a privacy-conscious report."
                )
                HStack {
                    Button {
                        Task { await model.detectHardware() }
                    } label: {
                        Label("Detect hardware", systemImage: "cpu")
                    }
                    .buttonStyle(.borderedProminent)
                    .tint(accent)
                    .disabled(model.isBusy)
                    Button("Copy report", systemImage: "doc.on.doc") {
                        NSPasteboard.general.clearContents()
                        NSPasteboard.general.setString(model.hardwareReport, forType: .string)
                    }
                    Button("Save report…", systemImage: "square.and.arrow.down") {
                        saveHardwareReport()
                    }
                }
                TextEditor(text: $model.hardwareReport)
                    .font(.system(.caption, design: .monospaced))
                    .scrollContentBackground(.hidden)
                    .padding(12)
                    .background(panel)
                    .clipShape(RoundedRectangle(cornerRadius: 12))
                    .frame(minHeight: 430)
                Text("Serial-number and hardware-UUID fields are removed from the displayed report. Review the report before sharing it.")
                    .font(.caption).foregroundStyle(.secondary)
            }
            .padding(30)
        }
    }

    private var activityPanel: some View {
        infoPanel(title: "Activity", symbol: "text.alignleft") {
            HStack(spacing: 8) {
                if model.isBusy { ProgressView().controlSize(.small) }
                Text(model.statusMessage)
                    .font(.subheadline.weight(.medium))
                    .foregroundStyle(model.isBusy ? accent : .secondary)
            }
            TextEditor(text: $model.logText)
                .font(.system(.caption, design: .monospaced))
                .scrollContentBackground(.hidden)
                .padding(10)
                .background(Color.black.opacity(0.22))
                .clipShape(RoundedRectangle(cornerRadius: 8))
                .frame(minHeight: 120, maxHeight: 210)
                .disabled(true)
        }
    }

    private func pageHeader(eyebrow: String, title: String, subtitle: String) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            Text(eyebrow)
                .font(.system(size: 10, weight: .bold, design: .monospaced))
                .foregroundStyle(accent)
            Text(title)
                .font(.system(size: 32, weight: .bold, design: .rounded))
                .foregroundStyle(.primary)
                .fixedSize(horizontal: false, vertical: true)
            Text(subtitle)
                .font(.subheadline)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .padding(.bottom, 2)
    }

    private func summaryCard(_ title: String, detail: String, symbol: String, tint: Color, action: @escaping () -> Void) -> some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 12) {
                Image(systemName: symbol).font(.system(size: 21, weight: .medium)).foregroundStyle(tint)
                Text(title).font(.headline).foregroundStyle(.primary)
                Text(detail).font(.caption).foregroundStyle(.secondary).multilineTextAlignment(.leading)
            }
            .frame(maxWidth: .infinity, minHeight: 118, alignment: .leading)
            .padding(18)
            .background(panel)
            .clipShape(RoundedRectangle(cornerRadius: 14))
        }
        .buttonStyle(.plain)
    }

    private func infoPanel<Content: View>(title: String, symbol: String, @ViewBuilder content: () -> Content) -> some View {
        VStack(alignment: .leading, spacing: 16) {
            Label(title, systemImage: symbol).font(.headline)
            content()
        }
        .padding(20)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(panel)
        .clipShape(RoundedRectangle(cornerRadius: 14))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.white.opacity(0.06), lineWidth: 1))
    }

    private func warningPanel(title: String, detail: String) -> some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: "exclamationmark.triangle.fill").foregroundStyle(.yellow).padding(.top, 2)
            VStack(alignment: .leading, spacing: 6) {
                Text(title).font(.headline)
                Text(detail).font(.subheadline).foregroundStyle(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color.yellow.opacity(0.07))
        .clipShape(RoundedRectangle(cornerRadius: 12))
        .overlay(RoundedRectangle(cornerRadius: 12).stroke(Color.yellow.opacity(0.18), lineWidth: 1))
    }

    private func bullet(_ value: String) -> some View {
        HStack(alignment: .top, spacing: 9) {
            Image(systemName: "checkmark.circle.fill").foregroundStyle(accent).padding(.top, 2)
            Text(value).font(.subheadline).foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
    }

    private func chooseFolder(_ message: String) -> URL? {
        let panel = NSOpenPanel()
        panel.message = message
        panel.prompt = "Choose Folder"
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false
        return panel.runModal() == .OK ? panel.url : nil
    }

    private func saveHardwareReport() {
        let panel = NSSavePanel()
        panel.nameFieldStringValue = "simple-hackintosh-mac-hardware-report.txt"
        panel.allowedContentTypes = [.plainText]
        guard panel.runModal() == .OK, let url = panel.url else { return }
        do {
            try model.hardwareReport.write(to: url, atomically: true, encoding: .utf8)
        } catch {
            model.alertMessage = error.localizedDescription
        }
    }
}
#endif
