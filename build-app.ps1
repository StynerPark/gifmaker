$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$project = Join-Path $projectRoot 'gifmaker.csproj'
dotnet build $project -c Release --nologo
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$compiled = Join-Path $projectRoot 'bin\Release\net8.0-windows'
$output = Join-Path $projectRoot 'build'
New-Item -ItemType Directory -Force -Path $output | Out-Null
Copy-Item -LiteralPath (Join-Path $compiled 'ClipForgeWpf.dll') -Destination (Join-Path $output 'gifmaker.dll') -Force

$bytes = [IO.File]::ReadAllBytes((Join-Path $compiled 'ClipForgeWpf.exe'))
$old = [Text.Encoding]::ASCII.GetBytes('ClipForgeWpf.dll')
$new = [Text.Encoding]::ASCII.GetBytes('gifmaker.dll')
$position = -1
for ($i = 0; $i -le $bytes.Length - $old.Length; $i++) {
    $same = $true
    for ($j = 0; $j -lt $old.Length; $j++) {
        if ($bytes[$i + $j] -ne $old[$j]) { $same = $false; break }
    }
    if ($same) {
        if ($position -ge 0) { throw 'Found multiple apphost entries.' }
        $position = $i
    }
}
if ($position -lt 0) { throw 'Apphost entry was not found.' }
for ($j = 0; $j -lt $old.Length; $j++) { $bytes[$position + $j] = 0 }
[Array]::Copy($new, 0, $bytes, $position, $new.Length)
[IO.File]::WriteAllBytes((Join-Path $output 'gifmaker.exe'), $bytes)
Write-Host "Built $output"
