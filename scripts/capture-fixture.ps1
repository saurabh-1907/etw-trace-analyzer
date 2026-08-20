param(
    [Parameter(Mandatory=$true)] [string] $Output,
    [int] $Seconds = 5
)

$ErrorActionPreference = 'Stop'
$absolute = [IO.Path]::GetFullPath($Output)
New-Item -ItemType Directory -Force -Path ([IO.Path]::GetDirectoryName($absolute)) | Out-Null
$buffer = New-Object byte[] (4MB)
$temp = Join-Path $env:TEMP 'etw-trace-analyzer-fixture.bin'

try {
    xperf -on PROC_THREAD+CSWITCH+DISK_IO+DISK_IO_INIT+FILE_IO+FILE_IO_INIT -f $absolute
    Start-Sleep -Milliseconds 250
    [IO.File]::WriteAllBytes($temp, $buffer)
    $stream = [IO.File]::Open($temp, [IO.FileMode]::Open, [IO.FileAccess]::ReadWrite, [IO.FileShare]::ReadWrite)
    try {
        for ($i = 0; $i -lt 20; $i++) {
            $stream.Position = 0
            [void]$stream.Read($buffer, 0, $buffer.Length)
            $stream.Position = 0
            $stream.Write($buffer, 0, $buffer.Length)
            [Threading.Thread]::SpinWait(200000)
        }
    } finally { $stream.Dispose() }
    Start-Sleep -Seconds $Seconds
} finally {
    xperf -d $absolute
    Remove-Item -Force -ErrorAction SilentlyContinue $temp
}
Write-Host "Recorded ETL fixture: $absolute"
