# AppVeyor script to extract changelog content and inject into .csproj
param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [Parameter(Mandatory=$false)]
    [string]$ChangelogPath = "CHANGELOG.md",
    
    [Parameter(Mandatory=$false)]
    [string]$CsprojPath = "src\ClockifyCli\ClockifyCli.csproj"
)

Write-Host "=== Changelog Injection Script ==="
Write-Host "Version: $Version"
Write-Host "Changelog: $ChangelogPath"
Write-Host "Project: $CsprojPath"

# Extract major.minor version for changelog lookup
$majorMinorVersion = ($Version -split '\.')[0..1] -join '.'
Write-Host "Looking for changelog section: $majorMinorVersion"

if (-not (Test-Path $ChangelogPath)) {
    Write-Host "WARNING: CHANGELOG.md not found at $ChangelogPath"
    $env:RELEASE_NOTES = "Release v$Version"
    exit 0
}

# Read changelog content
$content = Get-Content $ChangelogPath -Raw

# Extract the section for the current version (using major.minor)
$pattern = "## \[$majorMinorVersion\].*?(?=\n## |\n$|\Z)"
$match = [regex]::Match($content, $pattern, [System.Text.RegularExpressions.RegexOptions]::Singleline)

Write-Host "DEBUG: Searching for pattern: $pattern"
Write-Host "DEBUG: Match found: $($match.Success)"

if (-not $match.Success) {
    Write-Host "WARNING: No changelog section found for version $majorMinorVersion"
    Write-Host "DEBUG: Available sections in changelog:"
    $availableSections = [regex]::Matches($content, "## \[([^\]]+)\]")
    foreach ($section in $availableSections) {
        Write-Host "  - Found section: $($section.Groups[1].Value)"
    }
    $env:RELEASE_NOTES = "Release v$Version"
    exit 0
}

# Extract and clean up the changelog content
$versionChangelog = $match.Value
Write-Host "SUCCESS: Found changelog section ($($versionChangelog.Length) chars)"

# Clean up the content - remove the version header and date line
$lines = $versionChangelog -split "\n"
$contentLines = $lines | Select-Object -Skip 2 | Where-Object { $_.Trim() -ne "" }  # Skip version header and empty lines

# Remove markdown formatting for NuGet compatibility and join with HTML line break entities
$processedLines = @()
foreach ($line in $contentLines) {
    $processedLine = $line
    $processedLine = $processedLine -replace '\*\*(.*?)\*\*', '$1'  # Remove **bold**
    $processedLine = $processedLine -replace '\*(.*?)\*', '$1'      # Remove *italic*
    $processedLine = $processedLine -replace '`(.*?)`', '$1'        # Remove `code`
    $processedLine = $processedLine -replace '### ', ''             # Remove ### headers
    $processedLine = $processedLine -replace '- ', '• '             # Convert - to bullets
    $processedLines += $processedLine
}

# Join with actual newlines for CDATA section (NuGet formatting)
$cleanContent = ($processedLines -join "`n").Trim()

# No XML escaping needed inside CDATA

Write-Host "SUCCESS: Processed changelog content ($($cleanContent.Length) chars)"

# Set environment variable for GitHub release (original markdown format for GitHub)
$githubContent = ($contentLines -join "`n").Trim()
$env:RELEASE_NOTES = $githubContent
Write-Host "SUCCESS: Set RELEASE_NOTES environment variable"

# Update .csproj file with release notes
if (-not (Test-Path $CsprojPath)) {
    Write-Host "ERROR: .csproj file not found at $CsprojPath"
    exit 1
}

$csprojContent = Get-Content $CsprojPath -Raw

# Insert PackageReleaseNotes before the closing PropertyGroup tag using CDATA
$propertyGroupPattern = '(\s*<GeneratePackageOnBuild>false</GeneratePackageOnBuild>\s*)(</PropertyGroup>)'
$replacement = '$1' + "`n    <PackageReleaseNotes><![CDATA[$cleanContent]]></PackageReleaseNotes>" + "`n  " + '$2'
$updatedContent = [regex]::Replace($csprojContent, $propertyGroupPattern, $replacement)

if ($updatedContent -eq $csprojContent) {
    Write-Host "ERROR: Failed to inject PackageReleaseNotes - pattern not found"
    exit 1
}

Set-Content $CsprojPath -Value $updatedContent -NoNewline
Write-Host "SUCCESS: Updated .csproj with PackageReleaseNotes"

# Verify the injection worked
$verifyContent = Get-Content $CsprojPath -Raw
$verifyPattern = '(?s)<PackageReleaseNotes>(.*?)</PackageReleaseNotes>'
if ($verifyContent -match $verifyPattern) {
    $injectedLength = $matches[1].Length
    Write-Host "SUCCESS: Verification successful - PackageReleaseNotes injected ($injectedLength chars)"
} else {
    Write-Host "ERROR: Verification failed - PackageReleaseNotes not found in .csproj"
    exit 1
}

Write-Host "=== Changelog injection completed successfully ==="
