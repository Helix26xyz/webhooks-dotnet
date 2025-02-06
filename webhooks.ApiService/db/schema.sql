DROP TABLE WebhookEvents;
DROP TABLE WebHooks;
DROP TABLE WebHookStatus;
DROP TABLE WebhookEventStatus;
DROP TABLE WebhookEventSubStatus;

CREATE TABLE WebhookStatus (
	Value int NOT NULL PRIMARY KEY,
	[Name] varchar(50) NOT NULL
);
CREATE TABLE WebhookEventStatus (
	Value int NOT NULL PRIMARY KEY,
	[Name] varchar(50) NOT NULL
);
CREATE TABLE WebhookEventSubStatus (
	Value int NOT NULL PRIMARY KEY,
	[Name] varchar(50) NOT NULL
);

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
	FOREIGN KEY (Status) REFERENCES WebhookStatus(Value),
);


CREATE TABLE WebhookEvents (
    Id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
    WebhookId UNIQUEIDENTIFIER NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
	UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Payload NVARCHAR(MAX) NULL,
	Status int NOT NULL,
	SubStatus int NOT NULL,
    FOREIGN KEY (WebhookId) REFERENCES Webhooks(Id),
    FOREIGN KEY (Status) REFERENCES WebhookEventStatus(Value),
	FOREIGN KEY (SubStatus) REFERENCES WebhookEventSubStatus(Value),
	StatusResultText text
);

INSERT INTO WebhookStatus (Value, Name) VALUES

(1,'enabled'),
(2,'disabled'),
(3,'suspended')

INSERT INTO WebhookEventStatus (Value, Name) VALUES

(1,'new'),
(2,'received'),
(3,'processed')

INSERT INTO WebhookEventSubStatus (Value, Name) VALUES
(0,'pending'),
(1,'success'),
(2,'failed'),
(3,'retry'),
(4, 'skipped')


INSERT INTO Webhooks (Id, Name, Slug, Url, CreatedAt, CreatedBy, Owner, Project, Status)
VALUES 
    (NEWID(), 'Webhook 1', 'webhook-1', 'webhook1', GETUTCDATE(), 'User1', '1', '1', 1),
    (NEWID(), 'Webhook 2', 'webhook-2', 'webhook2', GETUTCDATE(), 'User1', '1', '1', 1),
    (NEWID(), 'Webhook 3', 'webhook-3', 'webhook3', GETUTCDATE(), 'User1', '1', '1', 1),
    (NEWID(), 'Webhook 4', 'webhook-4', 'webhook4', GETUTCDATE(), 'User1', '1', '1', 1),
    (NEWID(), 'Webhook 5', 'webhook-5', 'webhook5', GETUTCDATE(), 'User1', '1', '1', 1);
