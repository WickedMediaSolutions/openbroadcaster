Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue
Remove-Item -Path 'e:\openbroadcaster\avalonia-art.log' -ErrorAction SilentlyContinue
Remove-Item -Path 'e:\openbroadcaster\avalonia-debug.log' -ErrorAction SilentlyContinue
Remove-Item -Path 'e:\openbroadcaster\dc-dump.txt' -ErrorAction SilentlyContinue
# Run via cmd.exe so we can redirect stdout/stderr to a file
$cmdArgs = "/c dotnet run --project ""e:\\openbroadcaster\\OpenBroadcaster.Avalonia\\OpenBroadcaster.Avalonia.csproj"" --configuration Debug > e:\\openbroadcaster\\avalonia-run.log 2>&1"
$proc = Start-Process -FilePath cmd.exe -ArgumentList $cmdArgs -PassThru
Start-Sleep -Seconds 12
if ($proc -ne $null) {
    try { $proc | Stop-Process -Force } catch {}
}
Start-Sleep -Seconds 1
$dir = Join-Path $env:LOCALAPPDATA 'OpenBroadcaster\album-art'
if (Test-Path $dir) {
    Get-ChildItem -Path $dir | Select-Object Name,Length | ConvertTo-Csv -NoTypeInformation | Out-File 'e:\openbroadcaster\album_art_list.csv'
} else {
    '' | Out-File 'e:\openbroadcaster\album_art_list.csv'
}
