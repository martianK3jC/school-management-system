-- Read-only report. Run before and after applying HardenEnrollmentRecords.
-- This does not modify or remove records and also works before SectionId exists.
SELECT "Id", "Lrn",
       NOT ("Lrn" ~ '^[0-9]{12}$') AS invalid_lrn,
       NOT (length(btrim("FirstName")) BETWEEN 1 AND 100
            AND length(btrim("LastName")) BETWEEN 1 AND 100
            AND length(btrim("MiddleName")) <= 100) AS invalid_name,
       ("BirthDate" <= DATE '0001-01-01' OR "BirthDate" > CURRENT_DATE) AS invalid_birth_date,
       ("Status" NOT IN ('Enrolled', 'Transferred', 'Withdrawn', 'Graduated')
            OR "Sex" NOT IN ('Male', 'Female')) AS invalid_status_or_sex,
       (length(btrim("GuardianName")) > 200 OR length(btrim("GuardianContact")) > 50) AS invalid_guardian
FROM school."Learners"
WHERE NOT ("Lrn" ~ '^[0-9]{12}$')
   OR NOT (length(btrim("FirstName")) BETWEEN 1 AND 100
            AND length(btrim("LastName")) BETWEEN 1 AND 100
            AND length(btrim("MiddleName")) <= 100)
   OR "BirthDate" <= DATE '0001-01-01' OR "BirthDate" > CURRENT_DATE
   OR "Status" NOT IN ('Enrolled', 'Transferred', 'Withdrawn', 'Graduated')
   OR "Sex" NOT IN ('Male', 'Female')
   OR length(btrim("GuardianName")) > 200 OR length(btrim("GuardianContact")) > 50;

SELECT l."Id", l."GradeLevelId", l."SectionName", 'No matching section in this school and grade' AS issue
FROM school."Learners" l
WHERE l."SectionName" <> '' AND NOT EXISTS (
    SELECT 1 FROM school."Sections" s WHERE s."InstitutionId" = l."InstitutionId"
      AND s."GradeLevelId" = l."GradeLevelId" AND s."Name" = l."SectionName");

SELECT l."Id", 'Grade belongs to a different institution' AS issue
FROM school."Learners" l JOIN school."GradeLevels" g ON g."Id" = l."GradeLevelId"
WHERE g."InstitutionId" <> l."InstitutionId";

SELECT "Id", "Name", 'Invalid section values' AS issue FROM school."Sections"
WHERE NOT (length(btrim("Name")) BETWEEN 1 AND 100
    AND length(btrim("AdviserName")) <= 200 AND length(btrim("RoomNumber")) <= 100
    AND "MaxCapacity" >= 0 AND "Status" IN ('Active', 'Inactive'));

SELECT s."Id", 'Section grade belongs to a different institution' AS issue
FROM school."Sections" s JOIN school."GradeLevels" g ON g."Id" = s."GradeLevelId"
WHERE g."InstitutionId" <> s."InstitutionId";
