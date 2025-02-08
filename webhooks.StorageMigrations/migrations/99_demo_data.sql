INSERT INTO Webhooks (Id, Name, Slug, Url, CreatedAt, CreatedBy, Owner, Project, Status)
VALUES 
    (NEWID(), 'Webhook 1', 'webhook-1', 'webhook1', GETUTCDATE(), 'User1', '1', '1', 1),
    (NEWID(), 'Webhook 2', 'webhook-2', 'webhook2', GETUTCDATE(), 'User1', '1', '1', 1),
    (NEWID(), 'Webhook 3', 'webhook-3', 'webhook3', GETUTCDATE(), 'User1', '1', '1', 1),
    (NEWID(), 'Webhook 4', 'webhook-4', 'webhook4', GETUTCDATE(), 'User1', '1', '1', 1),
    (NEWID(), 'Webhook 5', 'webhook-5', 'webhook5', GETUTCDATE(), 'User1', '1', '1', 1);