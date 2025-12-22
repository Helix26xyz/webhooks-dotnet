-- Add backend configuration fields to Webhooks table
-- Migration: 20_add_backend_config.sql

-- Add BackendType column (defaults to Database = 1 for existing webhooks)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') AND name = 'BackendType')
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ADD BackendType int NOT NULL DEFAULT 1;
END

-- Add BackendConfig column (JSON configuration for backend)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') AND name = 'BackendConfig')
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ADD BackendConfig NVARCHAR(MAX) NULL;
END

-- Add DeliveryMode column (defaults to Synchronous = 1)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') AND name = 'DeliveryMode')
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ADD DeliveryMode int NOT NULL DEFAULT 1;
END

-- Add LastReceivedAt column (tracks when webhook last received a payload)
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') AND name = 'LastReceivedAt')
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ADD LastReceivedAt DATETIME2 NULL;
END

-- Create WebhookBackendType reference table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WebhookBackendType]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[WebhookBackendType] (
        Value int PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL
    );
END

-- Create WebhookDeliveryMode reference table
IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WebhookDeliveryMode]') AND type in (N'U'))
BEGIN
    CREATE TABLE [dbo].[WebhookDeliveryMode] (
        Value int PRIMARY KEY,
        Name NVARCHAR(50) NOT NULL
    );
END

-- Populate WebhookBackendType reference data
IF NOT EXISTS (SELECT * FROM WebhookBackendType)
BEGIN
    INSERT INTO WebhookBackendType (Value, Name) VALUES
    (1, 'Database'),
    (2, 'Kafka'),
    (3, 'RabbitMQ'),
    (4, 'AzureServiceBus'),
    (5, 'AWSSQS'),
    (6, 'Redis'),
    (99, 'Custom');
END

-- Populate WebhookDeliveryMode reference data
IF NOT EXISTS (SELECT * FROM WebhookDeliveryMode)
BEGIN
    INSERT INTO WebhookDeliveryMode (Value, Name) VALUES
    (1, 'Synchronous'),
    (2, 'Asynchronous');
END

-- Add foreign key constraints (optional, but recommended)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Webhooks_BackendType')
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ADD CONSTRAINT FK_Webhooks_BackendType
    FOREIGN KEY (BackendType) REFERENCES WebhookBackendType(Value);
END

IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_Webhooks_DeliveryMode')
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ADD CONSTRAINT FK_Webhooks_DeliveryMode
    FOREIGN KEY (DeliveryMode) REFERENCES WebhookDeliveryMode(Value);
END
