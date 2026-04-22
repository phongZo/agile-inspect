# Test new plugins

## 1) Prerequisites
- Build project in `Debug|x86` (or your normal run profile).
- Make sure new event types exist in `inspect_store.cfg`:
  - `remote_access_tools`
  - `cloud_sync_clients`
  - `personal_messaging_apps`
  - `usb_device`
  - `unknown_bluetooth`
  - `screen_sharing`
  - `virtual_machine`
- Set short test intervals (ex: `10` seconds) for faster verification.

## 2) Run AgileInspect
- Start `AgileInspect`.
- Confirm plugin DLLs are generated under `AgileInspect/bin/x86/Debug/net8.0-windows/win-x86/dll/`.
- Check logs to ensure each plugin starts without errors.

## 3) How to verify each plugin

### A. Remote access tools (`remote_access_tools`)
**Test action**
- Install/open one known tool: TeamViewer or AnyDesk.

**Expected**
- Event data contains:
```json
{
  "remote_access_tools": true,
  "appList": ["TeamViewer"]
}
```
- When uninstalled/closed and no matched tool remains:
```json
{
  "remote_access_tools": false,
  "appList": []
}
```

### B. Cloud sync clients (`cloud_sync_clients`)
**Test action**
- Open Dropbox desktop or Google Drive desktop app.

**Expected**
```json
{
  "cloud_sync_clients": true,
  "appList": ["Dropbox"]
}
```
- Close all cloud sync apps:
```json
{
  "cloud_sync_clients": false,
  "appList": []
}
```

### C. Personal messaging apps (`personal_messaging_apps`)
**Test action**
- Open WhatsApp Desktop or Telegram Desktop.

**Expected**
```json
{
  "personal_messaging_apps": true,
  "appList": ["Telegram"]
}
```
- Close all matched apps:
```json
{
  "personal_messaging_apps": false,
  "appList": []
}
```

### D. USB device (`usb_device`)
**Test action**
- Plug in a USB removable drive.

**Expected**
```json
{
  "usb_device": true,
  "driveList": ["E:\\ (KINGSTON)"]
}
```
- Unplug removable drives:
```json
{
  "usb_device": false,
  "driveList": []
}
```

### E. Unknown bluetooth (`unknown_bluetooth`)
**Test action**
- Pair a bluetooth device with a generic/unknown name (or simulate with test device).

**Expected**
```json
{
  "unknown_bluetooth": true,
  "deviceList": ["Bluetooth Device (Unknown)"]
}
```
- Remove/unpair unknown device:
```json
{
  "unknown_bluetooth": false,
  "deviceList": []
}
```

### F. Screen sharing (`screen_sharing`)
**Test action**
- Start a screen share in Teams/Zoom/Webex or open OBS.

**Expected**
```json
{
  "screen_sharing": true,
  "appList": ["Microsoft Teams"]
}
```
- Stop sharing and close related apps:
```json
{
  "screen_sharing": false,
  "appList": []
}
```

### G. Virtual machine (`virtual_machine`)
**Test action**
- Run on a VM machine (VMware/VirtualBox/Hyper-V guest), then run on physical machine.

**Expected on VM**
```json
{
  "virtual_machine": true,
  "hintList": ["VMware, Inc."]
}
```

**Expected on physical**
```json
{
  "virtual_machine": false,
  "hintList": []
}
```

## 4) Quick validation checklist
- Each plugin emits event at configured interval.
- `eventType` matches plugin mapping.
- `data` keys match exactly:
  - `remote_access_tools` + `appList`
  - `cloud_sync_clients` + `appList`
  - `personal_messaging_apps` + `appList`
  - `usb_device` + `driveList`
  - `unknown_bluetooth` + `deviceList`
  - `screen_sharing` + `appList`
  - `virtual_machine` + `hintList`
- True/false changes correctly when state changes.
- List content matches real running apps/devices.
