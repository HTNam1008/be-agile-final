/*
    Repair demo Singpass issuer for Azure MocPass.

    Why:
    - Seed data was created with local MockPass issuer:
        http://localhost:5156/singpass/v3/fapi
    - Azure production uses MocPass issuer:
        https://mocpass.azurewebsites.net/singpass/v3/fapi
    - If the issuer does not match, Singpass callback cannot find the seeded
      ESERVICE login account and may fail with:
        Singpass  login could not be completed.

    Safe  to run multiple times.
*/

DECLARE @OldIssuer nvarchar(300) = N'http://localhost:5156/singpass/v3/fapi';
DECLARE @NewIssuer nvarchar(300) = N'https://mocpass.azurewebsites.net/singpass/v3/fapi';

PRINT 'Before update';

SELECT
    ExternalIssuer,
    COUNT(*) AS AccountCount
FROM [iam].[LoginAccount]
WHERE [IdentityProviderCode] = N'SINGPASS'
  AND [PortalAccessCode] = N'ESERVICE'
GROUP BY ExternalIssuer
ORDER BY ExternalIssuer;

UPDATE [iam].[LoginAccount]
SET [ExternalIssuer] = @NewIssuer,
    [UpdatedAt] = SYSUTCDATETIME()
WHERE [IdentityProviderCode] = N'SINGPASS'
  AND [PortalAccessCode] = N'ESERVICE'
  AND [ExternalIssuer] = @OldIssuer;

PRINT CONCAT('Rows updated: ', @@ROWCOUNT);

PRINT 'After update';

SELECT
    ExternalIssuer,
    COUNT(*) AS AccountCount
FROM [iam].[LoginAccount]
WHERE [IdentityProviderCode] = N'SINGPASS'
  AND [PortalAccessCode] = N'ESERVICE'
GROUP BY ExternalIssuer
ORDER BY ExternalIssuer;

/*
    Optional check for first 5 demo accounts.
*/
SELECT TOP (5)
    [LoginAccountId],
    [PersonId],
    [ExternalIssuer],
    [ExternalSubjectId],
    [ProviderLoginName],
    [LoginStatusCode]
FROM [iam].[LoginAccount]
WHERE [IdentityProviderCode] = N'SINGPASS'
  AND [PortalAccessCode] = N'ESERVICE'
ORDER BY [LoginAccountId];
