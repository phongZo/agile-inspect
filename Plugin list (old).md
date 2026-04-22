## Plugin list

| Plugin Name | Event Type |
| --- | --- |
| WatermarkDetectorPlugin | `watermark` |
| BrightnessChangePlugin | `brightness` |
| AntivirusDetectorPlugin | `antivirus` |
| FirewallDetectorPlugin | `firewall` |
| InternetDetectorPlugin | `internet` |
| VpnDetectorPlugin | `vpn` |
| ExternalDiskDetectorPlugin | `external_disk` |
| HostFileMonitorPlugin | `host_file` |
| ClipboardMonitorPlugin | `clipboard` |
| PrintDetectorPlugin | `print` |
| PrintScreenDetectorPlugin | `print_screen` |
| FocusWindowDetectorPlugin | `focus_window` |
| AiInteractionDetectorPlugin | `ai_interaction` |
| ScreenLockPlugin | `screen_lock` |
| AutoUpdatePlugin | `auto_update` |
| UserPlugin | `user` |
| SecureBootPlugin | `secure_boot` |
| RdpPlugin | `rdp` |
| DevModePlugin | `dev_mode` |
| RemoteAccessToolsPlugin | `remote_access_tools` |
| CloudSyncClientsPlugin | `cloud_sync_clients` |
| PersonalMessagingAppsPlugin | `personal_messaging_apps` |
| UsbDevicePlugin | `usb_device` |
| UnknownBluetoothPlugin | `unknown_bluetooth` |
| ScreenSharingPlugin | `screen_sharing` |
| VirtualMachinePlugin | `virtual_machine` |

---

## Event settings

```json
{
  "eventSettings": [
    {
      "eventType": "ai_interaction",
      "triggerType": "realtime",
      "eventParams": {
        "browsers": ["chrome", "msedge", "firefox", "brave", "opera", "coccoc"],
        "aiDomains": [
          "openai.com",
          "chatgpt.com",
          "anthropic.com",
          "claude.ai",
          "githubcopilot.com",
          "api.githubcopilot.com",
          "copilot-proxy.githubusercontent.com",
          "generativelanguage.googleapis.com",
          "perplexity.ai",
          "x.ai",
          "grok.x.ai",
          "deepseek.com",
          "mistral.ai",
          "huggingface.co",
          "blackbox.ai",
          "phind.com",
          "you.com",
          "jasper.ai",
          "copy.ai",
          "rytr.me",
          "writesonic.com",
          "poe.com",
          "quora.com",
          "lmstudio.ai",
          "ollama.com",
          "grok.com"
        ]
      },
      "triggerParams": { "interval": 10 }
      // comment: AiInteractionDetectorPlugin currently ignores triggerParams.interval
    },

    { "eventType": "watermark", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 60 } },

    { "eventType": "brightness", "eventParams": {}, "triggerType": "realtime/interval", "triggerParams": { "interval": 10 } },

    { "eventType": "antivirus", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 60 } },

    { "eventType": "firewall", "eventParams": {}, "triggerType": "realtime/interval", "triggerParams": { "interval": 60 } },

    {
      "eventType": "internet",
      "eventParams": { "allowedInternetSsids": ["Khanh Huy_5G", "Khanh Huy"] },
      "triggerType": "realtime/interval",
      "triggerParams": { "interval": 60 }
    },

    { "eventType": "vpn", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 60 } },

    { "eventType": "external_disk", "eventParams": {}, "triggerType": "realtime/interval", "triggerParams": { "interval": 60 } },

    { "eventType": "host_file", "eventParams": {}, "triggerType": "realtime/interval", "triggerParams": { "interval": 60 } },

    { "eventType": "clipboard", "eventParams": {}, "triggerType": "realtime/interval", "triggerParams": { "interval": 60 } },

    { "eventType": "focus_window", "eventParams": {}, "triggerType": "realtime", "triggerParams": { "interval": 60 } },

    { "eventType": "print", "eventParams": {}, "triggerType": "realtime", "triggerParams": { "interval": 60 } },

    { "eventType": "print_screen", "eventParams": {}, "triggerType": "realtime", "triggerParams": { "interval": 60 } },

    { "eventType": "screen_lock", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 60 } },

    { "eventType": "auto_update", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 1800 } },

    { "eventType": "user", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 1800 } },

    { "eventType": "secure_boot", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 1800 } },

    { "eventType": "rdp", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 60 } },

    { "eventType": "dev_mode", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 1800 } },

    { "eventType": "remote_access_tools", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 300 } },

    { "eventType": "cloud_sync_clients", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 120 } },

    { "eventType": "personal_messaging_apps", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 60 } },

    { "eventType": "usb_device", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 30 } },

    { "eventType": "unknown_bluetooth", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 120 } },

    { "eventType": "screen_sharing", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 60 } },

    { "eventType": "virtual_machine", "eventParams": {}, "triggerType": "interval", "triggerParams": { "interval": 1800 } }
  ]
}
```

