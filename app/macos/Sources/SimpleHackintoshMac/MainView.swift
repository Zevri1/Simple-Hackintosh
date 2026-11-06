import AppKit
import SwiftUI

private enum AppSection: String, CaseIterable, Identifiable {
    case overview
    case installer
    case efiBuilder
    case efiChecker
    case hardware

    var id: String { rawValue }

    var title: String {
        switch self {
        case .overview: return "Overview"
        case .installer: return "macOS Installer"
        case .efiBuilder: return "EFI Builder"
        case .efiChecker: return "EFI Checker"
        case .hardware: return "Hardware"
        }
    }

    var icon: String {
        switch self {
        case .overview: return "square.grid.2x2"
        case .installer: return "externaldrive"
        case .efiBuilder: return "hammer"
        case .efiChecker: return "checkmark.shield"
        case .hardware: return "cpu"
        }
    }

    var subtitle: String {
        switch self {
        case .overview: return "A visual workspace for preparing and checking OpenCore."
        case .installer: return "Download the full macOS installer and prepare a bootable USB."
        case .efiBuilder: return "Fetch the official OpenCore release and create a clean EFI starter."
        case .efiChecker: return "Inspect the folder structure and enabled file references."
        case .hardware: return "Review a local inventory of this Mac's hardware."
        }
    }
}

struct MainView: View {
    @StateObject private var model = AppModel()
    @State private var selection: AppSection? = .overview

    var body: some View {
        NavigationSplitView {
            VStack(alignment: .leading, spacing: 18) {
                VStack(alignment: .leading, spacing: 4) {
                    Text("SH_")
                        .font(.system(size: 30, weight: .black, design: .rounded))
                        .foregroundColor(.accentColor)
                    Text("SIMPLE HACKINTOSH")
                        .font(.system(size: 11, weight: .bold, design: .monospaced))
                        .foregroundColor(.white)
                    Text("DESKTOP STUDIO")
                        .font(.system(size: 9, weight: .medium, design: .monospaced))
                        .foregroundColor(.secondary)
                }
                .padding(.horizontal, 16)
                .padding(.top, 20)

                List(selection: $selection) {
                    Section("WORKSPACE") {
                        ForEach(AppSection.allCases) { section in
                            Label(section.title, systemImage: section.icon)
                                .tag(section)
                        }
                    }
                }
                .listStyle(.sidebar)

                VStack(alignment: .leading, spacing: 8) {
                    Label("LOCAL WORKSPACE", systemImage: "lock.shield")
                        .font(.system(size: 10, weight: .bold, design: .monospaced))
                        .foregroundColor(Color.green)
                    Text("Hardware and EFI files are processed on this Mac. Nothing is uploaded.")
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                    Text("MAC APP · 0.1.0")
                        .font(.system(size: 9, weight: .medium, design: .monospaced))
                        .foregroundColor(.secondary)
                }
                .padding(14)
                .background(Color.white.opacity(0.045), in: RoundedRectangle(cornerRadius: 12))
                .padding(.horizontal, 12)
                .padding(.bottom, 12)
            }
            .background(Color(red: 0.055, green: 0.075, blue: 0.09))
            .navigationSplitViewColumnWidth(min: 210, ideal: 235, max: 270)
        } detail: {
            VStack(alignment: .leading, spacing: 0) {
                HStack(alignment: .center) {
                    VStack(alignment: .leading, spacing: 5) {
                        Text(selection?.title ?? "Overview")
                            .font(.system(size: 25, weight: .bold))
                        Text(selection?.subtitle ?? AppSection.overview.subtitle)
                            .font(.system(size: 12))
                            .foregroundColor(.secondary)
                    }
                    Spacer()
                    Label("READY", systemImage: "circle.fill")
                        .font(.system(size: 10, weight: .bold, design: .monospaced))
                        .foregroundColor(Color(red: 0.63, green: 0.91, blue: 0.68))
                        .padding(.horizontal, 12)
                        .padding(.vertical, 8)
                        .background(Color.green.opacity(0.09), in: Capsule())
                }
                .padding(.horizontal, 28)
                .padding(.top, 24)
                .padding(.bottom, 18)

                Divider().overlay(Color.white.opacity(0.07))

                ScrollView {
                    Group {
                        switch selection ?? .overview {
                        case .overview:
                            OverviewView(model: model, selection: $selection)
                        case .installer:
                            InstallerView(model: model)
                        case .efiBuilder:
                            EFIBuilderView(model: model)
                        case .efiChecker:
                            EFICheckerView(model: model)
                        case .hardware:
                            HardwareView(model: model)
                        }
                    }
                    .padding(28)
                }
            }
            .background(Color(red: 0.035, green: 0.05, blue: 0.065))
        }
        .onAppear {
            model.refreshHardware()
        }
    }
}

