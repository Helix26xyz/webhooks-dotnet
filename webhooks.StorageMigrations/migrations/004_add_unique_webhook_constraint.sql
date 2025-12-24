-- Add unique index on Owner, Project, Slug combination
-- This prevents duplicate webhooks that would break the /api/wes endpoint
CREATE UNIQUE INDEX IX_Webhooks_Owner_Project_Slug 
ON Webhooks (Owner, Project, Slug);
