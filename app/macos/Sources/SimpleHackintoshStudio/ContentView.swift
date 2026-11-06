import AppKit
import SwiftUI
import UniformTypeIdentifiers
import StudioCore

private enum AppSection: String, CaseIterable, Identifiable {
    case overview, installer, efi, hardware
    var id: String { rawValue }

    var title: String {
        switch self {
        case .overview: return "Overview"
        case .installer: return "macOS Downloader"
        case .efi: return "EFI Builder"
        case .hardware: return "Hardware"
        }
    }

    var symbol: String {
        switch self {
        case .overview: return "square.grid.2x2"
        case .installer: return "arrow.down.circle"
        case .efi: return "externaldrive.badge.plus"
        case .hardware: return "cpu"
        }
    }
}

struct ContentView: View {
    @StateObject private var model = StudioViewModel()
    @State private var selection: AppSection? = .overview

    var body: some View {
        NavigationSplitView {
            VStack(spacing: 0) {
                HStack(spacing: 11) {
                    ZStack {
                        RoundedRectangle(cornerRadius: 12)
                            .fill(LinearGradient(
                                colors: [.cyan.opacity(0.9), .blue.opacity(0.9)],
                                startPoint: .topLeading, endPoint: .bottomTrailing
                            ))
                            .frame(width: 38, height: 38)
                        Image(systemName: "desktopcomputer")
                            .font(.system(size: 18, weight: .semibold))
                            .foregroundStyle(.white)
                    }
                    VStack(alignment: .leading, spacing: 2) {
                        Text("Simple Hackintosh").font(.system(.headline, design: .rounded))
                        Text("STUDIO · macOS")
                            .font(.system(size: 10, weight: .bold, design: .rounded))
                            .tracking(1.2)
                            .foregroundStyle(.secondary)
                    }
                }
                .frame(maxWidth: .infinity, alignment: .leading)
                .padding(.horizontal, 18)
                .padding(.vertical, 20)

                List(selection: $selection) {
                    Section("WORKSPACE") {
                        ForEach(AppSection.allCases) { section in
                            Label(section.title, systemImage: section.symbol)
                                .tag(section as AppSection?)
                        }
                    }
                }
                .listStyle(.sidebar)

                VStack(alignment: .leading, spacing: 5) {
                    Text("EARLY PREVIEW")
                        .font(.system(size: 10, weight: .bold, design: .rounded))
                        .tracking(1)
                        .foregroundStyle(.secondary)
                    Text("EFI output is a starter template, not a ready-to-boot configuration.")
                        .font(.caption2)
                        .foregroundStyle(.secondary)
                        .fixedSize(horizontal: false, vertical: true)
                }
                .padding(16)
                .frame(maxWidth: .infinity, alignment: .leading)
            }
            .frame(minWidth: 215)
        } detail: {
            VStack(spacing: 0) {
                Group {
                    switch selection ?? .overview {
                    case .overview:
                        OverviewView(selection: $selection)
                    case .installer:
                        InstallerView(model: model)
                    case .efi:
                        EFIBuilderView(model: model)
                    case .hardware:
                        HardwareView(model: model)
                    }
                }
                .frame(maxWidth: .infinity, maxHeight: .infinity)
                StatusBar(message: model.statusMessage, isBusy: model.isBusy)
            }
            .background(Color(nsColor: .underPageBackgroundColor))
        }
        .task { await model.refreshInstallers() }
    }
}

