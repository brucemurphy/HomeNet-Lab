# 🏠 HomeNet Lab

**Professional network management and secure remote access portal**

## 🎯 What It Does

HomeNet Lab is a Windows application that automatically publishes a password-protected web portal for secure remote access to your home network services and Remote Desktop.

**Key Features:**
- 🌐 **Dynamic Service URLs** - Automatically uses your current external IP address
- 🔄 **Auto-Publishing** - Keeps web portal updated every 5 minutes
- 🔒 **Password Protected** - SHA-256 encrypted access control
- 📌 **System Tray** - Runs silently in background
- 🟢 **Smart Indicators** - Visual connection and upload status
- 📊 **Live Dashboard** - Real-time configuration display
- 🎨 **Beautiful UI** - Daily Bing wallpaper backgrounds

## 🚀 Quick Start

### 1. Configuration
Edit `config.xml` in the application folder:

```xml
<Configuration>
  <FTP>
    <Server>ftp://your-ftp-server.com</Server>
    <Username>your-ftp-username</Username>
    <Password>your-ftp-password</Password>
    <RemotePath>/public_html/</RemotePath>
    <HtmlFileName>index.html</HtmlFileName>
    <RdpFileName>HomeNetwork.rdp</RdpFileName>
  </FTP>

  <Security>
    <AccessPassword>YourSecurePassword123</AccessPassword>
  </Security>

  <Services>
    <Service>
      <Name>Emby Server</Name>
      <Port>8096</Port>
      <Enabled>true</Enabled>
    </Service>
    <!-- Add more services as needed -->
  </Services>

  <AutoPublish>
    <Enabled>true</Enabled>
    <FrequencySeconds>300</FrequencySeconds>
  </AutoPublish>
</Configuration>
```

### 2. Run Application
- Launch `HomeNet Lab.exe`
- Application loads configuration
- Dashboard displays current settings
- Auto-publish starts automatically

### 3. Manual Publish (First Time)
- Click **File → Publish Web Portal**
- Watch activity light blink during upload
- Success dialog shows upload location

### 4. Access Your Portal
Visit your web portal URL (e.g., `http://yoursite.com/index.html`)

## 🎨 User Interface

### Main Window
```
┌─────────────────────────────────────────┐
│ File                              [□][X]│
├─────────────────────────────────────────┤
│                                         │
│        ┌──────────────────┐            │
│        │ ⚙️ Configuration │            │
│        ├──────────────────┤            │
│        │ FTP Server       │            │
│        │ Username         │            │
│        │ Remote Path      │            │
│        │ Services         │            │
│        │ Ports            │            │
│        │ Auto-Publish     │            │
│        └──────────────────┘            │
│                                         │
├─────────────────────────────────────────┤
│ Bing Image Info        105.242.144.150 │
│ © Copyright            PC-NAME|192... ●│
└─────────────────────────────────────────┘
```

### System Tray (When Minimized)
Right-click system tray icon:
- **Publish Web Portal** - Upload files now
- **Show Window** - Restore window
- **Exit** - Close application

### Activity Light States
- 🟢 **Solid Green** - Connected to internet (idle)
- 🟢 **Blinking Green** - FTP upload in progress
- ⚫ **Gray** - No internet connection

## 🌐 Web Portal Features

### Password-Protected Access
- SHA-256 encrypted password
- Client-side authentication
- No plain-text credentials

### Dynamic Service Buttons
Each enabled service creates a button:
- 🎬 **First Service** (e.g., Emby Server)
- 🔌 **Additional Services** (Plex, Home Assistant, etc.)
- 🖥️ **Remote Desktop** - Downloads RDP connection file

### Service URLs
Automatically built using current external IP:
- `http://[YourCurrentIP]:[Port]`
- Always up-to-date
- No broken links

## ⚙️ Configuration Guide

### FTP Settings
- **Server** - Your FTP server address (with or without `ftp://`)
- **Username** - FTP login username
- **Password** - FTP login password
- **RemotePath** - Upload directory (must end with `/`)
- **HtmlFileName** - Web portal filename (default: `index.html`)
- **RdpFileName** - RDP file name (default: `HomeNetwork.rdp`)

### Security Settings
- **AccessPassword** - Password for web portal access (will be SHA-256 hashed)

### Service Configuration
Add multiple services:
```xml
<Service>
  <Name>Service Name</Name>
  <Port>8096</Port>
  <Enabled>true</Enabled>
</Service>
```