## Output examples (agent → server)

### Brightness (`brightness`)

```json
{
"eventType":"brightness",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{"brightness":62}
}
```

### Watermark (`watermark`)

```json
{
"eventType":"watermark",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{"watermark":false}
}
```

### Antivirus (`antivirus`)

```json
{
"eventType":"antivirus",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"appList":[
{
"name":"Kaspersky Anti-Virus",
"version":"21.3.10.391",
"signatureVersion":"",
"signatureLastUpdated":"",
"state":"off",
"localUpToDate":true,
"stateTimestamp":"Tue, 16 Sep 2025 02:55:52 GMT"
},
{
"name":"Microsoft Defender Antivirus",
"version":"4.18.25070.5",
"signatureVersion":"1.435.768.0",
"signatureLastUpdated":"2025-09-16 00:51:53",
"state":"off",
"localUpToDate":true,
"stateTimestamp":"Tue, 16 Sep 2025 03:09:26 GMT"
},
{
"name":"Avast Antivirus",
"version":"21.4.6162.0",
"signatureVersion":"",
"signatureLastUpdated":"",
"state":"snoozed",
"localUpToDate":true,
"stateTimestamp":"Tue, 16 Sep 2025 03:56:44 GMT"
}
]
}
}
```

**Field notes**

- **signatureVersion** *(Defender only)*: Microsoft Defender signature/definition DB version.
- **signatureLastUpdated** *(Defender only)*: last time Defender updated definitions.
- **state**: `on` / `off` / `snoozed` / `expired` (vendor/WSC dependent).
- **localUpToDate**: vendor says definitions are up-to-date (from Windows Security Center integration).
- **stateTimestamp**: when the AV state last changed.

### Firewall (`firewall`)

```json
{
"eventType":"firewall",
"clientName":"DESKTOP-VBP57D5:nd24",
"customerId":"60f773a842963f002e73a25b",
"data":{"firewall":true}
}
```

### Internet (`internet`)

```json
{
"eventType":"internet",
"clientName":"DESKTOP-VBP57D5:nd24",
"customerId":"60f773a842963f002e73a25b",
"data":{"internet":true}
}
```

### VPN (`vpn`)

```json
{
"eventType":"vpn",
"clientName":"DESKTOP-VBP57D5:nd24",
"customerId":"60f773a842963f002e73a25b",
"data":{"vpn":true}
}
```

### External Disk (`external_disk`)

```json
{
"eventType":"external_disk",
"clientName":"DESKTOP-VBP57D5:nd24",
"customerId":"60f773a842963f002e73a25b",
"data":{"external_disk":true}
}
```

### Host File (`host_file`)

```json
{
"eventType":"host_file",
"clientName":"DESKTOP-VBP57D5:nd24",
"data":{"host_file":true}
}
```

### Clipboard (`clipboard`)

```json
{
"eventType":"clipboard",
"clientName":"DESKTOP-VBP57D5:nd24",
"customerId":"60f773a842963f002e73a25b",
"data":{
"event":"CLIPBOARD_UPDATED",
"type":"text",
"preview":"copied text content here (max 100 chars)"
}
}
```

**Field notes**

- **type**: `text` / `image` / `files` / `unknown` — the first supported clipboard format detected (Win32 format priority order).
- **preview**: for `text` — first 100 characters; for `files` — "Files count: N"; for `image` — "Image copied".

### Print Screen (`print_screen`)

```json
{
"eventType":"print_screen",
"clientName":"DESKTOP-VBP57D5:nd24",
"customerId":"60f773a842963f002e73a25b",
"data":{"timestamp":"2026-01-15 22:43:42"}
}
```

### Focus Window (`focus_window`)

