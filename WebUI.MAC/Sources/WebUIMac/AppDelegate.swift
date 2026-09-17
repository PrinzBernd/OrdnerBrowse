import AppKit
import WebKit

final class AppDelegate: NSObject, NSApplicationDelegate, WKUIDelegate {
    private let processController = WebUIProcessController()
    private var window: NSWindow?

    func applicationDidFinishLaunching(
        _ notification: Notification)
    {
        do {
            let localUrl = try processController.start()

            let window = NSWindow(
                contentRect: NSRect(
                    x: 0,
                    y: 0,
                    width: 1280,
                    height: 820),
                styleMask: [
                    .titled,
                    .closable,
                    .miniaturizable,
                    .resizable
                ],
                backing: .buffered,
                defer: false)

            window.title = "OrdnerBrowse"
            window.center()

            let webView = WKWebView(frame: .zero)
            webView.uiDelegate = self
            webView.translatesAutoresizingMaskIntoConstraints = false

            let contentView = NSView()
            contentView.addSubview(webView)
            window.contentView = contentView

            NSLayoutConstraint.activate([
                webView.leadingAnchor.constraint(
                    equalTo: contentView.leadingAnchor),
                webView.trailingAnchor.constraint(
                    equalTo: contentView.trailingAnchor),
                webView.topAnchor.constraint(
                    equalTo: contentView.topAnchor),
                webView.bottomAnchor.constraint(
                    equalTo: contentView.bottomAnchor)
            ])

            self.window = window
            window.makeKeyAndOrderFront(nil)
            NSApp.activate(ignoringOtherApps: true)

            webView.load(URLRequest(url: localUrl))

            print("WEBUI_READY_URL=\(localUrl.absoluteString)")
        }
        catch {
            let alert = NSAlert()
            alert.alertStyle = .critical
            alert.messageText = "OrdnerBrowse konnte WebUI.Web nicht starten."
            alert.informativeText = error.localizedDescription
            alert.runModal()
            NSApp.terminate(nil)
        }
    }

    func webView(
        _ webView: WKWebView,
        createWebViewWith configuration: WKWebViewConfiguration,
        for navigationAction: WKNavigationAction,
        windowFeatures: WKWindowFeatures) -> WKWebView?
    {
        guard
            navigationAction.targetFrame == nil,
            let url = navigationAction.request.url
        else {
            return nil
        }

        NSWorkspace.shared.open(url)
        return nil
    }

    func applicationWillTerminate(
        _ notification: Notification)
    {
        processController.stop()
    }

    func applicationShouldTerminateAfterLastWindowClosed(
        _ sender: NSApplication) -> Bool
    {
        true
    }
}
