SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRANSACTION;

DELETE bl
FROM billing.BillLine bl
JOIN billing.Bill b ON b.BillId = bl.BillId
WHERE b.BillNumber LIKE 'QA-BILL-%';

DELETE FROM billing.Bill
WHERE BillNumber LIKE 'QA-BILL-%';

DELETE FROM course.CourseEnrollment
WHERE CourseEnrollmentId BETWEEN 960001 AND 960005;

DELETE FROM course.Course
WHERE CourseCode LIKE 'QA-%';

DELETE FROM account.AccountTransaction
WHERE IdempotencyKey LIKE 'QA_TEST_%';

DELETE FROM account.EducationAccount
WHERE AccountNumber LIKE 'QA-EA-%';

DELETE FROM person.SchoolEnrollment
WHERE SourceCode = 'QA_TEST';

DELETE FROM person.Person
WHERE MockPassPersonId LIKE 'QA_TEST_PERSON_%';

DELETE FROM org.Organization
WHERE OrganizationCode = 'QA_TEST_SCHOOL_B';

COMMIT TRANSACTION;

SELECT 'org.Organization' AS TableName, COUNT(*) AS RemainingSeedRows FROM org.Organization WHERE OrganizationCode = 'QA_TEST_SCHOOL_B'
UNION ALL SELECT 'person.Person', COUNT(*) FROM person.Person WHERE MockPassPersonId LIKE 'QA_TEST_PERSON_%'
UNION ALL SELECT 'person.SchoolEnrollment', COUNT(*) FROM person.SchoolEnrollment WHERE SourceCode = 'QA_TEST'
UNION ALL SELECT 'account.EducationAccount', COUNT(*) FROM account.EducationAccount WHERE AccountNumber LIKE 'QA-EA-%'
UNION ALL SELECT 'account.AccountTransaction', COUNT(*) FROM account.AccountTransaction WHERE IdempotencyKey LIKE 'QA_TEST_%'
UNION ALL SELECT 'course.Course', COUNT(*) FROM course.Course WHERE CourseCode LIKE 'QA-%'
UNION ALL SELECT 'course.CourseEnrollment', COUNT(*) FROM course.CourseEnrollment WHERE CourseEnrollmentId BETWEEN 960001 AND 960005
UNION ALL SELECT 'billing.Bill', COUNT(*) FROM billing.Bill WHERE BillNumber LIKE 'QA-BILL-%';