```json
{
"eventType":"focus_window",
"clientName":"DESKTOP-VBP57D5:nd24",
"customerId":"60f773a842963f002e73a25b",
"data":{
"processName":"Code",
"windowTitle":"agileinspect_event_log.txt - Visual Studio Code",
"pid":18420,
"timestamp":"2026-01-20 15:08:35"
}
}
```

### Print (`print`)

```json
{
"eventType":"print",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"timestamp":"2026-01-29 11:00:00",
"file_name":"Document1",
"printer_name":"Microsoft Print to PDF",
"owner":"buisi"
}
}
```

### AI Interaction (`ai_interaction`)

```json
{
"eventType":"ai_interaction",
"clientName":"DESKTOP-VBP57D5:nd24",
"customerId":"60f773a842963f002e73a25b",
"data":{
"isOn":true,
"keyword":"ChatGpt.com",
"process":"ChatGpt",
"type":"web/process"
}
// comment: in the normalized examples below, data.type is "web" or "process"
}
```

**Normalized examples**

```json
{
"eventType":"ai_interaction",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{"isOn":true,"keyword":"chatgpt.com","process":"chrome","type":"web"}
}
```

```json
{
"eventType":"ai_interaction",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{"isOn":true,"keyword":"githubcopilot.com","process":"copilot","type":"process"}
}
```

### Screen Lock (`screen_lock`)

```json
{
"eventType":"screen_lock",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{"screen_lock":600}
}
```

**Field notes**

- **screen_lock**: screen-saver / inactivity lock timeout in seconds. `0` means not configured or disabled, `1` indicates an error reading the value.

### Auto Update (`auto_update`)

```json
{
"eventType":"auto_update",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{"auto_update":true}
}
```

**Field notes**

- **auto_update**: `true` if Windows auto-update is **disabled** (via Group Policy `NoAutoUpdate=1` or `AUOptions=1`), `false` if auto-update is enabled.

### User (`user`)

```json
{
"eventType":"user",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"user":{
"username":"buisi",
"isAdmin":true,
"userType":"admin"
}
}
}
```

**Field notes**

- **username**: current Windows session user name.
- **isAdmin**: `true` if the user is a member of the Administrators group.
- **userType**: `"admin"` or `"standard"`.

### Secure Boot (`secure_boot`)

```json
{
"eventType":"secure_boot",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"secureBootEnabled":true,
"tpmPresent":true
}
}
```

**Field notes**

- **secureBootEnabled**: `true` if UEFI Secure Boot is enabled.
- **tpmPresent**: `true` if a TPM (Trusted Platform Module) is detected on the machine.

### RDP (`rdp`)

```json
{
"eventType":"rdp",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{"rdp":true}
}
```

**Field notes**

- **rdp**: `true` if Remote Desktop (RDP) is enabled (`fDenyTSConnections=0`), `false` if disabled.

### Developer Mode (`dev_mode`)

```json
{
"eventType":"dev_mode",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{"dev_mode":true}
}
```

**Field notes**

- **dev_mode**: `true` if Windows Developer Mode is enabled (`AllowDevelopmentWithoutDevLicense=1`), `false` otherwise.

### Remote Access Tools (`remote_access_tools`)

```json
{
"eventType":"remote_access_tools",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"remote_access_tools":true,
"appList":["AnyDesk","TeamViewer"]
}
}
```

### Cloud Sync Clients (`cloud_sync_clients`)

```json
{
"eventType":"cloud_sync_clients",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"cloud_sync_clients":true,
"appList":["Dropbox","Google Drive"]
}
}
```

### Personal Messaging Apps (`personal_messaging_apps`)

```json
{
"eventType":"personal_messaging_apps",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"personal_messaging_apps":true,
"appList":["Telegram","WhatsApp"]
}
}
```

### USB Device (`usb_device`)

```json
{
"eventType":"usb_device",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"usb_device":true,
"driveList":["E:\\ (KINGSTON)"]
}
}
```

### Unknown Bluetooth (`unknown_bluetooth`)

```json
{
"eventType":"unknown_bluetooth",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"unknown_bluetooth":true,
"deviceList":["Bluetooth Device (Unknown)"]
}
}
```

### Screen Sharing (`screen_sharing`)

```json
{
"eventType":"screen_sharing",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"screen_sharing":true,
"appList":["Microsoft Teams"]
}
}
```

### Virtual Machine (`virtual_machine`)

