# ATAMO Walkthroughs & Examples

This document provides practical guides for using ATAMO, with links to example code in the WinForms demo app.

## 1. Submitting an Event (Fire-and-Forget)
- Open `src/Atamo.WinFormsDemo/MainForm.cs`.
- Enter an event type and click Submit.
- The app calls `SubmitEventAsync` on the hub, which enqueues the event for processing.

## 2. Receiving Progress Updates
- The demo registers a subscriber callback with `RegisterSubscriberAsync`.
- Progress messages from agent dispatches are displayed in the list box.

## 3. Registering Agents
- In production, agents are registered via `IHubControl.RegisterAgentAsync`.
- The in-memory hub demo auto-registers agents for demonstration.

## 4. Viewing Agent Actions
- See `SampleAgent.cs` in `Atamo.Agents.Samples` for a simple agent implementation.
- Agents process actions and emit progress/results.

## 5. Extending with Custom Agents or Providers
- Implement the `IAgent` or `IConfigProvider` interface in your own class/library.
- Register with the hub via `IHubControl`.

## 6. Building Responsive Apps
- Use the hub in stateless mode (as in WinForms demo) for async, multi-threaded workflows.
- Use stateful service mode for guaranteed delivery and audit.

---
For API details, see [API_Interfaces.md](API_Interfaces.md).
For architecture and component descriptions, see [Components.md](Components.md).