private struct OverviewView: View {
    @ObservedObject var model: AppModel
    @Binding var selection: AppSection?

    var body: some View {
        VStack(alignment: .leading, spacing: 22) {
            VStack(alignment: .leading, spacing: 12) {
                Text("PREPARE WITH EVIDENCE")
                    .font(.system(size: 10, weight: .bold, design: .monospaced))
                    .foregroundColor(.accentColor)
                Text("Your OpenCore\nworkbench.")
                    .font(.system(size: 38, weight: .bold, design: .rounded))
                Text("Download a full macOS installer from Apple's software update catalogs, build an OpenCore starter, then inspect the files before you use them.")
                    .font(.system(size: 14))
                    .foregroundColor(.secondary)
                    .frame(maxWidth: 650, alignment: .leading)
            }
            .padding(26)
            .frame(maxWidth: .infinity, alignment: .leading)
            .background(
                LinearGradient(
                    colors: [Color(red: 0.10, green: 0.16, blue: 0.18), Color(red: 0.07, green: 0.10, blue: 0.13)],
                    startPoint: .topLeading,
                    endPoint: .bottomTrailing
                ),
                in: RoundedRectangle(cornerRadius: 18)
            )
            .overlay(RoundedRectangle(cornerRadius: 18).stroke(Color.white.opacity(0.07)))

            LazyVGrid(columns: [GridItem(.flexible()), GridItem(.flexible())], spacing: 14) {
                ToolCard(number: "01", title: "Full macOS installer", subtitle: "Apple catalog download · custom destination · USB creation", icon: "externaldrive", section: .installer, selection: $selection)
                ToolCard(number: "02", title: "OpenCore EFI starter", subtitle: "Latest official release · SHA-256 verification · Sample.plist", icon: "hammer", section: .efiBuilder, selection: $selection)
                ToolCard(number: "03", title: "EFI checker", subtitle: "Required files · config.plist · enabled path references", icon: "checkmark.shield", section: .efiChecker, selection: $selection)
                ToolCard(number: "04", title: "Hardware inventory", subtitle: "CPU · graphics · memory · macOS version", icon: "cpu", section: .hardware, selection: $selection)
            }

            NoticeCard(
                title: "Important distinction",
                message: "The EFI Builder creates a clean starter, not a hardware-configured EFI. A real boot configuration still needs the correct ACPI files, Kexts, drivers, quirks and SMBIOS for the target computer.",
                icon: "info.circle",
                tone: .warning
            )
        }
    }
}

private struct ToolCard: View {
    let number: String
    let title: String
    let subtitle: String
    let icon: String
    let section: AppSection
    @Binding var selection: AppSection?

    var body: some View {
        Button {
            selection = section
        } label: {
            HStack(alignment: .top, spacing: 14) {
                Image(systemName: icon)
                    .font(.system(size: 20, weight: .semibold))
                    .foregroundColor(.accentColor)
                    .frame(width: 46, height: 46)
                    .background(Color.accentColor.opacity(0.10), in: RoundedRectangle(cornerRadius: 12))
                VStack(alignment: .leading, spacing: 6) {
                    Text(number)
                        .font(.system(size: 10, weight: .bold, design: .monospaced))
                        .foregroundColor(.secondary)
                    Text(title)
                        .font(.system(size: 16, weight: .semibold))
                        .foregroundColor(.white)
                    Text(subtitle)
                        .font(.system(size: 11))
                        .foregroundColor(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                Spacer(minLength: 0)
                Image(systemName: "arrow.up.right")
                    .font(.system(size: 11, weight: .semibold))
                    .foregroundColor(.secondary)
            }
            .padding(18)
            .frame(maxWidth: .infinity, minHeight: 122, alignment: .leading)
            .background(Color.white.opacity(0.045), in: RoundedRectangle(cornerRadius: 14))
            .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.white.opacity(0.07)))
        }
        .buttonStyle(.plain)
    }
}

