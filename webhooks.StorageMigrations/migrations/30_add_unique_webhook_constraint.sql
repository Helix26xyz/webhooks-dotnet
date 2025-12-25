-- Migration: 30_add_unique_webhook_constraint.sql
-- Add unique constraint on Owner, Project, Slug combination
-- This prevents duplicate webhooks that would break the /api/wes endpoint

-- First, we need to alter the columns from NVARCHAR(MAX) to NVARCHAR(450)
-- because SQL Server doesn't allow indexes on MAX columns
-- 450 is chosen as it's a common limit that works with unique indexes

-- Alter Owner column
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') 
           AND name = 'Owner' 
           AND max_length = -1)  -- -1 means MAX
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ALTER COLUMN Owner NVARCHAR(450) NOT NULL;
END

-- Alter Project column
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') 
           AND name = 'Project' 
           AND max_length = -1)
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ALTER COLUMN Project NVARCHAR(450) NOT NULL;
END

-- Alter Slug column
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') 
           AND name = 'Slug' 
           AND max_length = -1)
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ALTER COLUMN Slug NVARCHAR(450) NOT NULL;
END

-- Now create the unique index on Owner, Project, Slug combination
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE name = 'IX_Webhooks_Owner_Project_Slug' 
               AND object_id = OBJECT_ID(N'[dbo].[Webhooks]'))
BEGIN
    CREATE UNIQUE INDEX IX_Webhooks_Owner_Project_Slug 
    ON [dbo].[Webhooks] (Owner, Project, Slug);
END
