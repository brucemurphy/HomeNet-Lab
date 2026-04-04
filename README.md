# 🏠 HomeNet Lab

**Automated secure remote access portal for home networks**

Publishes password-protected web portals with dynamic IP updates, multi-service access, and Remote Desktop connections to host PCs and virtual machines.

---

## ⚠️ Prerequisites

### Router Port Forwarding (Required for RDP)
Configure your router to forward ports to your PC:
- **Host PC:** Port 3389 (TCP) → Your PC's local IP
- **Virtual PCs:** Ports 3390, 3391, etc. → Your PC's local IP
- **Tool:** Use File → Setup Port Forwarding... for netsh commands (copies to clipboard)

### FTP Hosting
- Web host with FTP upload capability
- FTP credentials (server, username, password)

---

## 🚀 Quick Start

### 1. Configure
**Option A: Use the Built-in Editor (Recommended)**
1. Launch `HomeNet Lab.exe`
2. Right-click system tray icon → **Show Window**
3. Click **File → Edit Config.xml**
4. Fill in your FTP credentials and settings
5. Optionally check "Save as PC-specific config"
6. Click **💾 Save**

**Option B: Edit XML Manually**  
Edit `config.xml` or create `[PCNAME].xml` for PC-specific settings:

```xml
<Configuration>
  <FTP>
    <Server>ftp://your-server.com</Server>
    <Username>username</Username>
    <Password>password</Password>
    <RemotePath>/public_html/</RemotePath>
    <HtmlFileName>index.html</HtmlFileName>
  </FTP>

  <Security>
    <AccessPassword>YourSecurePassword</AccessPassword>
  </Security>

  <RDPTargets>
    <Target>
      <Name>Host PC</Name>
      <Port>3389</Port>
      <FileName>host.rdp</FileName>
      <Enabled>true</Enabled>
    </Target>
    <Target>
      <Name>VPC - WinServer</Name>
      <Port>3390</Port>
      <FileName>vpc-server.rdp</FileName>
      <Enabled>true</Enabled>
    </Target>
  </RDPTargets>

  <Services>
    <Service>
      <Name>Emby Server</Name>
      <Port>8096</Port>
      <Enabled>true</Enabled>
    </Service>
  </Services>

  <AutoPublish>
    <Enabled>true</Enabled>
    <FrequencySeconds>300</FrequencySeconds>
  </AutoPublish>
</Configuration>
```

### 2. Run
- Launch `HomeNet Lab.exe`
- App starts minimized in system tray
- Publishes portal immediately with current IP
- Auto-publishes every 5 minutes
- **Single instance only** - prevents duplicate launches

### 3. Access
- Visit your portal: `http://yoursite.com/index.html`
- Enter password to access services or download RDP files
- Connect to host PC or virtual machines remotely

---

## 🎯 Key Capabilities

### Auto-Configuration
- **PC-Specific Configs:** App loads `[PCNAME].xml` automatically or falls back to `config.xml`
- **PC Branding:** Web portals show PC name when using custom configs (e.g., "SERVER-01 • Secure Access Portal")
- **Silent Startup:** Launches to system tray with no notifications
- **Single Instance:** Prevents multiple instances from running

### Publishing
- **Instant Publish:** Updates portal immediately on startup
- **Auto-Publish:** Configurable interval (minimum 60s, default 300s)
- **Manual Publish:** File menu or system tray right-click
- **Multi-File Upload:** Uploads all RDP files + HTML portal

### Multi-RDP Support
- **Host PC Access:** Standard RDP on port 3389
- **Virtual PC Access:** Additional RDP targets on ports 3390, 3391, etc.
- **Separate Buttons:** Each RDP target gets its own button on web portal
- **Port Configuration:** Each target specifies port and filename

