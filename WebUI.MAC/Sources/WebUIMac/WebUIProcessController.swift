import Darwin
import Foundation

final class WebUIProcessController {
    private enum StartError: LocalizedError {
        case projectRootNotFound
        case bundledWebExecutableUnavailable(URL)
        case socketCreationFailed(Int32)
        case loopbackAddressFailed
        case socketBindFailed(Int32)
        case socketNameFailed(Int32)
        case invalidPort
        case processLaunchFailed(String)
        case processExited(Int32)
        case healthTimeout(URL)

        var errorDescription: String? {
            switch self {
            case .projectRootNotFound:
                return "Der Projektstamm mit gebautem WebUI.Web wurde nicht gefunden."
            case .bundledWebExecutableUnavailable(let url):
                return "Das eingebettete WebUI.Web wurde nicht gefunden oder ist nicht ausführbar: \(url.path)"
            case .socketCreationFailed(let code):
                return "Der Loopback-Socket konnte nicht angelegt werden (errno \(code))."
            case .loopbackAddressFailed:
                return "Die Loopback-Adresse 127.0.0.1 konnte nicht vorbereitet werden."
            case .socketBindFailed(let code):
                return "Ein freier Loopback-Port konnte nicht gebunden werden (errno \(code))."
            case .socketNameFailed(let code):
                return "Der dynamische Loopback-Port konnte nicht ermittelt werden (errno \(code))."
            case .invalidPort:
                return "Der ermittelte Loopback-Port ist ungültig."
            case .processLaunchFailed(let message):
                return "WebUI.Web konnte nicht gestartet werden: \(message)"
            case .processExited(let code):
                return "WebUI.Web wurde vor Erreichen von /healthz beendet (Exit-Code \(code))."
            case .healthTimeout(let url):
                return "WebUI.Web wurde unter \(url.absoluteString) nicht rechtzeitig bereit."
            }
        }
    }

    private struct WebLaunchTarget {
        let executableURL: URL
        let arguments: [String]
        let workingDirectoryURL: URL
        let contentRootURL: URL?
        let webRootURL: URL?
    }

    private var webProcess: Process?

    func start() throws -> URL {
        if let webProcess, webProcess.isRunning {
            throw StartError.processLaunchFailed(
                "Es läuft bereits ein WebUI.Web-Kindprozess.")
        }

        let launchTarget = try resolveWebLaunchTarget()

        let port = try reserveFreeLoopbackPort()
        guard let localUrl = URL(
            string: "http://127.0.0.1:\(port)")
        else {
            throw StartError.invalidPort
        }

        let process = Process()
        process.executableURL =
            launchTarget.executableURL
        process.arguments =
            launchTarget.arguments
        process.currentDirectoryURL =
            launchTarget.workingDirectoryURL

        var environment = ProcessInfo.processInfo.environment
        environment["ASPNETCORE_ENVIRONMENT"] = "Development"
        environment["DOTNET_ENVIRONMENT"] = "Development"
        environment["WebUi__RuntimeProfile"] = "MacDesktop"
        environment["LocalMultiUser__LoopbackUrl"] =
            localUrl.absoluteString
        environment["Logging__LogLevel__Default"] = "Information"
        environment["Logging__LogLevel__Microsoft_AspNetCore"] =
            "Warning"

        if let contentRootURL =
            launchTarget.contentRootURL
        {
            environment["ASPNETCORE_CONTENTROOT"] =
                contentRootURL.path
        }
        else
        {
            environment.removeValue(
                forKey: "ASPNETCORE_CONTENTROOT")
        }

        if let webRootURL =
            launchTarget.webRootURL
        {
            environment["ASPNETCORE_WEBROOT"] =
                webRootURL.path
        }
        else
        {
            environment.removeValue(
                forKey: "ASPNETCORE_WEBROOT")
        }

        process.environment = environment

        process.standardOutput = FileHandle.standardOutput
        process.standardError = FileHandle.standardError

        do {
            try process.run()
        }
        catch {
            throw StartError.processLaunchFailed(
                error.localizedDescription)
        }

        webProcess = process

        do {
            try waitForHealth(
                baseUrl: localUrl,
                process: process)
        }
        catch {
            stop()
            throw error
        }

        return localUrl
    }

    func stop() {
        guard let process = webProcess else {
            return
        }

        defer {
            webProcess = nil
        }

        guard process.isRunning else {
            return
        }

        process.terminate()

        let deadline = Date().addingTimeInterval(5)
        while process.isRunning && Date() < deadline {
            Thread.sleep(forTimeInterval: 0.05)
        }

        if process.isRunning {
            Darwin.kill(
                process.processIdentifier,
                SIGKILL)
        }
    }

    private func waitForHealth(
        baseUrl: URL,
        process: Process) throws
    {
        let healthUrl = baseUrl.appendingPathComponent(
            "healthz")
        let deadline = Date().addingTimeInterval(15)

        while Date() < deadline {
            if !process.isRunning {
                throw StartError.processExited(
                    process.terminationStatus)
            }

            do {
                _ = try Data(
                    contentsOf: healthUrl,
                    options: .uncached)
                return
            }
            catch {
                Thread.sleep(
                    forTimeInterval: 0.10)
            }
        }

        throw StartError.healthTimeout(
            healthUrl)
    }

