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
Add RDP targets (see `SERVER-01.xml` example):
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
- **Services** - Enabled services and ports
- **RDP Targets** - Configured RDP targets and ports
- **Auto-Publish** - Status and frequency
- **Activity Light** - Connection status
- **IP Display** - External IP, PC name, local IP

### Menu Bar
- **File → Publish Web Portal** - Manual upload with result dialog
- **File → Setup Port Forwarding...** - VPC setup wizard
- **File → Exit** - Close application

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
- PC identification banner (when using custom configs)
- External IP and last update timestamp
- Password-protected access (SHA-256)
- Service buttons (redirect to http://IP:PORT)
- Multiple RDP buttons (downloads appropriate .rdp file)
- Mobile-responsive design

### User Flow
1. Visit portal URL
2. See PC name, external IP, last update
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
- **Every 5 minutes:** Silent auto-publish (configurable)
- **On demand:** Manual publish with success/error dialog

---

## 🐛 Troubleshooting

**App doesn't appear:** Check system tray for 🏠 icon

**"Already running" message:** App is in tray - double-click icon to show

**Wrong config loaded:** Check dashboard "Config File" - should match PC name (e.g., `SERVER-01.xml`)

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
- Create `[PCNAME].xml` for each computer
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
- `config.xml` - Default configuration template
- `SplashLab.png`, `HomeNetLab.ico`, `HomeNetLab.png` - Branding assets

---

## 🎉 Features at a Glance

✅ Auto-loads PC-specific configs  
✅ Silent tray startup  
✅ Instant publish on launch  
✅ Multi-RDP targets (Host + VPCs)  
✅ Port forwarding wizard (clipboard copy)  
✅ Single instance enforcement  
✅ Live monitoring (5-second intervals)  
✅ Auto-publishing (configurable)  
✅ SHA-256 password protection  
✅ Bing wallpaper backgrounds  
✅ Dark theme UI  
✅ Mobile-friendly portals  

---

**Version:** 1.1  
**Platform:** Windows 10/11  
**Framework:** .NET 8.0  
**GitHub:** https://github.com/brucemurphy/HomeNet-Lab

Built with WPF, VB.NET, and ❤️
