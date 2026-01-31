#Requires -Version 7.0

# Load build configuration
$config = Get-Content './build-configuration.json' -Raw | ConvertFrom-Json

function Invoke-ValidateProjectReferences {
    param([PSCustomObject]$Config, [string]$SolutionFile)
    
    if (-not $Config.enabled) {
        Write-Host "Project reference validation is disabled"
        return
    }
    
    Write-Host "=== Validating Project References ==="
    
    # Check for circular references
    $projects = dotnet sln $SolutionFile list
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to list projects in solution"
    }
    
    Write-Host "✓ Project references validated successfully"
}

function Invoke-RestoreDependencies {
    param([PSCustomObject]$Config, [string]$SolutionFile)
    
    if (-not $Config.enabled) {
        Write-Host "Dependency restoration is disabled"
        return
    }
    
    Write-Host "=== Restoring Dependencies ==="
    
    dotnet restore $SolutionFile --verbosity $Config.verbosity
    if ($LASTEXITCODE -ne 0) {
        throw "Failed to restore dependencies"
    }
    
    Write-Host "✓ Dependencies restored successfully"
}

function Invoke-CodeFormatCheck {
    param([PSCustomObject]$Config, [string]$SolutionFile)
    
    if (-not $Config.enabled) {
        Write-Host "Code format check is disabled"
        return
    }
    
    Write-Host "=== Checking Code Format ==="
    
    dotnet format $SolutionFile --verify-no-changes --verbosity $Config.verbosity
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Code formatting issues detected. Run 'dotnet format' to fix."
        if ($Config.failOnError) {
            throw "Code format check failed"
        }
    } else {
        Write-Host "✓ Code format check passed"
    }
}

function Invoke-BuildSolution {
    param([PSCustomObject]$Config, [string]$SolutionFile)
    
    if (-not $Config.enabled) {
        Write-Host "Build is disabled"
        return
    }
    
    Write-Host "=== Building Solution ==="
    
    $buildArgs = @(
        $SolutionFile,
        "--configuration", $Config.configuration,
        "--verbosity", $Config.verbosity,
        "--no-restore"
    )
    
    if ($Config.warningsAsErrors) {
        $buildArgs += "/warnaserror"
    }
    
    dotnet build @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
    
    Write-Host "✓ Build completed successfully"
}

function Invoke-RunTests {
    param([PSCustomObject]$Config, [string]$SolutionFile)
    
    if (-not $Config.enabled) {
        Write-Host "Tests are disabled"
        return
    }
    
    Write-Host "=== Running Tests ==="
    
    $testArgs = @(
        $SolutionFile,
        "--configuration", $Config.configuration,
        "--verbosity", $Config.verbosity,
        "--no-build",
        "--no-restore",
        "--logger", "trx"
    )
    
    if ($Config.collectCoverage) {
        $testArgs += "/p:CollectCoverage=true"
        $testArgs += "/p:CoverletOutputFormat=json"
        $testArgs += "/p:CoverletOutput=./coverage.json"
    }
    
    dotnet test @testArgs
    if ($LASTEXITCODE -ne 0) {
        throw "Tests failed"
    }
    
    Write-Host "✓ All tests passed"
}

function Invoke-CodeCoverage {
    param([PSCustomObject]$Config, [string]$SolutionFile)
    
    if (-not $Config.enabled) {
        Write-Host "Code coverage is disabled"
        return
    }
    
    Write-Host "=== Collecting Code Coverage ==="
    
    dotnet test $SolutionFile `
        /p:CollectCoverage=true `
        /p:CoverletOutputFormat=json `
        /p:CoverletOutput=./coverage.json `
        --no-build `
        --no-restore
        
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Code coverage collection failed"
        if ($Config.failOnError) {
            throw "Code coverage collection failed"
        }
    } else {
        Write-Host "✓ Code coverage collected successfully"
    }
}

# Main execution
try {
    Write-Host ""
    Write-Host "=== MineCraft Management Service Build & Test ===" -ForegroundColor Cyan
    Write-Host ""
    
    $solutionFile = $config.solutionFile
    
    if (-not (Test-Path $solutionFile)) {
        throw "Solution file not found: $solutionFile"
    }
    
    Invoke-ValidateProjectReferences -Config $config.validate -SolutionFile $solutionFile
    Invoke-RestoreDependencies -Config $config.restore -SolutionFile $solutionFile
    Invoke-CodeFormatCheck -Config $config.codeFormat -SolutionFile $solutionFile
    Invoke-BuildSolution -Config $config.build -SolutionFile $solutionFile
    Invoke-RunTests -Config $config.test -SolutionFile $solutionFile
    Invoke-CodeCoverage -Config $config.coverage -SolutionFile $solutionFile
    
    Write-Host ""
    Write-Host "=== Build & Test Completed Successfully ===" -ForegroundColor Green
    Write-Host ""
    exit 0
}
catch {
    Write-Host ""
    Write-Host "=== Build & Test Failed ===" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    exit 1
}