private struct InstallerView: View {
    @ObservedObject var model: AppModel
    @State private var showEraseConfirmation = false

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            AppPanel(title: "APPLE INSTALLER CATALOG", subtitle: "Full installers are downloaded from Apple's software update catalogs. This workflow does not use macOS Recovery.") {
                HStack {
                    Button {
                        model.refreshInstallers()
                    } label: {
                        Label("Load catalog", systemImage: "arrow.clockwise")
                    }
                    .disabled(model.isLoadingInstallers || model.isDownloadingInstaller)
                    if model.isLoadingInstallers {
                        ProgressView().controlSize(.small)
                    }
                    Spacer()
                    Text("MIST ENGINE · v2.3")
                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                        .foregroundColor(.secondary)
                }

                if model.installers.isEmpty {
                    Text("No catalog loaded.")
                        .foregroundColor(.secondary)
                        .font(.system(size: 12))
                        .padding(.vertical, 6)
                } else {
                    Picker("macOS release", selection: $model.selectedInstallerID) {
                        ForEach(model.installers) { installer in
                            Text("\(installer.title) · \(installer.build)")
                                .tag(installer.id)
                        }
                    }
                    .pickerStyle(.menu)

                    if let selected = model.selectedInstaller {
                        HStack(spacing: 10) {
                            MetricPill(label: "VERSION", value: selected.version)
                            MetricPill(label: "BUILD", value: selected.build)
                            MetricPill(label: "SIZE", value: selected.sizeText)
                            MetricPill(label: "DATE", value: selected.releaseDate)
                        }
                    }
                }

                VStack(alignment: .leading, spacing: 7) {
                    Text("DOWNLOAD LOCATION")
                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                        .foregroundColor(.secondary)
                    HStack {
                        Text(model.installerFolder)
                            .font(.system(size: 11, design: .monospaced))
                            .lineLimit(1)
                            .truncationMode(.middle)
                            .textSelection(.enabled)
                        Spacer()
                        Button("Choose folder…") {
                            if let path = chooseFolder(startingAt: model.installerFolder) {
                                model.installerFolder = path
                            }
                        }
                    }
                }

                HStack {
                    Button {
                        model.downloadSelectedInstaller()
                    } label: {
                        Label("Download full installer", systemImage: "arrow.down.circle.fill")
                    }
                    .buttonStyle(.borderedProminent)
                    .disabled(model.selectedInstaller == nil || model.isDownloadingInstaller || model.isLoadingInstallers || model.installerFolder.isEmpty)

                    if model.isDownloadingInstaller {
                        ProgressView().controlSize(.small)
                    }
                    Spacer()
                    if !model.downloadedInstallerPath.isEmpty {
                        Button("Reveal in Finder") {
                            NSWorkspace.shared.selectFile(model.downloadedInstallerPath, inFileViewerRootedAtPath: model.installerFolder)
                        }
                    }
                }

                StatusText(text: model.installerStatus)
            }

            AppPanel(title: "CREATE BOOTABLE USB", subtitle: "Use Apple's createinstallmedia tool included in the downloaded installer application.") {
                HStack {
                    Button {
                        model.refreshVolumes()
                    } label: {
                        Label("Refresh removable drives", systemImage: "arrow.clockwise")
                    }
                    Picker("Target volume", selection: $model.selectedVolumePath) {
                        ForEach(model.volumes) { volume in
                            Text("\(volume.displayTitle) — \(volume.path)")
                                .tag(volume.path)
                        }
                    }
                    .frame(maxWidth: 460)
                    .disabled(model.volumes.isEmpty)
                }

                Button(role: .destructive) {
                    showEraseConfirmation = true
                } label: {
                    Label("Erase and create installer", systemImage: "externaldrive.badge.plus")
                }
                .disabled(model.downloadedInstallerPath.isEmpty || model.selectedVolume == nil || model.isCreatingUSB)

                if model.isCreatingUSB { ProgressView().controlSize(.small) }
                StatusText(text: model.usbStatus)
            }

