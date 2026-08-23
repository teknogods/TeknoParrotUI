# TeknoParrotUI Sunshine Integration Notes

Alongside the Sunshine-side work, the TeknoParrotUI side was updated to act as the management layer for the new Sunshine managed mode.

## Sunshine Management Screen

Added a dedicated Sunshine Host management screen under:

```text
Settings
  → Advanced Controls
  → Sunshine Host
```

The screen provides TeknoParrot-side control over the bundled Sunshine instance without requiring users to interact directly with Sunshine's Web UI for normal host management.

## Process Management

TeknoParrotUI now launches Sunshine using:

```text
sunshine.exe --managed --parent-pid <TeknoParrot PID>
```

This allows TeknoParrotUI to own the lifecycle of its bundled Sunshine instance.

Supported controls include:

```text
Start
Stop
Restart
```

The UI waits for Sunshine's managed API to become available after startup before treating the host as ready.

## Bundled Sunshine Process Detection

Process detection was hardened so TeknoParrotUI only manages the Sunshine executable bundled with TeknoParrot.

Instead of treating any process named:

```text
sunshine.exe
```

as its managed instance, TeknoParrotUI verifies that the running process path matches:

```text
<TeknoParrot install>\Sunshine\sunshine.exe
```

This prevents TeknoParrot from accidentally controlling or terminating a separate standalone Sunshine installation that a user may already have running.

The emergency force-stop fallback is restricted to the bundled executable as well.

## Host Connection Controls

The TeknoParrot management UI exposes a simplified connection mode:

```text
Open
Closed
```

Changes are sent through Sunshine's managed API.

The state stays synchronized in both directions:

```text
TeknoParrotUI changes Open/Closed
        ↓
Sunshine tray updates

Sunshine tray changes Open/Closed
        ↓
TeknoParrotUI updates
```

Sunshine's standalone Auto-Close functionality is left intact, but is intentionally not exposed in the TeknoParrot management screen.

## Pairing Workflow

TeknoParrotUI now handles the user-facing pairing workflow while Sunshine runs in managed mode.

The UI polls Sunshine for pairing state and displays statuses such as:

```text
Waiting on pairing requests
Pairing request is currently waiting
Pairing...
Pairing accepted by Sunshine.
Pairing failed.
```

When a Moonlight client requests pairing, TeknoParrotUI can detect that request without relying on Sunshine's normal tray popup.

The user can then enter:

```text
Moonlight PIN
Connection Name
```

and submit the pairing request directly through Sunshine's managed API.

After a successful pairing, the fields are cleared and the UI returns to the normal waiting state.

## Moonlight Client Management

Added a paired-client list directly to TeknoParrotUI.

The UI can:

```text
View paired clients
Refresh client information
Unpair a selected client
Disconnect all active sessions
```

The selected client's UUID is preserved across list refreshes where possible.

## Paired vs Connected State

The client list now distinguishes between a Moonlight device that is simply authorized and one that is actively streaming.

Example states:

```text
Living Room PC — Connected
Laptop — Paired • Offline
```

Disabled clients can also retain their disabled status in the display.

The summary beneath the list reports both totals, for example:

```text
2 paired client(s) • 1 connected
```

## Automatic Client-State Refresh

Previously, a newly paired or newly connected client might not appear updated until the user manually clicked Refresh.

The Sunshine management screen now refreshes host and client state automatically on its existing one-second polling cycle.

This allows the UI to update automatically when:

```text
A pairing request begins
A pairing request completes
A Moonlight stream starts
A Moonlight stream stops
A client becomes connected
A client returns to paired/offline
```

No manual Refresh is required for normal state changes.

## Managed Host Status

The UI displays Sunshine host information obtained from the managed API, including:

```text
Running / stopped state
Sunshine version
Managed API availability
Open / Closed state
Active stream count
Paired client count
Pairing request state
```

The screen also handles the intermediate state where Sunshine is running but its HTTPS API has not finished starting yet.

## Local HTTPS Handling

Because Sunshine's local managed API uses its local HTTPS certificate, TeknoParrotUI includes narrowly scoped certificate handling for:

```text
https://127.0.0.1:47990
https://localhost:47990
```

The bypass is limited to the local Sunshine API rather than globally accepting invalid certificates for arbitrary HTTPS requests.

TLS 1.2 support is explicitly enabled for the existing .NET Framework 4.6.2 application.

## Sunshine Lifecycle

The TeknoParrotUI process ID is passed to Sunshine at startup.

Because Sunshine now monitors that PID, the bundled Sunshine instance shuts down when:

```text
TeknoParrotUI closes normally
TeknoParrotUI is terminated unexpectedly
TeknoParrotUI is killed through Task Manager
```

This avoids leaving an orphaned managed Sunshine process running after the TeknoParrot UI disappears.

## Portable Sunshine Bundling

TeknoParrotUI is now set up to use a complete portable Sunshine package rather than relying on an independently installed copy.

The source-of-truth layout in the project is:

```text
TeknoParrotUi\
  Dependencies\
    Sunshine\
      sunshine.exe
      ...
```

The project copies the entire Sunshine dependency tree during build into:

```text
bin\x86\Debug\Sunshine\
```

or:

```text
bin\x86\Release\Sunshine\
```

depending on the selected build configuration.

The Sunshine files are also visible inside Visual Studio under:

```text
Dependencies
  → Sunshine
```

This means developers can update the bundled Sunshine build by replacing the contents of:

```text
Dependencies\Sunshine\
```

instead of manually copying files into the `bin` directory.

The `bin` copy is now treated as disposable build output.

## Deployment Model

The intended deployment layout is:

```text
TeknoParrot\
  TeknoParrotUi.exe
  Sunshine\
    sunshine.exe
    ...
```

TeknoParrotUI always launches that bundled copy and sets the Sunshine directory as its working directory so Sunshine's packaged assets continue to resolve correctly.

Users do not need to separately install the custom Sunshine build.

## Overall Result

The TeknoParrotUI side now provides a much more integrated host experience:

```text
TeknoParrotUI
    ↓
Starts bundled Sunshine in managed mode
    ↓
Controls host Open/Closed state
    ↓
Detects pairing requests
    ↓
Accepts Moonlight PINs
    ↓
Lists paired clients
    ↓
Shows Connected vs Paired/Offline
    ↓
Allows unpair/disconnect operations
    ↓
Automatically tracks host/client state
    ↓
Shuts Sunshine down when TeknoParrot exits
```

The goal is to keep Sunshine responsible for streaming, pairing, networking, certificates, and session management while TeknoParrotUI provides the user-facing controls and lifecycle management.
