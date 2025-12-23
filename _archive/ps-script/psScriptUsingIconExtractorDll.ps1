param(
    [string]$iconReference,
    [string]$iconExtractorDllPath = "C:\Users\43650\Desktop\IconExtractor.dll", # Ensure this path is correct
    [string]$winCopiesDllPath = "C:\Users\43650\Desktop\WinCopies.WindowsAPICodePack.Win32Native.dll", # Ensure this path is correct
    [string]$systemDrawingCommonDllPath = "C:\Users\43650\Desktop\System.Drawing.Common.dll" # Ensure this path is correct
)

try {
    # Load System.Drawing.Common assembly first
    $systemDrawingCommonAssembly = [Reflection.Assembly]::LoadFrom($systemDrawingCommonDllPath)
    Write-Host "System.Drawing.Common Assembly loaded: $($systemDrawingCommonAssembly.FullName)"
} catch {
    Write-Error "Failed to load System.Drawing.Common assembly: $_"
    return
}

try {
    # Load WinCopies.WindowsAPICodePack.Win32Native assembly
    $winCopiesAssembly = [Reflection.Assembly]::LoadFrom($winCopiesDllPath)
    Write-Host "WinCopies Assembly loaded: $($winCopiesAssembly.FullName)"
} catch {
    Write-Error "Failed to load WinCopies.WindowsAPICodePack.Win32Native assembly: $_"
    return
}

try {
    # Load IconExtractor assembly
    $iconExtractorAssembly = [Reflection.Assembly]::LoadFrom($iconExtractorDllPath)
    Write-Host "IconExtractor Assembly loaded: $($iconExtractorAssembly.FullName)"
} catch {
    Write-Error "Failed to load IconExtractor assembly: $_"
    return
}

# Load System.Windows.Forms assembly for access to System.Drawing namespace
Add-Type -AssemblyName System.Windows.Forms

function Extract-Icon {
    param(
        [string]$path,
        [int]$index
    )

    try {
        # Use the full name of the IconExtractor type
        $iconExtractorType = $iconExtractorAssembly.GetType("TsudaKageyu.IconExtractor")
        $iconExtractor = [Activator]::CreateInstance($iconExtractorType, $path)
        $icon = $iconExtractor.GetIcon($index)
        if ($null -eq $icon) {
            Write-Error "No icons were extracted. Please ensure the path and index are correct."
            return
        }

        $iconPath = "$PWD\icon_$index.ico"
        $fileStream = New-Object System.IO.FileStream $iconPath, 'Create'
        $icon.Save($fileStream)
        $fileStream.Close()
        Write-Output "Icon extracted to: $iconPath"
    } catch {
        Write-Error "Error extracting icon: $_"
    }
}

# Parse the icon reference
$parts = $iconReference.Split(',')
if($parts.Length -ne 2) {
    Write-Error "Invalid icon reference format. Please use format: path,index"
    return
}

$path = $parts[0]
$index = $parts[1] -as [int]

if($path -and ($index -ne $null)) {
    Extract-Icon -path $path -index $index
} else {
    Write-Error "Invalid path or index value in icon reference"
}
