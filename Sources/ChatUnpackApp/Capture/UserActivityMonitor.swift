import AppKit
import Foundation

@MainActor
public final class UserActivityMonitor {
  private var eventMonitor: Any?
  private var workspaceObserver: NSObjectProtocol?
  private var initialMouseLocation: CGPoint?
  private(set) public var detected = false

  public init() {}

  public func start() {
    stop()
    detected = false
    initialMouseLocation = NSEvent.mouseLocation

    let eventTypes: NSEvent.EventTypeMask = [
      .leftMouseDown,
      .rightMouseDown,
      .otherMouseDown,
      .mouseMoved,
      .scrollWheel,
      .keyDown
    ]
    eventMonitor = NSEvent.addGlobalMonitorForEvents(matching: eventTypes) { [weak self] event in
      Task { @MainActor [weak self] in
        guard let self else { return }
        if event.type == .mouseMoved,
           let initialMouseLocation = self.initialMouseLocation {
          let distance = hypot(
            NSEvent.mouseLocation.x - initialMouseLocation.x,
            NSEvent.mouseLocation.y - initialMouseLocation.y
          )
          if distance < 12 {
            return
          }
        }
        self.detected = true
      }
    }
    if eventMonitor == nil {
      detected = true
    }

    workspaceObserver = NSWorkspace.shared.notificationCenter.addObserver(
      forName: NSWorkspace.didActivateApplicationNotification,
      object: nil,
      queue: .main
    ) { [weak self] _ in
      Task { @MainActor [weak self] in
        self?.detected = true
      }
    }
  }

  public func stop() {
    if let eventMonitor {
      NSEvent.removeMonitor(eventMonitor)
      self.eventMonitor = nil
    }
    if let workspaceObserver {
      NSWorkspace.shared.notificationCenter.removeObserver(workspaceObserver)
      self.workspaceObserver = nil
    }
  }

  public func reset() {
    detected = false
    initialMouseLocation = NSEvent.mouseLocation
  }
}
