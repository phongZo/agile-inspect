# Plugin list

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

- **screen_lock**: screen-saver / inactivity lock timeout in seconds. `0` means not configured or disabled, `-1` indicates an error reading the value.

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
"secure_boot":{
"secureBootEnabled":true,
"tpmPresent":true
}
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
"hintList":[]
}
}

```