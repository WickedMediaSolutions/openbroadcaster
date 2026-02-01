Stop-Process -Name dotnet -Force -ErrorAction SilentlyContinue
Remove-Item -Path 'e:\openbroadcaster\avalonia-art.log' -ErrorAction SilentlyContinue
Remove-Item -Path 'e:\openbroadcaster\avalonia-run.log' -ErrorAction SilentlyContinue
$proc = Start-Process -FilePath cmd.exe -ArgumentList '/c dotnet run --project "e:\\openbroadcaster\\OpenBroadcaster.Avalonia\\OpenBroadcaster.Avalonia.csproj" --configuration Debug > e:\\openbroadcaster\\avalonia-run.log 2>&1' -PassThru
Start-Sleep -Seconds 20
if ($proc -ne $null) { try { $proc | Stop-Process -Force } catch {} }
Start-Sleep -Seconds 1
if (Test-Path 'e:\openbroadcaster\avalonia-art.log') { Get-Content 'e:\openbroadcaster\avalonia-art.log' -Tail 200 | Out-File 'e:\openbroadcaster\avalonia-art-tail.txt' } else { Get-Content 'e:\openbroadcaster\avalonia-run.log' -Tail 200 | Out-File 'e:\openbroadcaster\avalonia-art-tail.txt' }