private struct OverviewView: View {
    @Binding var selection: AppSection?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 24) {
                PageHeading(
                    eyebrow: "WORKSPACE",
                    title: "Build your installer workspace",
                    subtitle: "Download a full macOS installer, prepare an OpenCore starter EFI, and inspect hardware from one native app."
                )
                HStack(spacing: 14) {
                    OverviewCard(
                        icon: "arrow.down.to.line",
                        title: "Full macOS installer",
                        detail: "Use Apple's full-installer catalog. No Recovery image is used."
                    ) { selection = .installer }
                    OverviewCard(
                        icon: "externaldrive",
                        title: "OpenCore EFI starter",
                        detail: "Fetch the official release, verify its digest, and create the EFI folder."
                    ) { selection = .efi }
                    OverviewCard(
                        icon: "cpu",
                        title: "Hardware inventory",
                        detail: "Read system_profiler output and redact identifiers before export."
                    ) { selection = .hardware }
                }
                GroupBox {
                    VStack(alignment: .leading, spacing: 10) {
                        Label("Important workflow notes", systemImage: "info.circle")
                            .font(.headline)
                        Text("Studio downloads Apple's full InstallAssistant.pkg package directly to the folder you select. The package is streamed to that destination volume; it is not staged in /Applications and no Recovery image is used.")
                        Text("The EFI Builder creates a checksum-verified OpenCore starter. It does not yet select hardware-specific ACPI, Kexts, SMBIOS, or UEFI quirks. Do not boot this template without configuring and validating it for the target computer.")
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                    .font(.callout)
                    .foregroundStyle(.secondary)
                }
                Label(
                    "No disk is erased and no internal boot files are modified by these preview features.",
                    systemImage: "checkmark.shield"
                )
                .font(.caption)
                .foregroundStyle(.secondary)
            }
            .padding(28)
        }
    }
}

private struct OverviewCard: View {
    let icon: String
    let title: String
    let detail: String
    let action: () -> Void

    var body: some View {
        Button(action: action) {
            VStack(alignment: .leading, spacing: 12) {
                Image(systemName: icon)
                    .font(.system(size: 22, weight: .medium))
                    .foregroundStyle(.cyan)
                Text(title).font(.headline).foregroundStyle(.primary)
                Text(detail)
                    .font(.caption)
                    .foregroundStyle(.secondary)
                    .multilineTextAlignment(.leading)
                Spacer(minLength: 4)
                HStack {
                    Text("Open")
                    Image(systemName: "arrow.up.right")
                }
                .font(.caption.weight(.semibold))
                .foregroundStyle(.tint)
            }
            .frame(maxWidth: .infinity, minHeight: 165, alignment: .leading)
            .padding(18)
            .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 16))
            .overlay {
                RoundedRectangle(cornerRadius: 16)
                    .strokeBorder(Color.primary.opacity(0.07), lineWidth: 1)
            }
        }
        .buttonStyle(.plain)
    }
}

private struct InstallerView: View {
    @ObservedObject var model: StudioViewModel
    @State private var destination: URL?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                PageHeading(
                    eyebrow: "APPLE SOFTWARE UPDATE",
                    title: "macOS Downloader",
                    subtitle: "Browse Apple's public full-installer catalog and save the InstallAssistant.pkg file directly to any mounted drive you choose."
                )
                GroupBox {
                    VStack(alignment: .leading, spacing: 16) {
                        HStack {
                            Label("Available full installers", systemImage: "shippingbox")
                                .font(.headline)
                            Spacer()
                            Button {
                                Task { await model.refreshInstallers() }
                            } label: {
                                Label("Refresh", systemImage: "arrow.clockwise")
                            }
                            .disabled(model.isBusy)
                        }
                        if model.installers.isEmpty {
                            EmptyStateView(
                                title: "No versions loaded",
                                symbol: "macwindow",
                                message: "Refresh the list. Apple only returns versions currently available to this Mac."
                            )
                            .frame(minHeight: 130)
                        } else {
                            Picker("macOS version", selection: $model.selectedVersion) {
                                ForEach(model.installers) { installer in
                                    Text("\(installer.title) · \(installer.version)")
                                        .tag(installer.version)
                                }
                            }
                            .pickerStyle(.menu)

                            if let chosen = model.installers.first(where: { $0.version == model.selectedVersion }) {
                                HStack(spacing: 18) {
                                    DetailPill(label: "VERSION", value: chosen.version)
                                    DetailPill(label: "BUILD", value: chosen.build ?? "Not listed")
                                    DetailPill(label: "DOWNLOAD SIZE", value: chosen.size ?? "Not listed")
                                }
                            }
                        }
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                GroupBox {
                    VStack(alignment: .leading, spacing: 13) {
                        Label("Download destination", systemImage: "externaldrive")
                            .font(.headline)
                        HStack {
                            Text(destination?.path ?? "No destination selected")
                                .font(.system(.caption, design: .monospaced))
                                .foregroundStyle(destination == nil ? .secondary : .primary)
                                .lineLimit(2)
                                .textSelection(.enabled)
                            Spacer()
                            Button("Choose folder…") { destination = FolderPicker.choose() }
                        }
                        if let progress = model.downloadProgress {
                            ProgressView(value: progress.fractionCompleted)
                            Text("\(ByteCountFormatter.string(fromByteCount: progress.receivedBytes, countStyle: .file)) downloaded"
                                + (progress.expectedBytes.map { " of " + ByteCountFormatter.string(fromByteCount: $0, countStyle: .file) } ?? ""))
                                .font(.caption)
                                .foregroundStyle(.secondary)
                        }

                        Button {
                            guard let destination else { return }
                            Task { await model.downloadInstaller(to: destination) }
                        } label: {
                            Label("Download full installer", systemImage: "arrow.down.circle.fill")
                                .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.borderedProminent)
                        .disabled(model.isBusy || model.selectedVersion.isEmpty || destination == nil)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                Label(
                    "This downloads the full InstallAssistant.pkg package directly to the selected folder using a streaming HTTPS request. It does not use Recovery or stage the package in /Applications. A partial file is removed if the transfer fails or its final size does not match Apple's catalog.",
                    systemImage: "info.circle"
                )
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
            }
            .padding(28)
        }
    }
}

private struct EFIBuilderView: View {
    @ObservedObject var model: StudioViewModel
    @State private var destination: URL?

