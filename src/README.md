# Dictate for Windows

Voice dictation with AI-powered transcription and rewording for Windows.

## Project Structure

```
src/
├── DictateForWindows/                    # WinUI 3 Application
│   ├── App.xaml(.cs)                     # Application entry point
│   ├── Views/
│   │   ├── MainWindow.xaml               # Hidden host window
│   │   ├── DictatePopup.xaml             # Main dictation popup
│   │   ├── SettingsWindow.xaml           # Settings window
│   │   ├── PromptsWindow.xaml            # Prompt management
│   │   ├── UsageWindow.xaml              # Usage statistics
│   │   └── OnboardingWindow.xaml         # First-run wizard
│   ├── Controls/
│   │   └── RecordButton.xaml             # Animated record button
│   ├── ViewModels/                       # MVVM ViewModels
│   ├── Styles/                           # XAML resources
│   └── Strings/                          # Localization
│
├── DictateForWindows.Core/               # Core library
│   ├── Services/
│   │   ├── Activation/                   # Hotkey services
│   │   ├── Audio/                        # Audio recording (NAudio)
│   │   ├── Transcription/                # Whisper API clients
│   │   ├── Rewording/                    # Chat API clients
│   │   ├── TextInjection/                # Clipboard injection
│   │   └── Settings/                     # JSON settings
│   ├── Data/                             # SQLite repositories
│   ├── Models/                           # Data models
│   ├── Utilities/                        # Helper classes
│   └── Constants/                        # Settings keys, pricing
│
└── DictateForWindows.Installer/          # WiX/Inno Setup project
```

## Building

### Prerequisites

- Visual Studio 2022 17.8+
- .NET 8.0 SDK
- Windows App SDK 1.6+

### Build Commands

```bash
# Restore packages
dotnet restore DictateForWindows.sln

# Build Debug
dotnet build DictateForWindows.sln -c Debug

# Build Release
dotnet build DictateForWindows.sln -c Release

# Run
dotnet run --project src/DictateForWindows/DictateForWindows.csproj
```

## Features

### Phase 1: Foundation
- [x] Project structure and dependencies
- [x] WinUI 3 application shell
- [x] Global hotkey activation (Win+Shift+D)
- [x] Settings service (JSON-based)

### Phase 2: Audio Recording
- [x] NAudio-based audio capture
- [x] M4A (AAC) encoding
- [x] Device enumeration
- [x] Bluetooth microphone detection

### Phase 3: Transcription
- [x] OpenAI Whisper API integration
- [x] Groq API integration
- [x] Custom server support
- [x] 60+ language support
- [x] Retry logic with error handling

### Phase 4: Text Injection
- [x] Clipboard-based injection
- [x] Original clipboard preservation
- [x] Character animation mode
- [x] Auto-enter option

### Phase 5: Rewording & Prompts
- [x] GPT chat API integration
- [x] SQLite prompt database
- [x] Prompt queue system
- [x] Auto-apply prompts
- [x] Auto-formatting

### Phase 6: UI & Polish
- [x] Settings window
- [x] Prompts management
- [x] Usage dashboard
- [x] Onboarding wizard
- [x] Theme support

### Phase 7: Distribution
- [ ] WiX/Inno Setup installer
- [ ] Auto-update mechanism
- [ ] Microsoft Store package

## API Providers

### OpenAI
- Transcription: whisper-1, gpt-4o-transcribe, gpt-4o-mini-transcribe
- Rewording: gpt-4o-mini, gpt-4o, o4-mini, o3-mini, o1

### Groq
- Transcription: whisper-large-v3-turbo, whisper-large-v3
- Rewording: llama-3.3-70b-versatile, llama-3.1-8b-instant

## License

Apache 2.0 - See LICENSE file