**Common Services:**
- Emby Server: Port 8096
- Plex Server: Port 32400
- Home Assistant: Port 8123
- Jellyfin: Port 8096
- Nextcloud: Port 443

### Auto-Publish Settings
- **Enabled** - `true` or `false`
- **FrequencySeconds** - Update interval (minimum: 60, recommended: 300)

## 🔄 How It Works

### Automatic Updates (Every 5 Seconds)
1. Checks internet connection
2. Updates external IP address
3. Updates local IP address
4. Refreshes all displays

### Auto-Publish (Every 5 Minutes)
1. Checks internet connection
2. Updates external IP
3. Generates RDP file with current IP
4. Uploads RDP file to FTP server
5. Generates HTML portal with current IP
6. Uploads HTML portal to FTP server
7. **Runs silently** (no popups)

### Manual Publish
1. User clicks **File → Publish Web Portal**
2. Activity light blinks
3. Files upload to FTP
4. **Success/error dialog appears**

## 🎯 Publishing Status Display

During uploads, the footer shows real-time progress:
- "Preparing..." / "Reading configuration"
- "Publishing..." / "Updating external IP"
- "Publishing..." / "Uploading RDP file"
- "Publishing..." / "Uploading HTML portal"
- "Complete!" / "Published to your-server"
- Returns to IP display after 2 seconds

## 📁 Files Generated

### On FTP Server
- **index.html** (or custom name) - Web portal interface
- **HomeNetwork.rdp** (or custom name) - Remote Desktop configuration

### RDP File Contents
- Configured for your external IP address
- Optimized settings for remote access
- Auto-reconnect enabled
- Security negotiation enabled

## 🔒 Security Features

- **SHA-256 Password Hashing** - No plain-text passwords
- **Client-Side Encryption** - Browser handles authentication
- **Configurable Access** - Single password protects all services
- **No Credential Storage** - Passwords hashed, never stored as plain text

## 🐛 Troubleshooting

### Activity Light is Gray
**Cause:** No internet connection  
**Fix:** Check your network connection

### Auto-Publish Not Working
**Check:**
- `<Enabled>true</Enabled>` in config.xml
- Internet connection active (green light)
- FTP credentials correct

### FTP Upload Fails
Application shows detailed error messages:
- **Connection Failure** - Check server address and firewall
- **Authentication Failure** - Verify username and password
- **Path Error** - Confirm remote path exists with write permissions
- **Timeout** - Server may be offline or slow

### RDP File Empty or Not Downloading
**Fixes:**
- Wait 5 minutes for auto-publish to upload
- Manually publish: File → Publish Web Portal
- Clear browser cache (Ctrl+F5)
- Check FTP server to verify file exists

### Can't Minimize to Tray
**Fix:** Restart application fresh (not during debugging)

## 💡 Tips & Best Practices

### Auto-Publish Frequency
- **60s** - Very frequent, high bandwidth
- **300s (5 min)** - Recommended for typical use
- **600s (10 min)** - Low bandwidth environments

### Remote Path Structure
Always end with `/`:
- ✅ `/public_html/`
- ❌ `/public_html`

### Service Configuration
- Use descriptive names: "Emby Server", "Plex Media"
- Keep ports standard for each service
- Set `Enabled` to `false` to hide services

### Multiple Computers
Use different filenames for each computer:
```xml
<!-- Computer 1 -->
<HtmlFileName>desktop.html</HtmlFileName>
<RdpFileName>desktop.rdp</RdpFileName>

<!-- Computer 2 -->
<HtmlFileName>laptop.html</HtmlFileName>
<RdpFileName>laptop.rdp</RdpFileName>
```

## 📊 System Requirements

- **OS:** Windows 10/11
- **.NET:** .NET 8.0 Runtime
- **Internet:** Required for IP updates and publishing
- **FTP Access:** Web hosting with FTP upload capability

## 🔧 Advanced Features

### System Tray Operation
- Minimizes to system tray instead of taskbar
- Background operation with no window
- Context menu for quick actions
- Balloon notifications

### Real-Time Monitoring
- Connection status every 5 seconds
- IP address updates automatically
- Configuration display refreshes
- Activity indicator shows status

### Error Reporting
- Detailed FTP error messages
- Specific troubleshooting steps
- Configuration validation
- Network status information

