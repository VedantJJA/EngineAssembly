# Android frame pacing dependency

Official, unchanged AndroidX Games Frame Pacing 2.1.2 AAR and POM, licensed under Apache 2.0. The AAR SHA-1 was checked against Google's published checksum when downloaded.

Source: https://dl.google.com/dl/android/maven2/androidx/games/games-frame-pacing/2.1.2/games-frame-pacing-2.1.2.aar
POM: https://dl.google.com/dl/android/maven2/androidx/games/games-frame-pacing/2.1.2/games-frame-pacing-2.1.2.pom
License: https://www.apache.org/licenses/LICENSE-2.0

Unity adds this dependency when optimized Android frame pacing is enabled. WorkshopAndroidDependencies registers this local Maven coordinate exclusively for this module, preserving normal resolution for all other dependencies. This avoids failed Java HTTPS downloads during packaging and keeps builds reproducible. Do not add the AAR to Assets/Plugins, where it would duplicate Unity's Maven dependency.
