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

# Process content for NuGet (simple link to GitHub release)
$nugetContent = "See full release notes: https://github.com/BlythMeister/ClockifyCli/releases/tag/v$Version"

Write-Host "SUCCESS: Generated simple GitHub release link for NuGet (v$Version)"

# Set environment variable for GitHub release (use literal \n for AppVeyor compatibility)
# AppVeyor can handle literal \n strings but not actual newlines
$githubContent = ($contentLines -join '\n').Trim()
$env:RELEASE_NOTES = $githubContent

Write-Host "SUCCESS: Set RELEASE_NOTES environment variable for GitHub (literal newlines)"
Write-Host "DEBUG: GitHub content length: $($githubContent.Length) chars"
Write-Host "DEBUG: First 100 chars: $($githubContent.Substring(0, [Math]::Min(100, $githubContent.Length)))..."

# Update .csproj file with release notes
if (-not (Test-Path $CsprojPath)) {
    Write-Host "ERROR: .csproj file not found at $CsprojPath"
    exit 1
}

$csprojContent = Get-Content $CsprojPath -Raw

# Check if PackageReleaseNotes already exists (handle both CDATA and simple formats)
$existingNotesPattern = '(?s)<PackageReleaseNotes>.*?</PackageReleaseNotes>'
$nodeExists = $csprojContent -match $existingNotesPattern
if ($nodeExists) {
    # Replace existing PackageReleaseNotes
    Write-Host "DEBUG: Found existing PackageReleaseNotes, replacing with GitHub link"
    $replacement = "<PackageReleaseNotes>$nugetContent</PackageReleaseNotes>"
    $updatedContent = [regex]::Replace($csprojContent, $existingNotesPattern, $replacement)
} else {
    # Insert new PackageReleaseNotes before the closing PropertyGroup tag
    Write-Host "DEBUG: No existing PackageReleaseNotes found, inserting GitHub link"
    $propertyGroupPattern = '(\s*<GeneratePackageOnBuild>false</GeneratePackageOnBuild>\s*\n\s*)(</PropertyGroup>)'
    $replacement = '$1' + "<PackageReleaseNotes>$nugetContent</PackageReleaseNotes>" + "`n  " + '$2'
    $updatedContent = [regex]::Replace($csprojContent, $propertyGroupPattern, $replacement)
}

# Check if changes were made or if content was already correct
if ($updatedContent -eq $csprojContent) {
    if ($nodeExists) {
        Write-Host "SUCCESS: PackageReleaseNotes content was already up to date"
    } else {
        Write-Host "ERROR: Failed to inject PackageReleaseNotes - pattern not found"
        exit 1
    }
} else {
    Set-Content $CsprojPath -Value $updatedContent -NoNewline
    Write-Host "SUCCESS: Updated .csproj with PackageReleaseNotes"
}

# Always verify the content is present (regardless of whether we just wrote it)
$verifyContent = Get-Content $CsprojPath -Raw
$verifyPattern = '(?s)<PackageReleaseNotes>(.*?)</PackageReleaseNotes>'
if ($verifyContent -match $verifyPattern) {
    $injectedLength = $matches[1].Length
    Write-Host "SUCCESS: Verification successful - PackageReleaseNotes present ($injectedLength chars)"
    Write-Host "DEBUG: Content: $($matches[1])"
} else {
    Write-Host "ERROR: Verification failed - PackageReleaseNotes not found in .csproj"
    exit 1
}

Write-Host "=== Changelog injection completed successfully ==="