### Monitoring
- **Connection Status:** Checks internet every 5 seconds
- **IP Updates:** External and local IP auto-refresh
- **Activity Light:** Visual indicator (green=connected, blinking=uploading, gray=offline)
- **Live Dashboard:** Real-time config and status display
- **Daily Wallpaper Refresh:** Bing wallpaper updates automatically every 24 hours

---

## 💓 Heartbeat & Auto-Refresh

**New in v1.3!** The web portal now features intelligent monitoring and automatic refresh.

### How It Works

1. **Publish Timestamp:** Each time the portal is published, it embeds the current UTC timestamp
2. **Update Interval:** The configured `FrequencySeconds` value is embedded in the page
3. **Heartbeat Monitor:** JavaScript checks every 10 seconds if updates are arriving on time
4. **Auto-Refresh:** Page reloads automatically when the next update is expected

### Status Indicators

| Status | Color | Meaning | Calculation |
|--------|-------|---------|-------------|
| 🟢 Healthy | Green | Updates on schedule | < 1.5x interval |
| 🟡 Warning | Orange | Slightly overdue | 1.5x - 2x interval |
| 🔴 Stale | Red | Very overdue | > 2x interval |

### Example Timeline (300s interval)

```
00:00 - Page published, shows "Healthy"
02:00 - User opens page, sees "Healthy", refreshes at 05:05
05:00 - New version published
05:05 - Page auto-refreshes, loads new data
07:30 - Heartbeat turns "Warning" (no update at 10:00)
10:05 - Page tries to refresh, shows "Stale" if still no update
```

### Timezone Handling

- **Server:** Uses `DateTime.UtcNow` (Universal Time)
- **Display:** Timestamps show in **user's local timezone** automatically
- **Calculations:** All timing uses **UTC** for accuracy
- **Global Support:** Works correctly regardless of server or user location

### Benefits

✅ **Always Fresh Data** - Page reloads automatically  
✅ **Visual Status** - Instant health indicator  
✅ **Smart Timing** - Refreshes when update expected, not on fixed interval  
✅ **Timezone Safe** - Works globally without timezone issues  
✅ **No Manual Refresh** - Set it and forget it  

---

## 🔧 Configuration Reference

### FTP Settings
```xml
<FTP>
  <Server>ftp://server.com</Server>      <!-- FTP server address -->
  <Username>username</Username>           <!-- FTP login -->
  <Password>password</Password>           <!-- FTP password -->
  <RemotePath>/public_html/</RemotePath> <!-- Must end with / -->
  <HtmlFileName>index.html</HtmlFileName>
</FTP>
```

### Security
```xml
<Security>
  <AccessPassword>YourPassword</AccessPassword> <!-- SHA-256 hashed on portal -->
</Security>
```

### RDP Targets (Multi-Target Support)
```xml
<RDPTargets>
  <Target>
    <Name>Host PC</Name>              <!-- Display name on portal -->
    <Port>3389</Port>                 <!-- External port number -->
    <FileName>host.rdp</FileName>     <!-- RDP filename on FTP -->
    <Enabled>true</Enabled>           <!-- Show/hide on portal -->
  </Target>
</RDPTargets>
```

**Common Ports:**
- 3389: Host PC
- 3390-3399: Virtual PCs

**Legacy Fallback:** If `<RDPTargets>` section is missing, app uses `<RdpFileName>` from FTP section.

### Services (HTTP/HTTPS)
```xml
<Services>
  <Service>
    <Name>Service Name</Name>
    <Port>8096</Port>
    <Enabled>true</Enabled>
  </Service>
</Services>
```

**Common Services:**
- Emby: 8096
- Plex: 32400
- Home Assistant: 8123

### Auto-Publish
```xml
<AutoPublish>
  <Enabled>true</Enabled>
  <FrequencySeconds>300</FrequencySeconds> <!-- Min: 60, Recommended: 300 -->
</AutoPublish>
```

---

## 🖥️ Virtual PC (VPC) Setup

