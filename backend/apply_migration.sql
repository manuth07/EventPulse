ALTER TABLE "Events" ADD COLUMN IF NOT EXISTS "ImageBlobName" character varying(500);
INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20260903043217_AddEventPosterReference', '10.0.0-preview.1.25081.1')
ON CONFLICT ("MigrationId") DO NOTHING;
