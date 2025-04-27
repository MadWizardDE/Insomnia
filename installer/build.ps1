# Path to Inno Setup script
$setupScript = ".\setup.iss"

# Inno Setup compiler path (assumes it's in PATH, otherwise set full path)
$compiler = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"

$arguments = "/DDisableBridge"

# Run Inno Setup
Write-Host "Building installer from '$setupScript'..."

$isccResult = & $compiler $arguments $setupScript 2>&1

if ($LASTEXITCODE -eq 0) {
    #Write-Host "✅ Inno Setup compilation succeeded."
} else {
    $isccResult | Write-Output

    Show-Error -Message "❌ Inno Setup compilation failed!"
}