            NoticeCard(
                title: "USB erasure warning",
                message: "Creating a bootable installer erases the selected removable volume. The app asks for confirmation before calling createinstallmedia. Double-check the drive name and path, and back up its contents first.",
                icon: "exclamationmark.triangle",
                tone: .danger
            )
        }
        .onAppear {
            if model.installers.isEmpty && !model.isLoadingInstallers { model.refreshInstallers() }
            if model.volumes.isEmpty { model.refreshVolumes() }
        }
        .confirmationDialog(
            "Erase and create the macOS installer?",
            isPresented: $showEraseConfirmation,
            titleVisibility: .visible
        ) {
            Button("Erase and create on \(model.selectedVolume?.name ?? "selected volume")", role: .destructive) {
                model.createBootableUSB()
            }
            Button("Cancel", role: .cancel) {}
        } message: {
            Text("Everything on \(model.selectedVolume?.path ?? "the selected volume") will be erased. Continue only after checking the exact drive.")
        }
    }
}

private struct EFIBuilderView: View {
    @ObservedObject var model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            AppPanel(title: "OFFICIAL OPENCORE RELEASE", subtitle: "The app checks the GitHub release metadata, downloads the RELEASE archive and verifies its published SHA-256 digest before extracting it.") {
                VStack(alignment: .leading, spacing: 7) {
                    Text("OUTPUT PARENT FOLDER")
                        .font(.system(size: 9, weight: .bold, design: .monospaced))
                        .foregroundColor(.secondary)
                    HStack {
                        Text(model.efiFolder)
                            .font(.system(size: 11, design: .monospaced))
                            .lineLimit(1)
                            .truncationMode(.middle)
                            .textSelection(.enabled)
                        Spacer()
                        Button("Choose folder…") {
                            if let path = chooseFolder(startingAt: model.efiFolder) {
                                model.efiFolder = path
                            }
                        }
                    }
                }

                Button {
                    model.buildStarterEFI()
                } label: {
                    Label("Download OpenCore and build EFI starter", systemImage: "hammer.fill")
                }
                .buttonStyle(.borderedProminent)
                .disabled(model.isBuildingEFI || model.efiFolder.isEmpty)

                if model.isBuildingEFI { ProgressView().controlSize(.small) }
                StatusText(text: model.efiStatus)

                if !model.selectedEFIPath.isEmpty {
                    Button("Open EFI folder in Finder") {
                        NSWorkspace.shared.open(URL(fileURLWithPath: model.selectedEFIPath, isDirectory: true))
                    }
                }
            }

            NoticeCard(
                title: "This is not a ready-to-boot EFI",
                message: "The generated config.plist is the official Sample.plist. CPU platform, firmware quirks, ACPI, Kexts, drivers, graphics properties and SMBIOS must still be selected for the target PC. Never boot another computer's EFI without auditing it.",
                icon: "info.circle",
                tone: .warning
            )
        }
    }
}

private struct EFICheckerView: View {
    @ObservedObject var model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            AppPanel(title: "SELECT EFI FOLDER", subtitle: "The checker reads the selected folder locally and does not modify it.") {
                HStack {
                    Text(model.selectedEFIPath.isEmpty ? "No EFI folder selected" : model.selectedEFIPath)
                        .font(.system(size: 11, design: .monospaced))
                        .lineLimit(1)
                        .truncationMode(.middle)
                        .textSelection(.enabled)
                    Spacer()
                    Button("Choose EFI…") {
                        if let path = chooseFolder(startingAt: model.selectedEFIPath) {
                            model.inspectEFI(at: path)
                        }
                    }
                }
            }

            if !model.efiFindings.isEmpty {
                AppPanel(title: "STRUCTURE REPORT", subtitle: "A structural pass is not proof of compatibility or a successful boot.") {
                    ForEach(model.efiFindings) { finding in
                        HStack(alignment: .top, spacing: 12) {
                            Image(systemName: finding.level == .pass ? "checkmark.circle.fill" : (finding.level == .failure ? "xmark.circle.fill" : "exclamationmark.triangle.fill"))
                                .foregroundColor(finding.level == .pass ? .green : (finding.level == .failure ? .red : .yellow))
                            VStack(alignment: .leading, spacing: 3) {
                                Text(finding.title).font(.system(size: 12, weight: .semibold))
                                Text(finding.detail).font(.system(size: 11)).foregroundColor(.secondary)
                            }
                            Spacer()
                        }
                        .padding(.vertical, 5)
                    }
                }
            }
        }
    }
}