    private func reserveFreeLoopbackPort() throws -> UInt16 {
        let descriptor = Darwin.socket(
            AF_INET,
            SOCK_STREAM,
            0)

        guard descriptor >= 0 else {
            throw StartError.socketCreationFailed(
                errno)
        }

        defer {
            Darwin.close(descriptor)
        }

        var address = sockaddr_in()
        address.sin_len =
            UInt8(MemoryLayout<sockaddr_in>.size)
        address.sin_family =
            sa_family_t(AF_INET)
        address.sin_port =
            in_port_t(0).bigEndian

        let conversionResult =
            "127.0.0.1".withCString { pointer in
                inet_pton(
                    AF_INET,
                    pointer,
                    &address.sin_addr)
            }

        guard conversionResult == 1 else {
            throw StartError.loopbackAddressFailed
        }

        let bindResult =
            withUnsafePointer(to: &address) { pointer in
                pointer.withMemoryRebound(
                    to: sockaddr.self,
                    capacity: 1)
                {
                    Darwin.bind(
                        descriptor,
                        $0,
                        socklen_t(
                            MemoryLayout<sockaddr_in>.size))
                }
            }

        guard bindResult == 0 else {
            throw StartError.socketBindFailed(
                errno)
        }

        var boundAddress = sockaddr_in()
        var boundLength =
            socklen_t(MemoryLayout<sockaddr_in>.size)

        let nameResult =
            withUnsafeMutablePointer(
                to: &boundAddress)
            { pointer in
                pointer.withMemoryRebound(
                    to: sockaddr.self,
                    capacity: 1)
                {
                    Darwin.getsockname(
                        descriptor,
                        $0,
                        &boundLength)
                }
            }

        guard nameResult == 0 else {
            throw StartError.socketNameFailed(
                errno)
        }

        let port = UInt16(
            bigEndian: boundAddress.sin_port)

        guard port > 0 else {
            throw StartError.invalidPort
        }

        return port
    }

    private func resolveWebLaunchTarget() throws -> WebLaunchTarget {
        let fileManager = FileManager.default
        let bundleUrl =
            Bundle.main.bundleURL.standardizedFileURL

        if bundleUrl.pathExtension.lowercased() == "app" {
            let contentsRoot = bundleUrl
                .appendingPathComponent(
                    "Contents",
                    isDirectory: true)
            let webRoot = contentsRoot
                .appendingPathComponent(
                    "Resources",
                    isDirectory: true)
                .appendingPathComponent(
                    "WebUI.Web",
                    isDirectory: true)
            let webExecutable = contentsRoot
                .appendingPathComponent(
                    "Helpers",
                    isDirectory: true)
                .appendingPathComponent(
                    "WebUI.Web",
                    isDirectory: false)

            guard fileManager.fileExists(
                atPath: webExecutable.path),
                fileManager.isExecutableFile(
                    atPath: webExecutable.path)
            else {
                throw StartError
                    .bundledWebExecutableUnavailable(
                        webExecutable)
            }

            return WebLaunchTarget(
                executableURL: webExecutable,
                arguments: [],
                workingDirectoryURL: webRoot,
                contentRootURL: webRoot,
                webRootURL: webRoot
                    .appendingPathComponent(
                        "wwwroot",
                        isDirectory: true))
        }

        let projectRoot = try locateProjectRoot()
        let webRoot = projectRoot.appendingPathComponent(
            "WebUI.Web",
            isDirectory: true)
        let webDll = webRoot.appendingPathComponent(
            "bin/Release/net10.0/WebUI.Web.dll")

        return WebLaunchTarget(
            executableURL: URL(
                fileURLWithPath: "/usr/bin/env"),
            arguments: [
                "dotnet",
                webDll.path
            ],
            workingDirectoryURL: webRoot,
            contentRootURL: nil,
            webRootURL: nil)
    }

    private func locateProjectRoot() throws -> URL {
        let fileManager = FileManager.default
        var candidates = [
            URL(
                fileURLWithPath:
                    fileManager.currentDirectoryPath,
                isDirectory: true)
        ]

        if let executableUrl =
            Bundle.main.executableURL
        {
            candidates.append(
                executableUrl
                    .deletingLastPathComponent())
        }

        for candidate in candidates {
            var current =
                candidate.standardizedFileURL

            for _ in 0..<10 {
                let dll = current
                    .appendingPathComponent(
                        "WebUI.Web",
                        isDirectory: true)
                    .appendingPathComponent(
                        "bin/Release/net10.0/WebUI.Web.dll")

                if fileManager.fileExists(
                    atPath: dll.path)
                {
                    return current
                }

                let parent =
                    current.deletingLastPathComponent()

                if parent.path == current.path {
                    break
                }

                current = parent
            }
        }

        throw StartError.projectRootNotFound
    }
}
