/*
    Final MFA database schema for MOE Student Finance.

    This script is intentionally standalone so it can be reviewed and converted
    into an EF Core migration later by the migration owner.
*/

IF SCHEMA_ID(N'iam') IS NULL
BEGIN
    EXEC(N'CREATE SCHEMA iam');
END;
GO

CREATE TABLE iam.LoginMfaCredential
(
    LoginMfaCredentialId bigint IDENTITY(1,1) NOT NULL,
    LoginAccountId bigint NOT NULL,
    MfaTypeCode varchar(30) NOT NULL,
    SecretHash varbinary(256) NOT NULL,
    SecretSalt varbinary(32) NOT NULL,
    SecretHashAlgorithm varchar(50) NOT NULL,
    StatusCode varchar(30) NOT NULL,
    FailedAttemptCount int NOT NULL CONSTRAINT DF_LoginMfaCredential_FailedAttemptCount DEFAULT 0,
    LockedUntilUtc datetime2 NULL,
    LastVerifiedAtUtc datetime2 NULL,
    CreatedAtUtc datetime2 NOT NULL,
    UpdatedAtUtc datetime2 NOT NULL,
    RowVersion rowversion NOT NULL,

    CONSTRAINT PK_LoginMfaCredential PRIMARY KEY (LoginMfaCredentialId),
    CONSTRAINT FK_LoginMfaCredential_LoginAccount
        FOREIGN KEY (LoginAccountId)
        REFERENCES iam.LoginAccount(LoginAccountId),
    CONSTRAINT CK_LoginMfaCredential_MfaTypeCode
        CHECK (MfaTypeCode IN ('PIN')),
    CONSTRAINT CK_LoginMfaCredential_StatusCode
        CHECK (StatusCode IN ('ACTIVE', 'DISABLED', 'RESET_REQUIRED')),
    CONSTRAINT CK_LoginMfaCredential_FailedAttemptCount
        CHECK (FailedAttemptCount >= 0)
);
GO

CREATE UNIQUE INDEX UX_LoginMfaCredential_LoginAccount_MfaType
ON iam.LoginMfaCredential(LoginAccountId, MfaTypeCode);
GO

CREATE INDEX IX_LoginMfaCredential_LoginAccount
ON iam.LoginMfaCredential(LoginAccountId);
GO

CREATE TABLE iam.LoginMfaChallenge
(
    LoginMfaChallengeId uniqueidentifier NOT NULL,
    LoginAccountId bigint NOT NULL,
    PurposeCode varchar(30) NOT NULL,
    StatusCode varchar(30) NOT NULL,
    FailedAttemptCount int NOT NULL CONSTRAINT DF_LoginMfaChallenge_FailedAttemptCount DEFAULT 0,
    ExpiresAtUtc datetime2 NOT NULL,
    VerifiedAtUtc datetime2 NULL,
    CreatedAtUtc datetime2 NOT NULL,

    CONSTRAINT PK_LoginMfaChallenge PRIMARY KEY (LoginMfaChallengeId),
    CONSTRAINT FK_LoginMfaChallenge_LoginAccount
        FOREIGN KEY (LoginAccountId)
        REFERENCES iam.LoginAccount(LoginAccountId),
    CONSTRAINT CK_LoginMfaChallenge_PurposeCode
        CHECK (PurposeCode IN ('SETUP', 'VERIFY', 'LOGIN')),
    CONSTRAINT CK_LoginMfaChallenge_StatusCode
        CHECK (StatusCode IN ('PENDING', 'VERIFIED', 'EXPIRED', 'FAILED')),
    CONSTRAINT CK_LoginMfaChallenge_FailedAttemptCount
        CHECK (FailedAttemptCount >= 0)
);
GO

CREATE INDEX IX_LoginMfaChallenge_LoginAccount_Status_Expires
ON iam.LoginMfaChallenge(LoginAccountId, StatusCode, ExpiresAtUtc);
GO

CREATE TABLE iam.LoginMfaAuditEvent
(
    LoginMfaAuditEventId bigint IDENTITY(1,1) NOT NULL,
    LoginAccountId bigint NOT NULL,
    LoginMfaChallengeId uniqueidentifier NULL,
    EventCode varchar(60) NOT NULL,
    PerformedByAccountId bigint NULL,
    Reason nvarchar(1000) NULL,
    IpAddress nvarchar(64) NULL,
    UserAgent nvarchar(512) NULL,
    CreatedAtUtc datetime2 NOT NULL,

    CONSTRAINT PK_LoginMfaAuditEvent PRIMARY KEY (LoginMfaAuditEventId),
    CONSTRAINT FK_LoginMfaAuditEvent_LoginAccount
        FOREIGN KEY (LoginAccountId)
        REFERENCES iam.LoginAccount(LoginAccountId),
    CONSTRAINT FK_LoginMfaAuditEvent_LoginMfaChallenge
        FOREIGN KEY (LoginMfaChallengeId)
        REFERENCES iam.LoginMfaChallenge(LoginMfaChallengeId),
    CONSTRAINT FK_LoginMfaAuditEvent_PerformedByAccount
        FOREIGN KEY (PerformedByAccountId)
        REFERENCES iam.LoginAccount(LoginAccountId)
);
GO

CREATE INDEX IX_LoginMfaAuditEvent_LoginAccount_CreatedAt
ON iam.LoginMfaAuditEvent(LoginAccountId, CreatedAtUtc DESC);
GO

CREATE INDEX IX_LoginMfaAuditEvent_PerformedByAccount
ON iam.LoginMfaAuditEvent(PerformedByAccountId);
GO

/*
    Example rows. Do not run these in production with real hashes.

    INSERT INTO iam.LoginMfaCredential
    (
        LoginAccountId,
        MfaTypeCode,
        SecretHash,
        SecretSalt,
        SecretHashAlgorithm,
        StatusCode,
        CreatedAtUtc,
        UpdatedAtUtc
    )
    VALUES
    (
        1001,
        'PIN',
        0x00,
        0x00,
        'PBKDF2-SHA256-100000',
        'ACTIVE',
        SYSUTCDATETIME(),
        SYSUTCDATETIME()
    );
*/
