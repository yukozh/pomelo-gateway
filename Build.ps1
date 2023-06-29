$timestamp = 'v' + [System.DateTime]::UtcNow.ToString("yyyyMMddHHmm")
$root = Get-Location
$srcProjectsFolder = Join-Path $root 'src'
Set-Location $srcProjectsFolder
$projects = Get-ChildItem
For ($i = 0; $i -lt $projects.Length; ++$i) {
    Write-Host 'Packaging' $projects[$i].Name
    Set-Location $projects[$i].FullName
    $binPath = Join-Path $projects[$i].FullName 'bin'
    Remove-Item $binPath -Recurse -Force
    dotnet build
    dotnet pack --version-suffix $timestamp -c Release
    If ($LASTEXITCODE -ne 0) {
        Exit $LASTEXITCODE
    }
}

$publishPath = Join-Path $root 'publish'
If (Test-Path $publishPath) {
    Remove-Item -Path $publishPath -Recurse -Force
}

New-Item -Path $publishPath -ItemType Directory
$items = [System.IO.Directory]::GetFiles($root, "*.nupkg", [System.IO.SearchOption]::AllDirectories)
Foreach ($item In $items) {
    Copy-Item -Path $item -Destination $publishPath
    dotnet nuget push $item --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json
}

Set-Location $root