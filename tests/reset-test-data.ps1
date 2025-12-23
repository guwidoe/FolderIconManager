# Reset Test Data Script
# Resets the manual test folders in tests/TestData to their original state

$testDataPath = Join-Path $PSScriptRoot "TestData"

Write-Host "Resetting test data in: $testDataPath"

# Remove any extracted icons (they might have hidden attributes)
$folders = @("TestFolder1", "TestFolder2", "TestFolder3")
foreach ($folder in $folders) {
    $folderPath = Join-Path $testDataPath $folder
    $iconPath = Join-Path $folderPath "folder.ico"
    $iniPath = Join-Path $folderPath "desktop.ini"
    
    # Remove icon file if exists
    if (Test-Path $iconPath) {
        attrib -h -s $iconPath
        Remove-Item $iconPath -Force
        Write-Host "  Removed: $iconPath"
    }
    
    # Remove desktop.ini if exists
    if (Test-Path $iniPath) {
        attrib -h -s -r $iniPath
        Remove-Item $iniPath -Force
    }
    
    # Remove folder read-only attribute
    if (Test-Path $folderPath) {
        attrib -r $folderPath
    }
}

# Recreate desktop.ini files with proper encoding
Write-Host "Creating desktop.ini files..."

$content1 = "[.ShellClassInfo]`r`nIconResource=C:\Windows\System32\shell32.dll,4`r`n"
$content2 = "[.ShellClassInfo]`r`nIconResource=C:\Windows\System32\imageres.dll,3`r`n"
$content3 = "[.ShellClassInfo]`r`nIconResource=folder.ico,0`r`n"

[System.IO.File]::WriteAllText((Join-Path $testDataPath "TestFolder1\desktop.ini"), $content1, [System.Text.Encoding]::Default)
[System.IO.File]::WriteAllText((Join-Path $testDataPath "TestFolder2\desktop.ini"), $content2, [System.Text.Encoding]::Default)
[System.IO.File]::WriteAllText((Join-Path $testDataPath "TestFolder3\desktop.ini"), $content3, [System.Text.Encoding]::Default)

Write-Host "Test data reset complete!"
Write-Host ""
Write-Host "TestFolder1: External icon (shell32.dll, index 4)"
Write-Host "TestFolder2: External icon (imageres.dll, index 3)"
Write-Host "TestFolder3: Local icon reference (folder.ico - missing file)"

