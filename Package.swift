// swift-tools-version: 5.10
import PackageDescription

let package = Package(
    name: "FreeNotch",
    platforms: [.macOS(.v14)],
    targets: [
        .executableTarget(
            name: "FreeNotch",
            path: "Sources/FreeNotch",
            swiftSettings: [
                .unsafeFlags(["-Osize"], .when(configuration: .release))
            ],
            linkerSettings: [
                .linkedFramework("IOBluetooth"),
                .linkedFramework("IOKit"),
                .linkedFramework("CoreAudio"),
                .linkedFramework("EventKit"),
                .linkedFramework("AVFoundation"),
                .linkedFramework("ServiceManagement")
            ]
        )
    ]
)