### Network Architecture
```
Internet → Router → Host PC → Virtual PCs
         (3389-3392)  (netsh)  (VPC RDP:3389)
```

### Step 1: Router Configuration
Forward ports to host PC:
```
External Port → Host PC Internal IP:Port
3389 → 192.168.1.100:3389   (Host PC)
3390 → 192.168.1.100:3390   (VPC #1)
3391 → 192.168.1.100:3391   (VPC #2)
```

### Step 2: Host PC Port Forwarding
**Option A:** Use Port Forwarding Wizard
1. Click **File → Setup Port Forwarding...**
2. Click **Yes** to copy netsh commands to clipboard
3. Open PowerShell as Administrator
4. Paste and run commands

**Option B:** Manual Commands
Run as Administrator on host PC:
```powershell
netsh interface portproxy add v4tov4 listenport=3390 connectaddress=192.168.100.10 connectport=3389
netsh interface portproxy add v4tov4 listenport=3391 connectaddress=192.168.100.11 connectport=3389

# Verify
netsh interface portproxy show all
```

### Step 3: Configure App
**Using the GUI Editor (Recommended):**
1. Open **File → Edit Config.xml**
2. Click **➕ Add** to add your RDP targets
3. Configure name, port, and filename for each target
4. Click **💾 Save**

**Or Edit XML Manually:**  
See `TEMPLATE-PCNAME.xml` for a complete example with multiple RDP targets:
```xml
<RDPTargets>
  <Target><Name>Host PC</Name><Port>3389</Port><FileName>host.rdp</FileName><Enabled>true</Enabled></Target>
  <Target><Name>VPC - WinServer</Name><Port>3390</Port><FileName>vpc-server.rdp</FileName><Enabled>true</Enabled></Target>
</RDPTargets>
```

### Result
Web portal displays:
- 🖥️ Host PC → downloads `host.rdp` (connects via port 3389)
- 🖥️ VPC - WinServer → downloads `vpc-server.rdp` (connects via port 3390 → routes to VPC)

---

## 🎨 User Interface

### System Tray (Primary Interface)
Right-click icon:
- **Publish Web Portal** - Upload now
- **Show Window** - View dashboard
- **Exit** - Close application

Double-click icon to show window.

### Dashboard Window
Shows real-time information:
- **Config File** - Active configuration file
- **FTP Server** - Upload destination
- **Services** - Enabled services
- **Ports** - Service port numbers
- **RDP Targets** - Configured RDP target names
- **RDP Ports** - RDP port numbers
- **Auto-Publish** - Status and frequency
- **Activity Light** - Connection status
- **IP Display** - External IP, PC name, local IP

### Menu Bar
- **File → Edit Config.xml** - Built-in configuration editor
- **File → Publish Web Portal** - Manual upload with result dialog
- **File → Setup Port Forwarding...** - VPC setup wizard
- **File → Exit** - Close application

---

## 📝 Configuration Editor

**New in v1.2!** Built-in GUI editor for complete configuration management.

### Access
Click **File → Edit Config.xml** from the menu bar.

### Features
- **Visual Editor:** No need to manually edit XML files
- **Smart Save:** Detects current config file (config.xml or PCNAME.xml)
- **PC-Specific Configs:** Option to save as `[PCNAME].xml` when using default config
- **Borderless Dialogs:** Modern, clean interface with rounded corners
- **Keyboard Shortcuts:** Enter to save, Escape to cancel
- **Validation:** Port numbers (1-65535), required fields, filename extensions
- **Live Reload:** Updates dashboard immediately after save

### Editable Settings
**FTP Settings:**
- FTP Server address
- Username
- Password (secure PasswordBox)
- Remote Path
- HTML Filename

**Security Settings:**
- Access Password (secure PasswordBox)

**Auto-Publish Settings:**
- Enable/Disable toggle
- Frequency in seconds

**Services:**
- ➕ Add new services
- ✏️ Edit existing services
- 🗑️ Remove services
- Configure: name, port, enabled status
- Visual list with live updates

