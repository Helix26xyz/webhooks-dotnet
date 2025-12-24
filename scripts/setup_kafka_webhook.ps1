

$body = [PSCustomObject]@{

  "name" = "k3"
  "slug" = "k3"
  "url" = ""
  "owner" = "k3"
  "project" = "k3"
  "status" = '1'
  "backendType" = '2'
  "backendConfig" = (@{BootstrapServers="10.10.100.93:9092"; Topic="k3"} | ConvertTo-Json)
  "deliveryMode" = '1'
}| ConvertTo-Json;
 try { 
    $response = Invoke-RestMethod -Uri "https://localhost:7579/api/Webhooks" -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck; Write-Host "✅ Webhook triggered!"; Write-Host "   Event ID: $($response.id)"; Write-Host "   Status: $($response.status) (3=Processed)"; Write-Host "   SubStatus: $($response.subStatus) (1=Success)"; Write-Host "   Result: $($response.statusResultText)"; Write-Host ""; if ($response.status -eq 3 -and $response.subStatus -eq 1) { $match = [regex]::Match($response.statusResultText, "partition (\d+), offset (\d+)"); if ($match.Success) { Write-Host "📊 Kafka Details:"; Write-Host "   Partition: $($match.Groups[1].Value)"; Write-Host "   Offset: $($match.Groups[2].Value)"; Write-Host ""; Write-Host "Check your Kafka UI for topic 'test-topic' - message count should have increased!" } } 
} catch { 
    $_
    Write-Host "Error: $($_.Exception.Message)" 
}


$body = @{ test = "data"; timestamp = (Get-Date).ToString("o");
message = "Check your Kafka UI - this should appear!" } | ConvertTo-Json;
 try { 
    $response = Invoke-RestMethod -Uri "https://localhost:7579/api/wes/k2/k2/k2" -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck; Write-Host "✅ Webhook triggered!"; Write-Host "   Event ID: $($response.id)"; Write-Host "   Status: $($response.status) (3=Processed)"; Write-Host "   SubStatus: $($response.subStatus) (1=Success)"; Write-Host "   Result: $($response.statusResultText)"; Write-Host ""; if ($response.status -eq 3 -and $response.subStatus -eq 1) { $match = [regex]::Match($response.statusResultText, "partition (\d+), offset (\d+)"); if ($match.Success) { Write-Host "📊 Kafka Details:"; Write-Host "   Partition: $($match.Groups[1].Value)"; Write-Host "   Offset: $($match.Groups[2].Value)"; Write-Host ""; Write-Host "Check your Kafka UI for topic 'test-topic' - message count should have increased!" } } 
} catch { 
    Write-Host "Error: $($_.Exception.Message)" 
}



