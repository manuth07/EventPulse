SELECT "MigrationId" FROM "__EFMigrationsHistory" ORDER BY "MigrationId";
SELECT column_name, data_type, is_nullable FROM information_schema.columns WHERE table_name = 'Events' ORDER BY ordinal_position;
