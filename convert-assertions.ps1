#!/usr/bin/env pwsh

<#
.SYNOPSIS
Convert NUnit Framework assertions to FluentAssertions across UnitTests project.
.DESCRIPTION
Performs systematic regex-based conversion of NUnit assertions to FluentAssertions,
preserving NUnit.Framework imports needed for test attributes.
#>

param(
    [string]$TargetDirectory = "LgymApi.UnitTests",
    [switch]$DryRun = $false
)

$ConversionPatterns = @(
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.EqualTo\(\s*(.+?)\s*\)\s*\)'; Replacement = '$1.Should().Be($2)' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.Not\.EqualTo\(\s*(.+?)\s*\)\s*\)'; Replacement = '$1.Should().NotBe($2)' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.Null\s*\)'; Replacement = '$1.Should().BeNull()' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.Not\.Null\s*\)'; Replacement = '$1.Should().NotBeNull()' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.True\s*\)'; Replacement = '$1.Should().BeTrue()' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.False\s*\)'; Replacement = '$1.Should().BeFalse()' }
    @{ Pattern = 'Assert\.IsTrue\(\s*(\w+)\s*\)'; Replacement = '$1.Should().BeTrue()' }
    @{ Pattern = 'Assert\.IsFalse\(\s*(\w+)\s*\)'; Replacement = '$1.Should().BeFalse()' }
    @{ Pattern = 'Assert\.AreEqual\(\s*(.+?)\s*,\s*(\w+)\s*\)'; Replacement = '$2.Should().Be($1)' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.SameAs\(\s*(.+?)\s*\)\s*\)'; Replacement = '$1.Should().BeSameAs($2)' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.Not\.SameAs\(\s*(.+?)\s*\)\s*\)'; Replacement = '$1.Should().NotBeSameAs($2)' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Has\.Count\.EqualTo\(\s*(\d+)\s*\)\s*\)'; Replacement = '$1.Should().HaveCount($2)' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.Empty\s*\)'; Replacement = '$1.Should().BeEmpty()' }
    @{ Pattern = 'Assert\.That\(\s*(\w+)\s*,\s*Is\.Not\.Empty\s*\)'; Replacement = '$1.Should().NotBeEmpty()' }
)

function ConvertFile {
    param([string]$FilePath)
    
    $content = Get-Content -Path $FilePath -Raw -Encoding UTF8
    $originalContent = $content
    
    # Add FluentAssertions using if not present and file has assertions
    if ($content -match 'Assert\.' -and $content -notmatch 'using FluentAssertions') {
        $content = $content -replace '(using [^\n]+;\n)', "using FluentAssertions;`n`1"
    }
    
    # Apply conversion patterns
    foreach ($conversion in $ConversionPatterns) {
        $content = $content -replace $conversion.Pattern, $conversion.Replacement
    }
    
    # Check if file changed
    if ($content -ne $originalContent) {
        if ($DryRun) {
            Write-Host "Would update: $FilePath"
        } else {
            Set-Content -Path $FilePath -Value $content -Encoding UTF8 -NoNewline
            Write-Host "Updated: $FilePath"
        }
        return $true
    }
    return $false
}

# Main execution
$testFiles = @(Get-ChildItem -Path $TargetDirectory -Include "*.cs" -Recurse)
Write-Host "Found $($testFiles.Count) test files"

$updated = 0
foreach ($file in $testFiles) {
    if (ConvertFile -FilePath $file.FullName) {
        $updated++
    }
}

Write-Host "Conversion complete: $updated files updated"
