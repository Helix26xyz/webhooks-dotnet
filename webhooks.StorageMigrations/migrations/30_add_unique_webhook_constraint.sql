-- Migration: 30_add_unique_webhook_constraint.sql
-- Add unique constraint on Owner, Project, Slug combination
-- This prevents duplicate webhooks that would break the /api/wes endpoint

-- First, we need to alter the columns from NVARCHAR(MAX) to NVARCHAR(450)
-- because SQL Server doesn't allow indexes on MAX columns
-- 450 is chosen as it's a common limit that works with unique indexes

-- Helper to drop default constraints if they exist
DECLARE @sql NVARCHAR(MAX);

-- Drop default constraint on Owner column if it exists
SELECT @sql = 'ALTER TABLE [dbo].[Webhooks] DROP CONSTRAINT ' + dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Webhooks]')
  AND c.name = 'Owner';
IF @sql IS NOT NULL EXEC sp_executesql @sql;
SET @sql = NULL;

-- Drop default constraint on Project column if it exists
SELECT @sql = 'ALTER TABLE [dbo].[Webhooks] DROP CONSTRAINT ' + dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Webhooks]')
  AND c.name = 'Project';
IF @sql IS NOT NULL EXEC sp_executesql @sql;
SET @sql = NULL;

-- Drop default constraint on Slug column if it exists
SELECT @sql = 'ALTER TABLE [dbo].[Webhooks] DROP CONSTRAINT ' + dc.name
FROM sys.default_constraints dc
JOIN sys.columns c ON dc.parent_column_id = c.column_id
WHERE dc.parent_object_id = OBJECT_ID(N'[dbo].[Webhooks]')
  AND c.name = 'Slug';
IF @sql IS NOT NULL EXEC sp_executesql @sql;

-- Now alter the columns to NVARCHAR(450)
-- Alter Owner column
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') 
           AND name = 'Owner' 
           AND max_length = -1)  -- -1 means MAX
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ALTER COLUMN Owner NVARCHAR(450) NOT NULL;
    
    -- Recreate default constraint
    ALTER TABLE [dbo].[Webhooks]
    ADD CONSTRAINT DF_Webhooks_Owner DEFAULT ('') FOR Owner;
END

-- Alter Project column
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') 
           AND name = 'Project' 
           AND max_length = -1)
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ALTER COLUMN Project NVARCHAR(450) NOT NULL;
    
    -- Recreate default constraint
    ALTER TABLE [dbo].[Webhooks]
    ADD CONSTRAINT DF_Webhooks_Project DEFAULT ('') FOR Project;
END

-- Alter Slug column
IF EXISTS (SELECT * FROM sys.columns 
           WHERE object_id = OBJECT_ID(N'[dbo].[Webhooks]') 
           AND name = 'Slug' 
           AND max_length = -1)
BEGIN
    ALTER TABLE [dbo].[Webhooks]
    ALTER COLUMN Slug NVARCHAR(450) NOT NULL;
    
    -- Recreate default constraint
    ALTER TABLE [dbo].[Webhooks]
    ADD CONSTRAINT DF_Webhooks_Slug DEFAULT ('') FOR Slug;
END

-- Now create the unique index on Owner, Project, Slug combination
IF NOT EXISTS (SELECT * FROM sys.indexes 
               WHERE name = 'IX_Webhooks_Owner_Project_Slug' 
               AND object_id = OBJECT_ID(N'[dbo].[Webhooks]'))
BEGIN
    CREATE UNIQUE INDEX IX_Webhooks_Owner_Project_Slug 
    ON [dbo].[Webhooks] (Owner, Project, Slug);
END