**RDP Targets (Virtual PCs):**
- ➕ Add new RDP targets
- ✏️ Edit existing targets
- 🗑️ Remove targets
- Configure: name, port, filename, enabled status
- Visual list with live updates
- Automatic .rdp extension validation

### How to Use
1. Open **File → Edit Config.xml**
2. Modify desired settings in the form
3. Click **➕ Add** to add new services or RDP targets
4. Click **✏️ Edit** to modify selected items (or double-click)
5. Click **🗑️ Remove** to delete selected items
6. Toggle **Enabled** checkboxes directly in the list
7. *Optional:* Check "Save as PC-specific config" to create `[PCNAME].xml`
8. Click **💾 Save** to apply changes (or **❌ Cancel** to discard)
9. Dashboard updates automatically with new settings

### Dialog Features
- **Borderless design** with subtle borders for modern look
- **Close button (✕)** in top-right corner
- **Keyboard shortcuts:**
  - **Enter** - Save changes
  - **Escape** - Cancel and close
- **Real-time validation** - Prevents invalid entries
- **Auto-formatting** - Adds .rdp extension if missing

### Benefits
✅ No XML syntax knowledge required  
✅ Prevents configuration errors  
✅ Visual service management (add/edit/remove)  
✅ Visual RDP target management (add/edit/remove)  
✅ Port validation (1-65535)  
✅ Filename validation (.rdp extension)  
✅ Creates PC-specific configs easily  
✅ Shows which config file is active  
✅ Immediate feedback on save  
✅ Keyboard-friendly operation  
✅ Modern borderless UI with rounded buttons  

**Note:** All configuration is now manageable through the GUI editor!

---

## 🔧 Setup Port Forwarding Wizard

Access via **File → Setup Port Forwarding...**

**Features:**
1. Reads RDP targets from your config
2. Shows required router port forwarding rules
3. Generates netsh commands for host PC → VPC forwarding
4. Displays your PC name, IPs, and configuration
5. **Copies commands to clipboard** (click Yes)
6. Provides step-by-step execution instructions

**No automatic changes made** - full user control!

---

## 📱 Web Portal

