# Date Countdown Agent Guide

## Product direction

Date Countdown should feel like a native part of Windows 11: effortless, calm, familiar, and coherent. Use WinUI 3 and Windows App SDK features first, and follow current Fluent guidance rather than inventing a parallel design language.

The countdown is the product. Keep the primary window focused on the remaining time and its title, with as little supporting text and chrome as possible. Every persistent element must earn its place. Put infrequent or advanced capabilities, such as desktop overlay customization, in Settings or another progressive-disclosure surface instead of crowding the main experience.

Preserve these qualities in every change:

- **Native:** Prefer built-in WinUI controls, system typography, Mica and other appropriate system materials, standard window behavior, and Windows-supported APIs.
- **Minimal:** Do not add explanatory copy, cards, labels, buttons, or status UI unless they help the user make a necessary decision or complete a core task.
- **Intuitive:** Favor familiar Windows interactions, clear hierarchy, sensible defaults, and discoverable settings over instructions on the main surface.
- **Polished:** Use consistent spacing and alignment, purposeful context-appropriate motion, and complete hover, pressed, focus, disabled, empty, and error states. Respect the system reduced-motion setting.
- **Low cognitive load:** Keep advanced features out of the primary flow. When new capability creates complexity, prefer progressive disclosure over exposing more controls at once.
- **Accessible and adaptable:** Preserve keyboard access, Narrator/UI Automation semantics, high contrast, localization, text scaling, and responsive behavior.

## Architecture

- Put countdown rules, formatting, scheduling, preferences, serialization, and other deterministic behavior in `DateCountdown.Core` with unit tests.
- Keep `DateCountdown` responsible for WinUI presentation and Windows integration. Reuse shared behavior instead of implementing feature-specific variants in the main window, desktop overlay, widget provider, or CLI.
- Prefer a structural fix at the correct layer over an event-handler workaround or duplicated special case.
- Preserve the existing MSIX packaging, widget, CLI, startup, and desktop-overlay contracts unless the change explicitly intends to update them.

More detailed app-specific implementation guidance lives in [`DateCountdown/AGENTS.md`](DateCountdown/AGENTS.md) and its linked instruction files.

## Contributions

Keep the process lightweight. A pull request should:

1. Explain the user need and why the change fits the product direction above.
2. Stay focused; avoid unrelated dependency, formatting, or project-file churn.
3. Add or update tests for changed behavior, including boundaries and failure paths.
4. Build cleanly and pass the full unit-test suite.
5. For UI changes, include stable `AutomationProperties.AutomationId` values for important controls and verify the affected states at compact, medium, and large window sizes in both light and dark themes.
6. Note any accessibility, localization, performance, power-use, persistence, or compatibility impact.

Use the platform matching the development machine:

```powershell
dotnet build DateCountdown\DateCountdown.csproj -c Release -p:Platform=x64
dotnet test DateCountdown.Tests\DateCountdown.Tests.csproj -c Release -p:Platform=x64
```

Replace `x64` with `x86` or `ARM64` when appropriate.
