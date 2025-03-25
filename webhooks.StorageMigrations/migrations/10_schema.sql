IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WebhookStatus]') AND type in (N'U'))
BEGIN
    CREATE TABLE WebhookStatus (
        Value int NOT NULL PRIMARY KEY,
        [Name] varchar(50) NOT NULL
    );
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WebhookEventStatus]') AND type in (N'U'))
BEGIN
    CREATE TABLE WebhookEventStatus (
        Value int NOT NULL PRIMARY KEY,
        [Name] varchar(50) NOT NULL
    );
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WebhookEventSubStatus]') AND type in (N'U'))
BEGIN
    CREATE TABLE WebhookEventSubStatus (
        Value int NOT NULL PRIMARY KEY,
        [Name] varchar(50) NOT NULL
    );
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') AND type in (N'U'))
BEGIN
    CREATE TABLE Webhooks (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        Name NVARCHAR(MAX) NOT NULL DEFAULT '',
        Slug NVARCHAR(MAX) NOT NULL DEFAULT '',
        Url NVARCHAR(MAX) NOT NULL DEFAULT '',
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        CreatedBy NVARCHAR(MAX) NOT NULL DEFAULT '',
        Owner NVARCHAR(MAX) NOT NULL DEFAULT '',
        Project NVARCHAR(MAX) NOT NULL DEFAULT '',
        Status int NOT NULL,
        FOREIGN KEY (Status) REFERENCES WebhookStatus(Value)
    );
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[WebhookEvents]') AND type in (N'U'))
BEGIN
    CREATE TABLE WebhookEvents (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        WebhookId UNIQUEIDENTIFIER NOT NULL,
        CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        Payload NVARCHAR(MAX) NULL,
        Status int NOT NULL,
        SubStatus int NOT NULL,
        StatusResultText NVARCHAR(MAX) NULL,
        FOREIGN KEY (WebhookId) REFERENCES Webhooks(Id),
        FOREIGN KEY (Status) REFERENCES WebhookEventStatus(Value),
        FOREIGN KEY (SubStatus) REFERENCES WebhookEventSubStatus(Value)
    );
END

IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[Migrations]') AND type in (N'U'))
BEGIN
    CREATE TABLE Migrations (
        Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        MigrationName NVARCHAR(255) NOT NULL,
        AppliedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        Backend NVARCHAR(50) NOT NULL,
        Description NVARCHAR(MAX) NULL
    );
END

IF NOT EXISTS (SELECT * FROM WebhookStatus)
BEGIN
    INSERT INTO WebhookStatus (Value, Name) VALUES
    (1,'enabled'),
    (2,'disabled'),
    (3,'suspended')
END

IF NOT EXISTS (SELECT * FROM WebhookEventStatus)
BEGIN
    INSERT INTO WebhookEventStatus (Value, Name) VALUES
    (1,'new'),
    (2,'received'),
    (3,'processed')
END

IF NOT EXISTS (SELECT * FROM WebhookEventSubStatus)
BEGIN
    INSERT INTO WebhookEventSubStatus (Value, Name) VALUES
    (0,'pending'),
    (1,'success'),
    (2,'failed'),
    (3,'retry'),
    (4, 'skipped')
END
