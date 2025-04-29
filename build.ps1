function Show-Error
{
    param ( [string] $Message )

    Write-Host $Message
    Write-Host "Press any key to continue..."
    [System.Console]::ReadKey($true) | Out-Null
    exit 1
}


# List of projects and their publish profiles
$projects = @(
    @{ Path = ".\InsomniaService"; Profile = "Beta" },
    @{ Path = ".\plugins\DuoStreamIntegration"; Profile = "Beta" }
    @{ Path = ".\plugins\InsomniaServiceSessionBridge\InsomniaServiceBridge"; Profile = "Beta" }
    # Add more entries here as needed
)

# Path to the .csproj file
$frameworkProject = ".\plugins\InsomniaServiceSessionBridge\InsomniaSessionMinion\InsomniaSessionMinion.csproj"

# MSBuild path — you may need to adjust this based on your Visual Studio version
$msbuild = "${env:ProgramFiles}\Microsoft Visual Studio\2022\Community\MSBuild\Current\Bin\MSBuild.exe"

Write-Host "Building .NET Framework 4.8 project..."
#& "$msbuild" $frameworkProject /p:Configuration=Release

$msbuildResult = & "$msbuild" $frameworkProject /p:Configuration=Release /v:minimal 2>&1

if ($LASTEXITCODE -eq 0) {
    #Write-Host "✅ MSBuild succeeded for $frameworkProject"
} else {
    $msbuildResult | Write-Output

    Show-Error -Message "❌ MSBuild failed for $frameworkProject"
}


# Step 1: Publish all projects
foreach ($proj in $projects)
{
    $path = $proj.Path
    $buildProfile = $proj.Profile

    Write-Host "Publishing project '$path' with profile '$buildProfile'..."
    #dotnet publish $path /p:PublishProfile=$buildProfile

    $publishResult = dotnet publish $path /p:PublishProfile=$profile /v:minimal 2>&1

    if ($LASTEXITCODE -eq 0) {
        #Write-Host "✅ Publish succeeded for $path"
    } else {
        $publishResult | Write-Output

        Show-Error -Message "❌ Publish failed for $path"
    }
}

Set-Location ./installer

# Step 2: Run Inno Setup
. "./build.ps1"

#Start-Sleep -Seconds 1;