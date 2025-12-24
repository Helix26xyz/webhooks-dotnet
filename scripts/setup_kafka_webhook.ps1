$kafkaTopic = "k3"
$kafkaServer = "10.10.100.93:9092"
$API_HOST = "https://localhost:7579"
$API_HOST = "http://10.11.213.29:8080"

$webhook_definition = @{
    name = "k3"
    slug = "k3"
    url = ""
    owner = "k3"
    project = "k3"
    status = 1
    backendType = 2
    backendConfig = @{
        BootstrapServers = $kafkaServer
        Topic = $kafkaTopic
    }
    deliveryMode = 1
} | ConvertTo-Json


function New-Webhook {
    param (
        [string]$apiHost,
        [string]$webhookDefinition
    )

    try {
        $response = Invoke-RestMethod -Uri "$apiHost/api/Webhooks" -Method Post -Body $webhookDefinition -ContentType "application/json" -SkipCertificateCheck
        Write-Host "Webhook created successfully!"
        Write-Host "Webhook ID: $($response.id)"
        return $response
    } catch {
        Write-Host "Failed to create webhook: $($_.Exception.Message)"
        return $null
    }
}

function Invoke-Webhook{
    param (
        [string]$apiHost,
        [object]$webhook_definition,
        [hashtable]$payload
    )

    $body = $payload | ConvertTo-Json

    try {
        $response = Invoke-RestMethod -Uri "$apiHost/api/wes/$($webhook_definition.owner)/$($webhook_definition.project)/$($webhook_definition.slug)/" -Method Post -Body $body -ContentType "application/json" -SkipCertificateCheck
        Write-Host "✅ Webhook invoked successfully!"
        return $response
    } catch {
        Write-Host "Failed to invoke webhook: $($_.Exception.Message)"
        return $null
    }
}


New-Webhook -apiHost $API_HOST -webhookDefinition $webhook_definition
$sample_payload = @{
    message = "Hello, Kafka Webhook!"
    timestamp = (Get-Date).ToString("o")
    }
    Invoke-Webhook -apiHost $API_HOST -webhook_definition $webhook_definition -payload $sample_payload
