

$apihost = "http://localhost:5431"

#remove backslash from the end of the URL
if ($apihost.EndsWith("/")) {
    $apihost = $apihost.Substring(0, $apihost.Length - 1)
}

$headers = @{
    "Content-Type" = "application/json"
    "Accept" = "application/json"
}


$webhooks = IRM -uri "$apihost/api/webhooks" -method GET -headers $headers


$firstWebhook = $webhooks[0]
$owner = $firstWebhook.owner
$project = $firstWebhook.project
$slug = $firstWebhook.slug
$webhookId = $firstWebhook.id

# Submit a test event
$newWebhookEvent = irm -uri "$apihost/api/wes/$owner/$project/$slug" -method GET -headers $headers


# Get an event
$webhookEvent = irm -uri "$apihost/api/webhookevents/receive/$webhookId" -method GET -headers $headers
$webhookEventId = $webhookEvent.id
# do some work on the event:

# Close the event
$res = @{
    Status = 1;
    ResultText = "Webhook event processed successfully";
} | ConvertTo-Json -Depth 5
$webhookEvent = irm -uri "$apihost/api/webhookevents/return/$webhookEventId" -method PUT -headers $headers -body $res