## 📱 Web Portal User Experience

1. **Visit Portal** - User navigates to your URL
2. **See Information** - External IP and last update time displayed
3. **Click Service** - Select Emby, Plex, RDP, etc.
4. **Enter Password** - SHA-256 authentication
5. **Access Service** - Redirects to service or downloads RDP file

### Mobile Friendly
- Responsive design
- Touch-friendly buttons
- Works on phones and tablets
- Gradient background

## 🎊 Key Benefits

✅ **Always Current** - IPs update automatically  
✅ **Secure Access** - Password-protected portal  
✅ **Silent Operation** - Runs in background  
✅ **Multiple Services** - Add unlimited services  
✅ **Error Reporting** - Know exactly what's wrong  
✅ **Professional UI** - Beautiful dark theme  
✅ **Minimal Setup** - Configure once, runs forever  

## 🔄 Update Process

### What Gets Updated
- External IP address (from api.ipify.org)
- RDP file with current IP
- HTML portal with current IP
- Service URLs with current IP

### When Updates Happen
- Every 5 minutes (default)
- On manual publish
- On application startup
- When internet reconnects

## 📝 Example Configurations

### Single Service (Emby)
```xml
<Service>
  <Name>Emby Server</Name>
  <Port>8096</Port>
  <Enabled>true</Enabled>
</Service>
```

### Multiple Services
```xml
<Service>
  <Name>Emby Server</Name>
  <Port>8096</Port>
  <Enabled>true</Enabled>
</Service>
<Service>
  <Name>Plex Server</Name>
  <Port>32400</Port>
  <Enabled>true</Enabled>
</Service>
<Service>
  <Name>Home Assistant</Name>
  <Port>8123</Port>
  <Enabled>true</Enabled>
</Service>
```

### Disable Auto-Publish
```xml
<AutoPublish>
  <Enabled>false</Enabled>
  <FrequencySeconds>300</FrequencySeconds>
</AutoPublish>
```

## 🎨 Customization

### Filenames
Change uploaded filenames:
```xml
<HtmlFileName>portal.html</HtmlFileName>
<RdpFileName>mypc.rdp</RdpFileName>
```

### Service Names
Use any descriptive name:
```xml
<Name>My Media Server</Name>
<Name>Smart Home Hub</Name>
<Name>File Storage</Name>
```

### Upload Frequency
Adjust timing (in seconds):
```xml
<FrequencySeconds>600</FrequencySeconds>  <!-- 10 minutes -->
```

## 🚨 Important Notes

- **Minimum Frequency:** 60 seconds (enforced)
- **Timeout:** 30 seconds per upload
- **Password Security:** Use strong passwords
- **FTP Security:** Ensure FTPS if available
- **Port Forwarding:** Required for remote access to services

## 📞 Support

### Common Issues

**Issue:** "Cannot connect to FTP server"  
**Fix:** Check server address, firewall, and internet

**Issue:** "Authentication failed"  
**Fix:** Verify username and password in config.xml

**Issue:** "Cannot access remote path"  
**Fix:** Ensure path exists and has write permissions

**Issue:** RDP file has no content  
**Fix:** Wait for auto-publish cycle, or manually publish

## 🏗️ Technical Details

### Built With
- WPF (Windows Presentation Foundation)
- .NET 8.0
- Visual Basic .NET

### Network Operations
- HTTP client for IP detection
- FTP client for file uploads
- DNS resolution for local IP
- Bing API for wallpaper images

### Background Processing
- 5-second connection monitor
- 300-second auto-publish timer
- Asynchronous file uploads
- Non-blocking UI updates

## 📦 What You Get

### Application Files
- `HomeNet Lab.exe` - Main application
- `config.xml` - Configuration file
- `HomeNetLab.ico` - Application icon
- `HomeNetLab.png` - Window icon

### Generated Files (On FTP)
- HTML portal (customizable name)
- RDP connection file (customizable name)

## 🎉 Summary

HomeNet Lab provides a complete solution for:
- Publishing password-protected web portals
- Automatic IP address updates
- Secure remote desktop access
- Multiple service management
- Professional network monitoring

**Configure once, runs forever!**

Simply edit `config.xml`, run the application, and enjoy secure remote access to your home network from anywhere in the world.

---

**Version:** 1.0  
**Platform:** Windows 10/11  
**Framework:** .NET 8.0
