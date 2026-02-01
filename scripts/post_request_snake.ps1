$body = @{ track_id='0e281482-e409-4b82-b0ce-5276b327e602'; requester_name='VoiceTester'; message='Test request from assistant' } | ConvertTo-Json -Depth 5
try {
    $resp = Invoke-RestMethod -Uri 'http://localhost:8586/api/requests' -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 10
    Write-Output '---POST_RESPONSE---'
    $resp | ConvertTo-Json -Depth 5
} catch {
    Write-Output '---POST_FAILED---'
    Write-Output $_.Exception.Message
}
