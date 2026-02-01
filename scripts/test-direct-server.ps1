$path = Join-Path $env:APPDATA 'OpenBroadcaster'
New-Item -ItemType Directory -Force -Path $path | Out-Null
$json = @"
{
  "Version":"1.1",
  "DirectServer": { "Enabled": true, "Port": 8586, "AllowRemoteConnections": true, "ApiKey": "", "EnableCors": true, "CorsOrigins": "*" }
}
"@
Set-Content -Path (Join-Path $path 'settings.json') -Value $json -Encoding UTF8

Start-Process -FilePath dotnet -ArgumentList 'run','--project','e:\openbroadcaster\OpenBroadcaster.Avalonia\OpenBroadcaster.Avalonia.csproj','--configuration','Debug' -NoNewWindow
Start-Sleep -Seconds 6

Write-Output 'Calling /api/status...'
try { $s = Invoke-RestMethod -Uri 'http://localhost:8586/api/status'; $s | ConvertTo-Json -Depth 5; } catch { Write-Output "Status call failed: $_" }

Write-Output 'Calling /api/library/search...'
try { $search = Invoke-RestMethod -Uri 'http://localhost:8586/api/library/search?q=&page=1&per_page=5'; $search | ConvertTo-Json -Depth 5 } catch { Write-Output "Search failed: $_"; $search = $null }

if ($search -and $search.items -and $search.items.Count -gt 0) {
  $id = $search.items[0].id
  Write-Output "Found id: $id"
  $body = @{ trackId = $id; requesterName = 'Tester'; message = 'Test request' } | ConvertTo-Json
  Write-Output 'Posting request...'
  try { $resp = Invoke-RestMethod -Uri 'http://localhost:8586/api/requests' -Method Post -ContentType 'application/json' -Body $body; $resp | ConvertTo-Json -Depth 5 } catch { Write-Output "Post failed: $_" }
} else { Write-Output 'No tracks found to request.' }