    var body: some View {
        ScrollView {
            VStack(alignment: .leading, spacing: 22) {
                PageHeading(
                    eyebrow: "OPENCORE PACKAGE",
                    title: "EFI Builder",
                    subtitle: "Create a clean x64 OpenCore starter folder using the official stable release and its published SHA-256 digest."
                )
                GroupBox {
                    VStack(alignment: .leading, spacing: 14) {
                        Label("Build destination", systemImage: "folder.badge.plus")
                            .font(.headline)
                        HStack {
                            Text(destination?.path ?? "Choose an output folder")
                                .font(.system(.caption, design: .monospaced))
                                .foregroundStyle(destination == nil ? .secondary : .primary)
                                .lineLimit(2)
                                .textSelection(.enabled)
                            Spacer()
                            Button("Choose folder…") { destination = FolderPicker.choose() }
                        }
                        Button {
                            guard let destination else { return }
                            Task { await model.buildStarterEFI(to: destination) }
                        } label: {
                            Label("Build OpenCore EFI starter", systemImage: "hammer")
                                .frame(maxWidth: .infinity)
                        }
                        .buttonStyle(.borderedProminent)
                        .disabled(model.isBusy || destination == nil)
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                GroupBox {
                    VStack(alignment: .leading, spacing: 10) {
                        Label("Included", systemImage: "checkmark.circle").font(.headline)
                        BulletLine("Official BOOTx64.efi and OpenCore.efi from the latest stable OpenCore release.")
                        BulletLine("The matching Sample.plist copied as config.plist.")
                        BulletLine("GitHub's published SHA-256 digest is checked before extraction.")
                        Divider()
                        Label("Still requires hardware-specific work", systemImage: "exclamationmark.triangle")
                            .font(.headline)
                        BulletLine("ACPI tables, Kexts, UEFI drivers and Kernel settings.")
                        BulletLine("PlatformInfo/SMBIOS and NVRAM values for the target computer.")
                        BulletLine("Matching ocvalidate checks and a real boot test from a USB backup.")
                    }
                    .frame(maxWidth: .infinity, alignment: .leading)
                }
                Text("The builder never overwrites an existing EFI folder. It stops if the selected destination already contains EFI.")
                    .font(.callout)
                    .foregroundStyle(.secondary)
            }
            .padding(28)
        }
    }
}

private struct HardwareView: View {
    @ObservedObject var model: StudioViewModel

    var body: some View {
        VStack(alignment: .leading, spacing: 18) {
            PageHeading(
                eyebrow: "SYSTEM PROFILER",
                title: "Hardware inventory",
                subtitle: "Inspect this Mac's hardware report. Common serial numbers, UUIDs and MAC addresses are redacted before display or export."
            )
            HStack {
                Button {
                    Task { await model.scanHardware() }
                } label: {
                    Label("Scan hardware", systemImage: "arrow.clockwise")
                }
                .buttonStyle(.borderedProminent)
                .disabled(model.isBusy)

                Button {
                    let panel = NSSavePanel()
                    panel.nameFieldStringValue = "Hardware-Report-Redacted.json"
                    panel.allowedContentTypes = [.json]
                    if panel.runModal() == .OK, let url = panel.url {
                        model.exportHardwareReport(to: url)
                    }
                } label: {
                    Label("Export redacted JSON", systemImage: "square.and.arrow.down")
                }
                .disabled(model.hardwareReport.isEmpty || model.isBusy)
                Spacer()
            }
            if model.hardwareReport.isEmpty {
                EmptyStateView(
                    title: "No hardware report yet",
                    symbol: "cpu",
                    message: "Select Scan hardware to collect the inventory."
                )
                .frame(maxWidth: .infinity, maxHeight: .infinity)
            } else {
                TextEditor(text: $model.hardwareReport)
                    .font(.system(.caption, design: .monospaced))
                    .scrollContentBackground(.hidden)
                    .padding(10)
                    .background(.regularMaterial, in: RoundedRectangle(cornerRadius: 12))
                    .overlay {
                        RoundedRectangle(cornerRadius: 12)
                            .strokeBorder(Color.primary.opacity(0.08), lineWidth: 1)
                    }
            }
        }
        .padding(28)
    }
}

private struct PageHeading: View {
    let eyebrow: String
    let title: String
    let subtitle: String

    var body: some View {
        VStack(alignment: .leading, spacing: 7) {
            Text(eyebrow)
                .font(.system(size: 10, weight: .bold, design: .rounded))
                .tracking(1.5)
                .foregroundStyle(.cyan)
            Text(title)
                .font(.system(size: 30, weight: .bold, design: .rounded))
            Text(subtitle)
                .font(.callout)
                .foregroundStyle(.secondary)
                .fixedSize(horizontal: false, vertical: true)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
    }
}

private struct DetailPill: View {
    let label: String
    let value: String

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label)
                .font(.system(size: 9, weight: .bold, design: .rounded))
                .tracking(0.8)
                .foregroundStyle(.secondary)
            Text(value).font(.system(.caption, design: .rounded).weight(.semibold))
        }
        .padding(10)
        .background(Color.primary.opacity(0.045), in: RoundedRectangle(cornerRadius: 9))
    }
}

private struct BulletLine: View {
    let value: String
    init(_ value: String) { self.value = value }
    var body: some View {
        Label(value, systemImage: "circle")
            .font(.callout)
            .foregroundStyle(.secondary)
    }
}

private struct StatusBar: View {
    let message: String
    let isBusy: Bool

