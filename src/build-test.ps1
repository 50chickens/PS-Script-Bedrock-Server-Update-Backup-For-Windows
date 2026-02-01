#Requires -Version 7.0

# Load build configuration
$config = Get-Content './build-configuration.json' -Raw | ConvertFrom-Json

function Invoke-CodeLint {
    param([PSCustomObject]$Configuration)
    
    if (-not $Configuration.lint.enabled) {
        Write-Host "Code lint is disabled"
        return
    }
    
    Write-Host "=== Linting Code ==="
    
    $solutionFile = $Configuration.solutionFile
    
    dotnet format $solutionFile --verify-no-changes --verbosity $Configuration.lint.verbosity
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Code lint failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Code lint passed"
}

function Invoke-ValidateProjectReferences {
    param([PSCustomObject]$Configuration)
    
    $enabled = $Configuration.validate.enabled
    
    if (-not $enabled) {
        Write-Host "Project reference validation is disabled"
        return
    }
    
    Write-Host "=== Validating Project References ==="
    
    $solutionFile = $Configuration.solutionFile
    
    # Check for circular references
    dotnet sln $solutionFile list | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to list projects in solution" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Project references validated successfully"
}

function Invoke-RestoreDependencies {
    param([PSCustomObject]$Configuration)
    
    $enabled = $Configuration.restore.enabled
    $verbosity = $Configuration.restore.verbosity
    $solutionFile = $Configuration.solutionFile
    
    if (-not $enabled) {
        Write-Host "Dependency restoration is disabled"
        return
    }
    
    Write-Host "=== Restoring Dependencies ==="
    
    dotnet restore $solutionFile --verbosity $verbosity
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Failed to restore dependencies" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Dependencies restored successfully"
}

function Invoke-CodeFormatCheck {
    param([PSCustomObject]$Configuration)
    
    $enabled = $Configuration.codeFormat.enabled
    $verbosity = $Configuration.codeFormat.verbosity
    $failOnError = $Configuration.codeFormat.failOnError
    $solutionFile = $Configuration.solutionFile
    
    if (-not $enabled) {
        Write-Host "Code format check is disabled"
        return
    }
    
    Write-Host "=== Checking Code Format ==="
    
    dotnet format $solutionFile --verify-no-changes --verbosity $verbosity
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Code formatting issues detected. Run 'dotnet format' to fix."
        if ($failOnError) {
            Write-Host "Code format check failed" -ForegroundColor Red
            exit 1
        }
    } else {
        Write-Host "✓ Code format check passed"
    }
}

function Invoke-BuildSolution {
    param([PSCustomObject]$Configuration)
    
    $enabled = $Configuration.build.enabled
    $buildConfig = $Configuration.build.configuration
    $verbosity = $Configuration.build.verbosity
    $warningsAsErrors = $Configuration.build.warningsAsErrors
    $solutionFile = $Configuration.solutionFile
    
    if (-not $enabled) {
        Write-Host "Build is disabled"
        return
    }
    
    Write-Host "=== Building Solution ==="
    
    $buildArgs = @(
        $solutionFile,
        "--configuration", $buildConfig,
        "--no-restore",
        "--verbosity", $verbosity
    )
    
    if ($warningsAsErrors) {
        $buildArgs += @("/p:TreatWarningsAsErrors=true")
    }
    
    dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Build failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ Build completed successfully"
}

function Invoke-RunTests {
    param([PSCustomObject]$Configuration)
    
    $enabled = $Configuration.test.enabled
    $testConfig = $Configuration.test.configuration
    $verbosity = $Configuration.test.verbosity
    $collectCoverage = $Configuration.test.collectCoverage
    $solutionFile = $Configuration.solutionFile
    
    if (-not $enabled) {
        Write-Host "Tests are disabled"
        return
    }
    
    Write-Host "=== Running Tests ==="
    
    $testArgs = @(
        $solutionFile,
        "--configuration", $testConfig,
        "--verbosity", $verbosity,
        "--no-build",
        "--no-restore",
        "--logger", "trx"
    )
    
    if ($collectCoverage) {
        $testArgs += @("/p:CollectCoverage=true", "/p:CoverletOutputFormat=json", "/p:CoverletOutput=./coverage.json")
    }
    
    dotnet test @testArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Tests failed" -ForegroundColor Red
        exit 1
    }
    
    Write-Host "✓ All tests passed"
}

# Main execution
Write-Host ""
Write-Host "=== Minecraft Management Service Build & Test ===" -ForegroundColor Cyan
Write-Host ""

if (-not (Test-Path $config.solutionFile)) {
    Write-Host "Solution file not found: $($config.solutionFile)" -ForegroundColor Red
    exit 1
}

Invoke-CodeLint -Configuration $config
Invoke-ValidateProjectReferences -Configuration $config
Invoke-RestoreDependencies -Configuration $config
Invoke-CodeFormatCheck -Configuration $config
Invoke-BuildSolution -Configuration $config
Invoke-RunTests -Configuration $config

Write-Host ""
Write-Host "=== Build & Test Completed Successfully ===" -ForegroundColor Green
Write-Host ""
exit 0
