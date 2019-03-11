$msbuildPath = "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe"

Write-Output "Using '$msbuildPath' as the path to MSBuild.exe. Please change this if MSBuild is in a different location on your system.`n"

$rootPath = Resolve-Path "$PSScriptRoot\.."
$binPath = Resolve-Path "$rootPath\bin"
$tempPath = "$binPath\temp"

Write-Output "Using '$tempPath' as temporary directory"

New-Item $tempPath -ItemType Directory -Force | Out-Null



#download doorstop

$doorstopVersion = "2.7.1.0"

Write-Output "Downloading Doorstop $doorstopTag"

$wc = New-Object System.Net.WebClient

$url = "https://github.com/NeighTools/UnityDoorstop/releases/download/v$doorstopVersion/Doorstop_x64_$doorstopVersion.zip"
$output = "$tempPath\doorstop_x64.zip"

$wc.DownloadFile($url, $output)

$url = "https://github.com/NeighTools/UnityDoorstop/releases/download/v$doorstopVersion/Doorstop_x86_$doorstopVersion.zip"
$output = "$tempPath\doorstop_x86.zip"

$wc.DownloadFile($url, $output)



# build assemblies

Write-Output "`nBuilding legacy assemblies..."

New-Item "$tempPath\shared" -ItemType Directory -Force | Out-Null
New-Item "$tempPath\bootstrap" -ItemType Directory -Force | Out-Null
New-Item "$tempPath\legacy" -ItemType Directory -Force | Out-Null
New-Item "$tempPath\v2018" -ItemType Directory -Force | Out-Null

& $msbuildPath /p:Configuration=Legacy /t:Build /p:DebugType=none $rootPath\BepInEx.sln

Copy-Item -Path "$binPath\*" -Destination "$tempPath\shared\" -Include ("*.dll", "*.xml")

Move-Item -Path ("$tempPath\shared\BepInEx.dll", "$tempPath\shared\BepInEx.Preloader.dll") -Destination "$tempPath\legacy" -Force

Move-Item -Path "$tempPath\shared\BepInEx.Bootstrap.dll" -Destination "$tempPath\bootstrap" -Force


Write-Output "`nBuilding v2018 assemblies..."

& $msbuildPath /p:Configuration=v2018 /t:Build /p:DebugType=none $rootPath\BepInEx.sln

Copy-Item -Path ("$binPath\BepInEx.dll", "$binPath\BepInEx.Preloader.dll") -Destination "$tempPath\v2018\"




#package zip

Compress-Archive -Path "$tempPath\*" -DestinationPath "$binPath\InstallPackage.zip" -CompressionLevel Optimal -Force


Remove-Item $tempPath -Recurse