private struct HardwareView: View {
    @ObservedObject var model: AppModel

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            AppPanel(title: "LOCAL HARDWARE INVENTORY", subtitle: "Collected using Apple's system_profiler and built-in macOS utilities. Serial numbers and hardware UUIDs are not requested.") {
                HStack {
                    Button {
                        model.refreshHardware()
                    } label: {
                        Label("Refresh inventory", systemImage: "arrow.clockwise")
                    }
                    if model.isLoadingHardware { ProgressView().controlSize(.small) }
                }
                ForEach(model.hardwareItems) { item in
                    HStack(alignment: .firstTextBaseline) {
                        Text(item.name.uppercased())
                            .font(.system(size: 9, weight: .bold, design: .monospaced))
                            .foregroundColor(.secondary)
                            .frame(width: 125, alignment: .leading)
                        Text(item.value)
                            .font(.system(size: 12))
                            .textSelection(.enabled)
                        Spacer()
                    }
                    .padding(.vertical, 4)
                    Divider().overlay(Color.white.opacity(0.06))
                }
                StatusText(text: model.hardwareStatus)
            }
        }
        .onAppear {
            if model.hardwareItems.isEmpty && !model.isLoadingHardware { model.refreshHardware() }
        }
    }
}

private struct AppPanel<Content: View>: View {
    let title: String
    let subtitle: String
    let content: Content

    init(title: String, subtitle: String, @ViewBuilder content: () -> Content) {
        self.title = title
        self.subtitle = subtitle
        self.content = content()
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 14) {
            VStack(alignment: .leading, spacing: 4) {
                Text(title)
                    .font(.system(size: 11, weight: .bold, design: .monospaced))
                    .foregroundColor(.accentColor)
                Text(subtitle)
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
            }
            content
        }
        .padding(18)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(Color.white.opacity(0.045), in: RoundedRectangle(cornerRadius: 14))
        .overlay(RoundedRectangle(cornerRadius: 14).stroke(Color.white.opacity(0.07)))
    }
}

private struct MetricPill: View {
    let label: String
    let value: String

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label)
                .font(.system(size: 8, weight: .bold, design: .monospaced))
                .foregroundColor(.secondary)
            Text(value)
                .font(.system(size: 10, weight: .semibold))
                .lineLimit(1)
                .minimumScaleFactor(0.8)
        }
        .padding(.horizontal, 10)
        .padding(.vertical, 8)
        .background(Color.white.opacity(0.055), in: RoundedRectangle(cornerRadius: 9))
    }
}

private struct NoticeCard: View {
    enum Tone: Equatable { case warning, danger }
    let title: String
    let message: String
    let icon: String
    let tone: Tone

    private var color: Color { tone == .danger ? .red : .yellow }

    var body: some View {
        HStack(alignment: .top, spacing: 12) {
            Image(systemName: icon)
                .foregroundColor(color)
                .font(.system(size: 16, weight: .semibold))
            VStack(alignment: .leading, spacing: 5) {
                Text(title).font(.system(size: 12, weight: .semibold))
                Text(message)
                    .font(.system(size: 11))
                    .foregroundColor(.secondary)
                    .fixedSize(horizontal: false, vertical: true)
            }
            Spacer()
        }
        .padding(16)
        .frame(maxWidth: .infinity, alignment: .leading)
        .background(color.opacity(0.07), in: RoundedRectangle(cornerRadius: 12))
        .overlay(RoundedRectangle(cornerRadius: 12).stroke(color.opacity(0.20)))
    }
}

private struct StatusText: View {
    let text: String

    var body: some View {
        Text(text)
            .font(.system(size: 11))
            .foregroundColor(.secondary)
            .textSelection(.enabled)
            .frame(maxWidth: .infinity, alignment: .leading)
            .padding(.top, 2)
    }
}

private func chooseFolder(startingAt path: String) -> String? {
    let panel = NSOpenPanel()
    panel.canChooseFiles = false
    panel.canChooseDirectories = true
    panel.canCreateDirectories = true
    panel.allowsMultipleSelection = false
    panel.prompt = "Choose folder"
    if !path.isEmpty {
        panel.directoryURL = URL(fileURLWithPath: path, isDirectory: true)
    }
    return panel.runModal() == .OK ? panel.url?.path : nil
}