### Features
- **Heartbeat Indicator:** Real-time status monitoring (Healthy/Warning/Stale)
- **Auto-Refresh:** Synchronized with publish interval for always-fresh data
- PC identification banner (when using custom configs)
- External IP and last update timestamp
- Password-protected access (SHA-256)
- Service buttons (redirect to http://IP:PORT)
- Multiple RDP buttons (downloads appropriate .rdp file)
- Mobile-responsive design

### Status Monitoring
The portal includes an intelligent heartbeat indicator that shows data freshness:
- **🟢 Healthy:** Updates arriving on time (within 1.5x expected interval)
- **🟡 Warning:** Updates slightly overdue (1.5x - 2x interval)
- **🔴 Stale:** Updates very overdue (2x+ interval)

**Auto-Refresh:** Page automatically reloads when next update is expected, ensuring users always see current data.

**Timezone Support:** All timestamps use UTC internally for accurate calculations regardless of server or user timezone. Display timestamps automatically convert to user's local time.

**Refresh Logic:**
- Calculates time since last publish
- Determines when next update is expected
- Refreshes 5 seconds after expected update
- Minimum refresh interval: 10 seconds

### User Flow
1. Visit portal URL
2. See PC name, external IP, last update, and status
3. Click service or RDP button
4. Enter password
5. Access service or download RDP file

---

## 🔄 Operation

### Startup Sequence
1. Check for existing instance (exit if found)
2. Load PC-specific config or `config.xml`
3. Update external IP
4. Publish portal immediately (all RDP files + HTML)
5. Minimize to system tray
6. Start monitors and timers

### Background Tasks
- **Every 5 seconds:** Internet check, IP updates, dashboard refresh
- **Every 1 hour:** Check if Bing wallpaper needs refresh (updates after 24 hours)
- **Every 5 minutes:** Silent auto-publish (configurable)
- **On demand:** Manual publish with success/error dialog

---

## 🐛 Troubleshooting

**App doesn't appear:** Check system tray for 🏠 icon

**"Already running" message:** App is in tray - double-click icon to show

**Wrong config loaded:** Check dashboard "Config File" - should match PC name (e.g., `SERVER-01.xml`)

**Portal not refreshing:**
- Check browser console for JavaScript errors
- Verify `AutoPublish/FrequencySeconds` is set correctly in config
- Ensure timestamp format is ISO 8601 (YYYY-MM-DDTHH:MM:SSZ)
- Clear browser cache and reload

**Heartbeat shows "Stale" immediately:**
- Likely timezone issue - fixed in v1.3 with UTC timestamps
- Re-publish portal to get updated HTML with UTC support

**VPC RDP not working:**
- Verify router forwards ports to host PC
- Run `netsh interface portproxy show all` on host
- Check Windows Firewall allows ports
- Ensure VPC has RDP enabled

**FTP fails:** Check error dialog (credentials, path, connection)

**Activity light gray:** No internet - check network

---

## 💡 Tips

### Multi-PC Strategy
- Use the config editor and check "Save as PC-specific config"
- Or manually create `[PCNAME].xml` for each computer (see `TEMPLATE-PCNAME.xml` for example)
- Use unique filenames: `server01.html`, `desktop.html`
- PC name appears on portal automatically

### VPC Best Practices
- Sequential ports (3389, 3390, 3391...)
- Descriptive names ("Host PC", "VPC - WinServer")
- Disable unused targets

### Security
- Strong passwords for portal and Windows accounts
- Consider non-standard RDP ports (security through obscurity)
- Use FTPS if available

### Performance
- 300s publish frequency recommended
- Remote path must end with `/`
- FTP timeout: 30 seconds per file
- Auto-refresh synchronized with publish schedule
- Heartbeat checks every 10 seconds
- Page refreshes automatically when new data expected

---

## 📊 System Requirements

- Windows 10/11
- .NET 8.0 Runtime
- Internet connection
- FTP hosting
- Router with port forwarding capability

---

## 📦 Included Files

- `HomeNet Lab.exe` - Main application
- `config.xml` - Default configuration (edit this or use GUI editor)
- `TEMPLATE-PCNAME.xml` - Example PC-specific config (reference only)
- `SplashLab.png`, `HomeNetLab.ico`, `HomeNetLab.png` - Branding assets

**Note:** `TEMPLATE-PCNAME.xml` is just an example. The app only uses `config.xml` or auto-detects `[YOURPCNAME].xml` if it exists.

---

## 🎉 Features at a Glance

✅ Auto-loads PC-specific configs  
✅ **Full GUI config editor** - Services & RDP targets  
✅ **Borderless modern dialogs** with keyboard shortcuts  
✅ Silent tray startup  
✅ Instant publish on launch  
✅ Multi-RDP targets (Host + VPCs)  
✅ Port forwarding wizard (clipboard copy)  
✅ Single instance enforcement  
✅ Live monitoring (5-second intervals)  
✅ **Heartbeat indicator** - Real-time portal status monitoring  
✅ **Auto-refresh portal** - Synchronized with publish interval  
✅ Auto-publishing (configurable)  
✅ **Daily Bing wallpaper refresh**  
✅ SHA-256 password protection  
✅ Bing wallpaper backgrounds
✅ **Dark theme UI** with rounded buttons  
✅ Mobile-friendly portals  
✅ **Separate port displays** for services & RDP  

---

**Version:** 1.3  
**Platform:** Windows 10/11  
**Framework:** .NET 8.0  
**GitHub:** https://github.com/brucemurphy/HomeNet-Lab

Built with WPF, VB.NET, and ❤️