```json
{
"eventType":"virtual_machine",
"clientName":"DESKTOP-28USAMR:buisi",
"customerId":"60f773a842963f002e73a25b",
"data":{
"virtual_machine":false,
"appList":[]
}
}
```

### The following user or system behaviors are currently detectable by the agent:

1. **Watermark Status (`watermark`)**
    
    Detection of whether the AgileMark watermark is enabled or disabled (visibility state).
    
2. **Screen Brightness Status (`brightness`)**
    
    Detection of screen brightness level changes.
    
3. **Antivirus Status (`antivirus`)**
    
    Detection of antivirus presence and operational state (enabled / disabled / snoozed / expired), including signature/update status where available.
    
4. **Firewall Status (`firewall`)**
    
    Detection of whether the system firewall is enabled or disabled.
    
5. **VPN Connection Status (`vpn`)**
    
    Detection of active VPN connections (connected / not connected).
    
6. **External Disk Usage (`external_disk`)**
    
    Detection of external storage devices (USB drives, external HDD/SSD) being connected.
    
7. **Hosts File Modification (`host_file`)**
    
    Monitoring changes to the system hosts file.
    
8. **Clipboard / Copy Activity (`clipboard`)**
    
    Detection of clipboard update events (e.g., copied text, image, or files).
    
9. **Print Activity (`print`)**
    
    Detection of when a user prints a document, including:
    
    - when it was printed
    - the document name
    - which printer was used
    - who printed it
10. **Screen Capture Attempts (`print_screen`)**
    
    Detection of screenshot / Print Screen attempts.
    
11. **Application in Focus (`focus_window`)**
    
    Detection of the currently active (foreground) application/window (process name, window title, PID).
    
12. **Use of ChatGPT / LLM Tools (`ai_interaction`)**
    
    Detection of access to known LLM/AI websites or related desktop processes.
    
13. **Screen Lock Timeout Setting (`screen_lock`)**
    
    Detection of screen saver / inactivity lock timeout configuration (in seconds).
    
14. **Windows Auto Update Policy Status (`auto_update`)**
    
    Detection of whether Windows auto-update is disabled via policy/registry configuration.
    
15. **Current User & Privilege Level (`user`)**
    
    Detection of current logged-in user information, including whether the user is an administrator or standard user.
    
16. **Secure Boot / TPM Status (`secure_boot`)**
    
    Detection of whether UEFI Secure Boot is enabled and whether TPM is present.
    
17. **Remote Desktop (RDP) Status (`rdp`)**
    
    Detection of whether Remote Desktop is enabled or disabled.
    
18. **Developer Mode Status (`dev_mode`)**
    
    Detection of whether Windows Developer Mode is enabled.
    
19. **Corporate Wi-Fi Connection** *(via `internet` event policy / allowed SSID configuration)*
    
    Detection of whether the device is connected to an approved corporate Wi-Fi SSID (based on configured allowed SSIDs).
    
20. **Remote Access Tools Installed (`remote_access_tools`)**
    
    Detection of installed remote-access tools (for example TeamViewer, AnyDesk, RustDesk, and similar software).
    
21. **Cloud Sync Clients Active (`cloud_sync_clients`)**
    
    Detection of active cloud-sync desktop clients (for example Dropbox, Google Drive, OneDrive, Box, iCloud, and similar tools).
    
22. **Personal Messaging Apps Open (`personal_messaging_apps`)**
    
    Detection of opened personal messaging desktop applications (for example WhatsApp, Telegram, Signal, LINE, Discord, and similar apps).
    
23. **USB Device Connection (`usb_device`)**
    
    Detection of connected removable USB storage devices.
    
24. **Unknown Bluetooth Paired (`unknown_bluetooth`)**
    
    Detection of unknown or generic Bluetooth paired devices.
    
25. **Screen Sharing Activity (`screen_sharing`)**
    
    Detection of potential screen-sharing or screen-capture activity from supported applications.
    
26. **Virtualization / VM Activity (`virtual_machine`)**
    
    Detection of virtual-machine environment indicators and active VM runtime/process activity.
    

### **Actions We Can Perform Now**

**Based on the detected behaviors above, the agent can immediately perform the following actions:**

1. Dynamic Watermark Control
    - Change AgileMark watermark opacity.
    - Display custom watermark messages.
2. User Notification (Doing)
    - Show toast notifications or system notifications to inform or warn the user when a policy-related behavior is detected.