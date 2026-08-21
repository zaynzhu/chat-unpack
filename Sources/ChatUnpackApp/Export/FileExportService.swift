import AppKit
import Foundation
import UniformTypeIdentifiers

@MainActor
public final class FileExportService {
  public init() {}

  public func saveMarkdown(_ text: String, defaultFileName: String) throws {
    let panel = NSSavePanel()
    panel.title = "保存 Markdown"
    panel.nameFieldStringValue = defaultFileName
    panel.allowedContentTypes = [UTType(filenameExtension: "md") ?? .plainText]
    panel.canCreateDirectories = true

    guard panel.runModal() == .OK, let url = panel.url else {
      throw FileExportError.cancelled
    }

    do {
      try text.write(to: url, atomically: true, encoding: .utf8)
    } catch {
      throw FileExportError.writeFailed
    }
  }
}

public enum FileExportError: LocalizedError {
  case cancelled
  case writeFailed

  public var errorDescription: String? {
    switch self {
    case .cancelled:
      return "已取消保存"
    case .writeFailed:
      return "Markdown 保存失败"
    }
  }
}
