// swift-tools-version: 6.0

import PackageDescription

let package = Package(
  name: "ChatUnpack",
  platforms: [
    .macOS(.v13)
  ],
  products: [
    .library(
      name: "ChatUnpackCore",
      targets: ["ChatUnpackCore"]
    ),
    .executable(
      name: "ChatUnpackApp",
      targets: ["ChatUnpackApp"]
    ),
    .executable(
      name: "ChatUnpackFixtureHost",
      targets: ["ChatUnpackFixtureHost"]
    ),
    .executable(
      name: "ChatUnpackCoreTestRunner",
      targets: ["ChatUnpackCoreTestRunner"]
    )
  ],
  targets: [
    .target(
      name: "ChatUnpackCore",
      path: "Sources/ChatUnpackCore"
    ),
    .executableTarget(
      name: "ChatUnpackApp",
      dependencies: ["ChatUnpackCore"],
      path: "Sources/ChatUnpackApp"
    ),
    .executableTarget(
      name: "ChatUnpackFixtureHost",
      dependencies: ["ChatUnpackCore"],
      path: "Sources/ChatUnpackFixtureHost"
    ),
    .executableTarget(
      name: "ChatUnpackCoreTestRunner",
      dependencies: ["ChatUnpackCore"],
      path: "Tests/ChatUnpackCoreTests"
    )
  ]
)
