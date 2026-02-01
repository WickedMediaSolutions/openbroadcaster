$body = @{ trackId='0e281482-e409-4b82-b0ce-5276b327e602'; requesterName='VoiceTester'; message='Test request from assistant' } | ConvertTo-Json
try {
    Invoke-RestMethod -Uri 'http://localhost:8586/api/requests' -Method Post -Body $body -ContentType 'application/json' -TimeoutSec 10
    Write-Output '---POST_OK---'
} catch {
    Write-Output '---POST_FAILED---'
    Write-Output $_.Exception.Message
}