    var body: some View {
        HStack(spacing: 9) {
            if isBusy {
                ProgressView().controlSize(.small)
            } else {
                Circle().fill(Color.green).frame(width: 7, height: 7)
            }
            Text(message)
                .font(.caption)
                .foregroundStyle(.secondary)
                .lineLimit(1)
                .textSelection(.enabled)
            Spacer()
            Text("v0.1.0 preview")
                .font(.system(size: 10, weight: .medium, design: .rounded))
                .foregroundStyle(.tertiary)
        }
        .padding(.horizontal, 18)
        .padding(.vertical, 10)
        .background(.bar)
    }
}


private struct EmptyStateView: View {
    let title: String
    let symbol: String
    let message: String

    var body: some View {
        VStack(spacing: 9) {
            Image(systemName: symbol)
                .font(.system(size: 30, weight: .light))
                .foregroundStyle(.secondary)
            Text(title).font(.headline)
            Text(message)
                .font(.callout)
                .foregroundStyle(.secondary)
                .multilineTextAlignment(.center)
                .frame(maxWidth: 330)
        }
        .frame(maxWidth: .infinity, minHeight: 150)
        .padding(16)
    }
}

private enum FolderPicker {
    static func choose() -> URL? {
        let panel = NSOpenPanel()
        panel.title = "Choose destination folder"
        panel.message = "Choose a folder on the drive where Studio should save its output."
        panel.canChooseFiles = false
        panel.canChooseDirectories = true
        panel.allowsMultipleSelection = false
        panel.canCreateDirectories = true
        return panel.runModal() == .OK ? panel.url : nil
    }
}
