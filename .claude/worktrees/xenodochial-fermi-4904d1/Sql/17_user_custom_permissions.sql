USE reclamos_auto;

ALTER TABLE Users
    ADD COLUMN IF NOT EXISTS CustomPermissionsJson TEXT NULL;
