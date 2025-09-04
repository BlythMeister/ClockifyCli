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

# Process content for NuGet (convert to clean text format)
$nugetLines = @()

foreach ($line in $contentLines) {
    $processedLine = $line.Trim()
    
    if ($processedLine -match '^### (.+)$') {
        # Add header without HTML
        $nugetLines += $matches[1]
        $nugetLines += ""  # Add blank line after header
    }
    elseif ($processedLine -match '^- \*\*(.+?)\*\*: (.+)$') {
        # Bold feature with description - convert to plain text
        $nugetLines += "• $($matches[1]): $($matches[2])"
    }
    elseif ($processedLine -match '^- (.+)$') {
        # Regular list item
        $nugetLines += "• $($matches[1])"
    }
    elseif ($processedLine -match '^\s+- (.+)$') {
        # Nested list item (sub-bullet)
        $nugetLines += "  • $($matches[1])"
    }
    elseif ($processedLine -ne '' -and -not $processedLine.StartsWith('##')) {
        # Regular paragraph
        if ($processedLine -notmatch '^\[.*\]') {  # Skip version links
            $nugetLines += $processedLine
        }
    }
}

# Join with actual newlines for CDATA section (plain text for NuGet)
$cleanContent = ($nugetLines -join "`n").Trim()

# Clean up any remaining markdown in text content
$cleanContent = $cleanContent -replace '\*\*(.*?)\*\*', '$1'  # Remove bold
$cleanContent = $cleanContent -replace '\*(.*?)\*', '$1'      # Remove italic
$cleanContent = $cleanContent -replace '`(.*?)`', '$1'        # Remove code

# Escape XML characters for .csproj
$cleanContent = $cleanContent -replace '&', '&amp;'
$cleanContent = $cleanContent -replace '<', '&lt;'
$cleanContent = $cleanContent -replace '>', '&gt;'
$cleanContent = $cleanContent -replace '"', '&quot;'

# No XML escaping needed inside CDATA

Write-Host "SUCCESS: Processed changelog content ($($cleanContent.Length) chars)"

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

# Check if PackageReleaseNotes already exists
$existingNotesPattern = '(?s)<PackageReleaseNotes>.*?</PackageReleaseNotes>'
$nodeExists = $csprojContent -match $existingNotesPattern
if ($nodeExists) {
    # Replace existing PackageReleaseNotes
    Write-Host "DEBUG: Found existing PackageReleaseNotes, replacing content"
    $replacement = "<PackageReleaseNotes><![CDATA[$cleanContent]]></PackageReleaseNotes>"
    $updatedContent = [regex]::Replace($csprojContent, $existingNotesPattern, $replacement)
} else {
    # Insert new PackageReleaseNotes before the closing PropertyGroup tag
    Write-Host "DEBUG: No existing PackageReleaseNotes found, inserting new"
    $propertyGroupPattern = '(\s*<GeneratePackageOnBuild>false</GeneratePackageOnBuild>\s*\n\s*)(</PropertyGroup>)'
    $replacement = '$1' + "<PackageReleaseNotes><![CDATA[$cleanContent]]></PackageReleaseNotes>" + "`n  " + '$2'
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
$verifyPattern = '(?s)<PackageReleaseNotes><!\[CDATA\[(.*?)\]\]></PackageReleaseNotes>'
if ($verifyContent -match $verifyPattern) {
    $injectedLength = $matches[1].Length
    Write-Host "SUCCESS: Verification successful - PackageReleaseNotes present ($injectedLength chars)"
} else {
    Write-Host "ERROR: Verification failed - PackageReleaseNotes not found in .csproj"
    exit 1
}

Write-Host "=== Changelog injection completed successfully ==="
