
# This script is meant to be executed on the host to install and set up 
# Canoe/NoVNC 

# Assumptions: 
# - Running latest Windows 11
# - Host has Internet connection (kind of obvious 'cause it's a server)
# - Installed Programs:
#   - WSL Debian
#   - TightVNC


param (
    [int]$skipto = 0
)
$stage = 0

## SSL Setup (TODO: Finish Later when I have access to an actual signed cert)

## Installations

# Canoe
if ($skipto -le $stage) {
    C:\Windows\Microsoft.NET\Framework\v4.0.30319\InstallUtil.exe $($(Get-Location).Path.ToString() + "\CanoeService\bin\Debug\CanoeService.exe")
    if (-not $?) {
        Write-Output "There was a problem installing Canoe Service (try running this script as administrator?)"
        exit 1
    }
} else {
    Write-Output "Skipping Canoe Service Installation..."
}
$stage++

# noVNC dependencies
if ($skipto -le $stage) {
    wsl -d Debian -u root apt update "&&" apt install -y python3-numpy git
    if (-not $?) {
        Write-Output "There was a problem installing dependencies (is WSL working correctly?)"
        exit 1
    }
} else {
    Write-Output "Skipping noVNC dependency installation..."
}
$stage++

# NoVNC
$NOVNC_PATH = "~/noVNC"
if ($skipto -le $stage) {
    wsl -d Debian install -d $NOVNC_PATH "&&" git -C $NOVNC_PATH clone https://github.com/w6xm/noVNC.git
    if (-not $?) {
        Write-Output "There was a problem downloading NoVNC"
        exit 1
    }
} else {
    Write-Output "Skipping noVNC installation..."
}
$stage++

## Configure port forwarding between WSL and Windows

## Create task
# Remember to update the skipto value if you add stages before this!!!
if ($skipto -le $stage) {
    $trigger = New-JobTrigger -AtStartup -RandomDelay 00:00:15
    Register-ScheduledJob -Trigger $trigger -FilePath $($(Get-Location).Path.ToString() + "\setup_canoe_host.ps1") -Name WSL_Canoe_PortProxy -ArgumentList 4 
    if (-not $?) {
        Write-Output "There was a problem registering the system task"
        exit 1
    }
} else {
    Write-Output "Skipping task creation..."
}
$stage++

# This method for getting an IP address works on Debian and Ubuntu; 
# I don't know exactly how portable it is, though. Also, it only gets the 
# first address even if mutiple are available.
$WSL_IP = wsl -d Debian bash -c "hostname -I | awk '{print $1}'"
$HOST_IP = $($(Get-NetIPAddress -InterfaceAlias Ethernet -AddressFamily IPv4).CimInstanceProperties | Where-Object -Property Name -eq IPAddress).Value.ToString()
# This *might* be necessary for WSL2; I wasn't able to check because my Windows VM 
# could only use WSL1
#if ($skipto -le $stage) {
#   New-NetFirewallRule -DisplayName "WSL" -Direction Inbound -InterfaceAlias "vEthernet (WSL)"  -Action Allow
#   if (-not $?) {
#      Write-Output "There was a problem opening the firewall between WSL and Windows"
#      exit 1
#   }
#} else {
#   Write-Output "Skipping WSL-Windows firewall configuration"
#}
#$stage++

if ($skipto -le $stage) {
    netsh interface portproxy add v4tov4 listenport=6080 listenaddress=0.0.0.0 connectport=6080 connectaddress=$WSL_IP
    if (-not $?) {
        Write-Output "There was a problem configuring port forwarding between WSL and Windows"
        exit 1
    }
} else {
    Write-Output "Skipping port forwarding configuration..."
}
$stage++

## Start noVNC (tightvnc should start automatically)
if ($skipto -le $stage) {
    wsl -d Debian nohup $NOVNC_PATH/noVNC/utils/novnc_proxy --vnc $HOST_IP`:5900 --listen 0.0.0.0:6080 `&
    if (-not $?) {
        # This doesn't actually work since nohup means even it
        # it fails the signal won't fall through. 
        #
        # Something to fix later, I guess.
        Write-Output "There was a problem starting NoVNC"
        exit 1
    }
} else {
    Write-Output "Skipping starting noVNC..."
}
$stage++

# For security reasons/to avoid surprises, this script doesn't
# automatically configure the firewall
Write-Output "`nInstallation complete!"
Write-Output "Remember to configure firewall to open ports 6080 and 8443"
