[CmdletBinding()]
param(
    [string]$BaseUrl = "https://localhost:7000",
    [string]$AdminToken = $env:MOE_ADMIN_TOKEN,
    [string]$AdminMfaSession = $env:MOE_ADMIN_MFA_SESSION,
    [string]$StudentToken = $env:MOE_STUDENT_TOKEN,
    [ValidateSet(
        "AccountCreation",
        "AdminEnrollment",
        "TopUp",
        "MonthlyBill",
        "MonthlyBillPositive",
        "MissedInstallmentPrepare",
        "FasSubmit",
        "FasApprove",
        "FasReject",
        "AccountClosure",
        "Lifecycle",
        "CourseWithdrawal")]
    [string[]]$Flow = @("AccountCreation"),
    [string]$ExpectedRecipientEmail,
    [long]$OrganizationId = 2,
    [long]$PersonId,
    [long]$EducationAccountId,
    [long]$CourseId,
    [long]$CoursePaymentPlanId,
    [switch]$CreateMockCourse,
    [long]$CourseFeeComponentId = 4,
    [switch]$CreateMockEnrollment,
    [string]$StudentNumber,
    [long]$CampaignId,
    [long[]]$TopUpEducationAccountIds,
    [switch]$CreateMockTopUpCampaign,
    [long]$EnrollmentId,
    [long]$FasApplicationId,
    [string]$FasRejectionReasonCode = "OTHER",
    [int]$BillingYear = (Get-Date).Year,
    [ValidateRange(1, 12)]
    [int]$BillingMonth = (Get-Date).Month,
    [ValidateRange(0, 120)]
    [int]$WaitSeconds = 10,
    [switch]$AllowLocalDatabaseMutation,
    [string]$SqlServer = "LAPTOP-312U8AGK\MSSQLSERVER04",
    [string]$SqlDatabase = "StudentFinance",
    [switch]$ListFlows
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$script:AdminSessionEstablished = $false
$script:WebSession = New-Object Microsoft.PowerShell.Commands.WebRequestSession

$flowDetails = @(
    [pscustomobject]@{ Flow = "AccountCreation"; Method = "POST"; Endpoint = "/api/admin/v1/education-accounts"; Mail = "NOTI-07"; Recipient = "EducationAccount.PersonId -> newest STUDENT iam.LoginAccount.ContactEmail; Development fallback when missing" },
    [pscustomobject]@{ Flow = "AdminEnrollment"; Method = "POST"; Endpoint = "/api/admin/v1/courses/{courseId}/enrollments"; Mail = "NOTI-04"; Recipient = "Enrollment.PersonId -> newest STUDENT iam.LoginAccount.ContactEmail" },
    [pscustomobject]@{ Flow = "TopUp"; Method = "POST"; Endpoint = "/api/admin/v1/top-up-campaigns/{campaignId}/runs"; Mail = "NOTI-02"; Recipient = "Each credited EducationAccount.PersonId -> newest STUDENT iam.LoginAccount.ContactEmail" },
    [pscustomobject]@{ Flow = "MonthlyBill"; Method = "GET"; Endpoint = "/api/eservice/v1/billing-statements/{year}/{month}"; Mail = "NOTI-01"; Recipient = "Authenticated student PersonId -> newest STUDENT iam.LoginAccount.ContactEmail" },
    [pscustomobject]@{ Flow = "MonthlyBillPositive"; Method = "POST + GET"; Endpoint = "Create mock course/enrollment, then GET billing statement"; Mail = "NOTI-01"; Recipient = "Authenticated student -> newest STUDENT iam.LoginAccount.ContactEmail" },
    [pscustomobject]@{ Flow = "MissedInstallmentPrepare"; Method = "POST + local SQL"; Endpoint = "Create installment bill and prepare yesterday due date"; Mail = "NOTI-11 after API restart"; Recipient = "Authenticated student -> newest STUDENT iam.LoginAccount.ContactEmail" },
    [pscustomobject]@{ Flow = "FasSubmit"; Method = "POST"; Endpoint = "/api/eservice/v1/fas/applications/{id}/submit"; Mail = "NOTI-05 submit"; Recipient = "fas.FasApplication.Email" },
    [pscustomobject]@{ Flow = "FasApprove"; Method = "POST"; Endpoint = "/api/admin/v1/fas/applications/{id}/approve"; Mail = "NOTI-05 approved"; Recipient = "fas.FasApplication.Email" },
    [pscustomobject]@{ Flow = "FasReject"; Method = "POST"; Endpoint = "/api/admin/v1/fas/applications/{id}/reject"; Mail = "NOTI-05 rejected"; Recipient = "fas.FasApplication.Email" },
    [pscustomobject]@{ Flow = "AccountClosure"; Method = "POST"; Endpoint = "/api/admin/v1/education-accounts/{id}/close"; Mail = "NOTI-06"; Recipient = "EducationAccount.PersonId -> newest STUDENT iam.LoginAccount.ContactEmail" },
    [pscustomobject]@{ Flow = "Lifecycle"; Method = "POST"; Endpoint = "/api/admin/v1/education-account-lifecycle/run-now"; Mail = "NOTI-06/07 and age-30 reminders"; Recipient = "Each affected PersonId -> newest STUDENT iam.LoginAccount.ContactEmail" },
    [pscustomobject]@{ Flow = "CourseWithdrawal"; Method = "POST"; Endpoint = "/api/eservice/v1/course-enrollments/{id}/cancel"; Mail = "NOTI-10"; Recipient = "Enrollment.PersonId -> newest STUDENT iam.LoginAccount.ContactEmail" }
)

if ($ListFlows)
{
    $flowDetails | Format-Table -AutoSize
    return
}

function Get-BearerToken
{
    param([string]$Token)

    if ([string]::IsNullOrWhiteSpace($Token))
    {
        return $null
    }

    return $Token.Trim() -replace '^Bearer\s+', ''
}

function Assert-Value
{
    param(
        [bool]$Condition,
        [string]$Message
    )

    if (-not $Condition)
    {
        throw $Message
    }
}

function Enable-LocalhostCertificateBypass
{
    if (-not ("MailTestLocalhostCertificateValidation" -as [type]))
    {
        Add-Type @"
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

public static class MailTestLocalhostCertificateValidation
{
    public static bool Validate(
        object sender,
        X509Certificate certificate,
        X509Chain chain,
        SslPolicyErrors errors)
    {
        return true;
    }
}
"@
    }

    $validationMethod = [MailTestLocalhostCertificateValidation].GetMethod("Validate")
    $validationCallback = [Delegate]::CreateDelegate(
        [System.Net.Security.RemoteCertificateValidationCallback],
        $validationMethod)
    [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.SecurityProtocolType]::Tls12
    [System.Net.ServicePointManager]::ServerCertificateValidationCallback = $validationCallback
}

function Invoke-MoeApi
{
    param(
        [ValidateSet("GET", "POST", "PUT", "PATCH", "DELETE")]
        [string]$Method,
        [string]$Path,
        [string]$Token,
        [object]$Body,
        [hashtable]$AdditionalHeaders = @{}
    )

    $uri = "{0}/{1}" -f $BaseUrl.TrimEnd('/'), $Path.TrimStart('/')
    $headers = @{
        Accept = "application/json"
    }

    $bearerToken = Get-BearerToken $Token
    $isAdminRequest = -not [string]::IsNullOrWhiteSpace($AdminToken) -and $bearerToken -eq (Get-BearerToken $AdminToken)
    if ($null -ne $bearerToken -and (-not $isAdminRequest -or -not $script:AdminSessionEstablished))
    {
        $headers.Authorization = "Bearer $bearerToken"
    }

    foreach ($header in $AdditionalHeaders.GetEnumerator())
    {
        $headers[$header.Key] = $header.Value
    }

    $parameters = @{
        Uri = $uri
        Method = $Method
        Headers = $headers
        ContentType = "application/json"
        WebSession = $script:WebSession
    }

    if ($null -ne $Body)
    {
        $parameters.Body = $Body | ConvertTo-Json -Depth 12 -Compress
    }

    if ((Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck"))
    {
        $parameters.SkipCertificateCheck = $true
    }
    elseif ($BaseUrl.StartsWith("https://localhost", [StringComparison]::OrdinalIgnoreCase))
    {
        Enable-LocalhostCertificateBypass
    }

    Write-Host "[$Method] $uri" -ForegroundColor Cyan

    try
    {
        $response = Invoke-RestMethod @parameters
        Write-Host ($response | ConvertTo-Json -Depth 12)
        return $response
    }
    catch
    {
        $errorBody = if ($null -ne $_.ErrorDetails) { $_.ErrorDetails.Message } else { $null }
        if (-not [string]::IsNullOrWhiteSpace($errorBody))
        {
            Write-Host $errorBody -ForegroundColor Red
        }
        elseif ($null -ne $_.Exception.Response)
        {
            $responseStream = $_.Exception.Response.GetResponseStream()
            if ($null -ne $responseStream)
            {
                $reader = New-Object System.IO.StreamReader($responseStream)
                try
                {
                    Write-Host $reader.ReadToEnd() -ForegroundColor Red
                }
                finally
                {
                    $reader.Dispose()
                }
            }
        }
        else
        {
            Write-Host $_.Exception.Message -ForegroundColor Red
        }

        throw
    }
}

function Write-FlowHeader
{
    param(
        [string]$Name,
        [string]$Notification,
        [string]$Recipient
    )

    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkGray
    Write-Host "$Name ($Notification)" -ForegroundColor Yellow
    Write-Host "Recipient: $Recipient" -ForegroundColor Yellow
    if (-not [string]::IsNullOrWhiteSpace($ExpectedRecipientEmail))
    {
        Write-Host "Expected Gmail: $ExpectedRecipientEmail" -ForegroundColor Yellow
    }
    Write-Host "============================================================" -ForegroundColor DarkGray
}

function Require-AdminToken
{
    Assert-Value (-not [string]::IsNullOrWhiteSpace($AdminToken)) "Admin token is required. Set `$env:MOE_ADMIN_TOKEN or pass -AdminToken."
    Initialize-AdminSession
}

function Require-StudentToken
{
    Assert-Value (-not [string]::IsNullOrWhiteSpace($StudentToken)) "Student token is required. Set `$env:MOE_STUDENT_TOKEN or pass -StudentToken."
}

function Initialize-AdminSession
{
    if ($script:AdminSessionEstablished)
    {
        return
    }

    $bearerToken = Get-BearerToken $AdminToken
    $parameters = @{
        Uri = "$($BaseUrl.TrimEnd('/'))/api/admin/v1/auth/session"
        Method = "POST"
        Headers = @{ Authorization = "Bearer $bearerToken"; Accept = "application/json" }
        ContentType = "application/json"
        WebSession = $script:WebSession
    }

    if ((Get-Command Invoke-RestMethod).Parameters.ContainsKey("SkipCertificateCheck"))
    {
        $parameters.SkipCertificateCheck = $true
    }
    elseif ($BaseUrl.StartsWith("https://localhost", [StringComparison]::OrdinalIgnoreCase))
    {
        Enable-LocalhostCertificateBypass
    }

    Write-Host "[POST] $($parameters.Uri) (establish admin cookie session)" -ForegroundColor Cyan
    [void](Invoke-RestMethod @parameters)

    if (-not [string]::IsNullOrWhiteSpace($AdminMfaSession))
    {
        $mfaCookie = New-Object System.Net.Cookie(
            "moe_admin_mfa_session",
            $AdminMfaSession.Trim(),
            "/",
            ([Uri]$BaseUrl).Host)
        $script:WebSession.Cookies.Add($mfaCookie)
    }

    $script:AdminSessionEstablished = $true
    Write-Host "Admin cookie session established." -ForegroundColor Green
}

function New-MockBilledEnrollment
{
    param(
        [ValidateSet("FULL_PAYMENT", "INSTALLMENT")]
        [string]$PlanTypeCode,
        [int]$InstallmentCount,
        [string]$ScenarioCode
    )

    Require-AdminToken
    Require-StudentToken
    $suffix = "{0}{1}" -f (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss"), (Get-Random -Minimum 100 -Maximum 999)
    $courseStartDate = (Get-Date).ToUniversalTime().Date.AddDays(30)
    Write-FlowHeader "Create mock billed enrollment" "$ScenarioCode prerequisite" "No mail until the notification trigger"

    $courseResponse = Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses" -Token $AdminToken -Body @{
        organizationId = $OrganizationId
        courseCode = "MAIL-$ScenarioCode-$suffix"
        courseName = "Mail $ScenarioCode Test $suffix"
        description = "Created by mail test script"
        startDate = $courseStartDate.ToString("yyyy-MM-dd")
        endDate = $courseStartDate.AddDays(30).ToString("yyyy-MM-dd")
        enrollmentOpenAt = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
        enrollmentCloseAt = $courseStartDate.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
        beforeStartRefundPercentage = 100
        afterStartRefundPercentage = 50
    }
    $newCourseId = [long]$courseResponse.data.courseId

    Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses/$newCourseId/fees" -Token $AdminToken -Body @{
        feeComponentId = $CourseFeeComponentId
        feeValue = 1.00
        sequenceNumber = 1
    } | Out-Null

    $planResponse = Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses/$newCourseId/payment-plans" -Token $AdminToken -Body @{
        displayName = if ($PlanTypeCode -eq "INSTALLMENT") { "Two installments" } else { "Pay in full" }
        planTypeCode = $PlanTypeCode
        installmentCount = $InstallmentCount
    }
    $newPlanId = [long]$planResponse.data.coursePaymentPlanId

    Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses/$newCourseId/publish" -Token $AdminToken -Body $null | Out-Null
    $enrollmentResponse = Invoke-MoeApi -Method POST -Path "/api/eservice/v1/course-enrollments" -Token $StudentToken -Body @{
        courseId = $newCourseId
        coursePaymentPlanId = $newPlanId
        fasApplicationSchemeIds = @()
    }

    return [pscustomobject]@{
        CourseId = $newCourseId
        CoursePaymentPlanId = $newPlanId
        CourseEnrollmentId = [long]$enrollmentResponse.data.courseEnrollmentId
        BillId = [long]$enrollmentResponse.data.billId
        BillDueDate = [string]$enrollmentResponse.data.generatedBills[0].currentDueDate
    }
}

$appSettingsPath = Join-Path $PSScriptRoot "..\..\src\Hosts\Moe.StudentFinance.Api\appsettings.json"
$developmentFallbackRecipient = $null
if (Test-Path $appSettingsPath)
{
    $appSettingsContent = Get-Content $appSettingsPath -Raw
    $mailDeliveryMatch = [regex]::Match(
        $appSettingsContent,
        '"MailDelivery"\s*:\s*\{(?<Body>.*?)\}\s*,',
        [Text.RegularExpressions.RegexOptions]::Singleline)

    if ($mailDeliveryMatch.Success)
    {
        $mailDeliveryBody = $mailDeliveryMatch.Groups["Body"].Value
        $enabledMatch = [regex]::Match($mailDeliveryBody, '"Enabled"\s*:\s*(?<Value>true|false)', [Text.RegularExpressions.RegexOptions]::IgnoreCase)
        $fallbackMatch = [regex]::Match($mailDeliveryBody, '"DevelopmentFallbackRecipient"\s*:\s*"(?<Value>[^"]*)"')
        $mailEnabled = if ($enabledMatch.Success) { $enabledMatch.Groups["Value"].Value } else { "unknown" }
        $developmentFallbackRecipient = if ($fallbackMatch.Success) { $fallbackMatch.Groups["Value"].Value } else { $null }
        Write-Host "MailDelivery.Enabled: $mailEnabled" -ForegroundColor Green
        Write-Host "Configured DevelopmentFallbackRecipient: $developmentFallbackRecipient" -ForegroundColor Green
    }
}

$selectedFlows = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($selectedFlow in $Flow)
{
    [void]$selectedFlows.Add($selectedFlow)
}

if ($selectedFlows.Contains("AccountCreation"))
{
    Require-AdminToken
    $accountPersonId = $PersonId
    $recipientDescription = "newest STUDENT iam.LoginAccount.ContactEmail"

    if ($accountPersonId -le 0)
    {
        $suffix = "{0}{1}" -f (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss"), (Get-Random -Minimum 100 -Maximum 999)
        $mockStudentNumber = "MAIL-$suffix"
        $mockIdentityNumber = "MAIL$suffix"
        $mockEmail = if ([string]::IsNullOrWhiteSpace($ExpectedRecipientEmail)) { "mail-test-$suffix@example.com" } else { $ExpectedRecipientEmail }

        Write-FlowHeader "Create mock student prerequisite" "No mail" "The request email is stored on person.Person and is not the mail-service recipient"
        $studentResponse = Invoke-MoeApi -Method POST -Path "/api/admin/v1/students" -Token $AdminToken -Body @{
            organizationId = $OrganizationId
            identityNumber = $mockIdentityNumber
            fullName = "Mail Test Student $suffix"
            dateOfBirth = "2008-01-01"
            nationalityCode = "SG"
            citizenshipStatusCode = "CITIZEN"
            studentNumber = $mockStudentNumber
            academicYear = (Get-Date).Year.ToString()
            levelCode = "PRI_2"
            classCode = "MAIL"
            startDate = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-dd")
            email = $mockEmail
            mobile = "90000000"
            address = "Mail test data"
            isAccountHolder = $false
        }

        $accountPersonId = [long]$studentResponse.data.personId
        $recipientDescription = "DevelopmentFallbackRecipient '$developmentFallbackRecipient' because a newly created Person has no STUDENT LoginAccount.ContactEmail"
        Write-Host "Created mock PersonId: $accountPersonId" -ForegroundColor Green
        Write-Host "Created mock StudentNumber: $mockStudentNumber" -ForegroundColor Green
    }

    Write-FlowHeader "Create Education Account" "NOTI-07" $recipientDescription
    $accountResponse = Invoke-MoeApi -Method POST -Path "/api/admin/v1/education-accounts" -Token $AdminToken -Body @{
        personId = $accountPersonId
        reasonCode = "MANUAL_CREATE"
        remarks = "Mail test script"
    }

    if ($null -ne $accountResponse.data.educationAccountId)
    {
        Write-Host "EducationAccountId: $($accountResponse.data.educationAccountId)" -ForegroundColor Green
        if ($selectedFlows.Contains("AccountClosure") -and $EducationAccountId -le 0)
        {
            $EducationAccountId = [long]$accountResponse.data.educationAccountId
            Write-Host "The newly created account will be used by the AccountClosure flow." -ForegroundColor Green
        }
    }
}

if ($selectedFlows.Contains("AdminEnrollment"))
{
    Require-AdminToken
    Assert-Value ($CourseId -gt 0) "-CourseId is required for AdminEnrollment."
    Assert-Value (-not [string]::IsNullOrWhiteSpace($StudentNumber)) "-StudentNumber is required for AdminEnrollment."
    Write-FlowHeader "Admin adds student to course" "NOTI-04" "student PersonId -> newest STUDENT iam.LoginAccount.ContactEmail"
    Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses/$CourseId/enrollments" -Token $AdminToken -Body @{
        studentNumber = $StudentNumber
    } | Out-Null
}

if ($selectedFlows.Contains("TopUp"))
{
    Require-AdminToken
    $targetCampaignId = $CampaignId

    if ($CreateMockTopUpCampaign)
    {
        Assert-Value (@($TopUpEducationAccountIds).Count -gt 0) "-TopUpEducationAccountIds is required with -CreateMockTopUpCampaign."
        $campaignSuffix = "{0}{1}" -f (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss"), (Get-Random -Minimum 100 -Maximum 999)
        Write-FlowHeader "Create mock immediate top-up campaign" "Prerequisite" "No mail until the campaign run credits accounts"
        $campaignResponse = Invoke-MoeApi -Method POST -Path "/api/admin/v1/top-up-campaigns" -Token $AdminToken -Body @{
            organizationId = $OrganizationId
            campaignCode = "MAIL-$campaignSuffix"
            campaignName = "Mail Test Campaign $campaignSuffix"
            description = "Created by mail test script"
            recipientModeCode = "FixedSelection"
            defaultTopUpAmount = 0.01
            reason = "Mail delivery test"
            scheduleTypeCode = "IMMEDIATE"
            startDate = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd")
            endDate = $null
            frequencyCode = $null
            frequencyInterval = $null
        }
        $targetCampaignId = [long]$campaignResponse

        $recipients = @($TopUpEducationAccountIds | ForEach-Object {
            @{ educationAccountId = $_; amountOverride = 0.01 }
        })
        Invoke-MoeApi -Method PUT -Path "/api/admin/v1/top-up-campaigns/$targetCampaignId/fixed-recipients" -Token $AdminToken -Body @{
            mode = "ExplicitIds"
            filter = $null
            recipients = $recipients
            excludedEducationAccountIds = @()
        } | Out-Null
        Invoke-MoeApi -Method PATCH -Path "/api/admin/v1/top-up-campaigns/$targetCampaignId/status" -Token $AdminToken -Body @{
            topUpCampaignId = $targetCampaignId
            newStatusCode = "ACTIVE"
        } | Out-Null
        Write-Host "Created active mock TopUpCampaignId: $targetCampaignId" -ForegroundColor Green
    }

    Assert-Value ($targetCampaignId -gt 0) "-CampaignId is required for TopUp, or use -CreateMockTopUpCampaign with -TopUpEducationAccountIds."
    Write-FlowHeader "Execute top-up campaign" "NOTI-02" "each successfully credited account holder -> newest STUDENT iam.LoginAccount.ContactEmail"
    $idempotencyKey = "MAIL-TEST-$targetCampaignId-$([Guid]::NewGuid().ToString('N'))"
    $runResponse = Invoke-MoeApi -Method POST -Path "/api/admin/v1/top-up-campaigns/$targetCampaignId/runs" -Token $AdminToken -Body @{
        idempotencyKey = $idempotencyKey
        note = "Mail test script"
    }

    $runId = [long]$runResponse.data.runId
    for ($attempt = 1; $attempt -le 30; $attempt++)
    {
        Start-Sleep -Seconds 2
        $summary = Invoke-MoeApi -Method GET -Path "/api/admin/v1/top-up/runs/$runId" -Token $AdminToken -Body $null
        $status = [string]$summary.data.status
        Write-Host "Run $runId status: $status" -ForegroundColor DarkCyan
        if ($status -in @("COMPLETED", "FAILED", "PARTIALLY_COMPLETED"))
        {
            break
        }
    }
}

if ($selectedFlows.Contains("MonthlyBill"))
{
    Require-StudentToken
    Write-FlowHeader "Retrieve monthly billing statement" "NOTI-01" "authenticated student -> newest STUDENT iam.LoginAccount.ContactEmail"
    Write-Host "Mail is sent only when a new statement/item is created and outstanding amount is greater than zero." -ForegroundColor DarkYellow
    Invoke-MoeApi -Method GET -Path "/api/eservice/v1/billing-statements/$BillingYear/$BillingMonth" -Token $StudentToken -Body $null | Out-Null
}

if ($selectedFlows.Contains("MonthlyBillPositive"))
{
    $setup = New-MockBilledEnrollment -PlanTypeCode "FULL_PAYMENT" -InstallmentCount 1 -ScenarioCode "NOTI01"
    $dueDate = [DateTime]::ParseExact($setup.BillDueDate, "yyyy-MM-dd", [Globalization.CultureInfo]::InvariantCulture)
    Write-FlowHeader "Generate statement with outstanding bill" "NOTI-01 positive" "authenticated student -> newest STUDENT iam.LoginAccount.ContactEmail"
    Invoke-MoeApi -Method GET -Path "/api/eservice/v1/billing-statements/$($dueDate.Year)/$($dueDate.Month)" -Token $StudentToken -Body $null | Out-Null
    Write-Host "NOTI-01 trigger data: CourseId=$($setup.CourseId) EnrollmentId=$($setup.CourseEnrollmentId) BillId=$($setup.BillId)" -ForegroundColor Green
}

if ($selectedFlows.Contains("MissedInstallmentPrepare"))
{
    Assert-Value $AllowLocalDatabaseMutation.IsPresent "MissedInstallmentPrepare changes one local Bill.CurrentDueDate. Pass -AllowLocalDatabaseMutation explicitly."
    Assert-Value $BaseUrl.StartsWith("https://localhost", [StringComparison]::OrdinalIgnoreCase) "MissedInstallmentPrepare is allowed only against localhost."
    $setup = New-MockBilledEnrollment -PlanTypeCode "INSTALLMENT" -InstallmentCount 2 -ScenarioCode "NOTI11"
    $yesterday = (Get-Date).ToUniversalTime().Date.AddDays(-1).ToString("yyyy-MM-dd")
    $sql = @"
SET NOCOUNT ON;
UPDATE billing.Bill
SET CurrentDueDate = '$yesterday', DueDate = '$yesterday'
WHERE BillId = $($setup.BillId)
  AND OutstandingAmount > 0
  AND BillStatusCode NOT IN ('PAID', 'CANCELLED');
IF @@ROWCOUNT <> 1 THROW 51000, 'Expected exactly one eligible local test bill.', 1;
"@
    & sqlcmd -S $SqlServer -d $SqlDatabase -E -C -b -Q $sql
    if ($LASTEXITCODE -ne 0)
    {
        throw "Failed to prepare NOTI-11 local test bill."
    }

    Write-Host "NOTI-11 prepared: CourseId=$($setup.CourseId) EnrollmentId=$($setup.CourseEnrollmentId) BillId=$($setup.BillId) DueDate=$yesterday" -ForegroundColor Green
    Write-Host "Restart the API now. MissedInstallmentPaymentEmailWorker runs immediately on startup and should log 'Queued email delivered'." -ForegroundColor Yellow
}

if ($selectedFlows.Contains("FasSubmit"))
{
    Require-StudentToken
    Assert-Value ($FasApplicationId -gt 0) "-FasApplicationId is required for FasSubmit and must be a valid completed draft."
    Write-FlowHeader "Submit FAS application" "NOTI-05 submit" "fas.FasApplication.Email"
    Invoke-MoeApi -Method POST -Path "/api/eservice/v1/fas/applications/$FasApplicationId/submit" -Token $StudentToken -Body $null | Out-Null
}

if ($selectedFlows.Contains("FasApprove"))
{
    Require-AdminToken
    Assert-Value ($FasApplicationId -gt 0) "-FasApplicationId is required for FasApprove."
    Write-FlowHeader "Approve FAS application" "NOTI-05 approved" "fas.FasApplication.Email"
    Invoke-MoeApi -Method POST -Path "/api/admin/v1/fas/applications/$FasApplicationId/approve" -Token $AdminToken -Body @{
        remarks = "Approved by mail test script"
    } | Out-Null
}

if ($selectedFlows.Contains("FasReject"))
{
    Require-AdminToken
    Assert-Value ($FasApplicationId -gt 0) "-FasApplicationId is required for FasReject."
    Write-FlowHeader "Reject FAS application" "NOTI-05 rejected" "fas.FasApplication.Email"
    Invoke-MoeApi -Method POST -Path "/api/admin/v1/fas/applications/$FasApplicationId/reject" -Token $AdminToken -Body @{
        rejectionReasonCode = $FasRejectionReasonCode
        remarks = "Rejected by mail test script"
    } | Out-Null
}

if ($selectedFlows.Contains("AccountClosure"))
{
    Require-AdminToken
    Assert-Value ($EducationAccountId -gt 0) "-EducationAccountId is required for AccountClosure. This operation changes the account status."
    Write-FlowHeader "Close Education Account" "NOTI-06" "EducationAccount.PersonId -> newest STUDENT iam.LoginAccount.ContactEmail"
    Invoke-MoeApi -Method POST -Path "/api/admin/v1/education-accounts/$EducationAccountId/close" -Token $AdminToken -Body @{
        reasonCode = "OTHER"
        remarks = "Mail test script"
    } | Out-Null
}

if ($selectedFlows.Contains("Lifecycle"))
{
    Require-AdminToken
    Write-FlowHeader "Run Education Account lifecycle" "NOTI-06/07 and age-30 reminder" "each affected PersonId -> newest STUDENT iam.LoginAccount.ContactEmail"
    Write-Host "This endpoint only sends mail when records satisfy lifecycle dates and account conditions." -ForegroundColor DarkYellow
    Invoke-MoeApi -Method POST -Path "/api/admin/v1/education-account-lifecycle/run-now" -Token $AdminToken -Body $null | Out-Null
}

if ($selectedFlows.Contains("CourseWithdrawal"))
{
    Require-StudentToken
    if ($CreateMockCourse)
    {
        Require-AdminToken
        $courseSuffix = "{0}{1}" -f (Get-Date).ToUniversalTime().ToString("yyyyMMddHHmmss"), (Get-Random -Minimum 100 -Maximum 999)
        $courseStartDate = (Get-Date).ToUniversalTime().Date.AddDays(30)
        Write-FlowHeader "Create mock course for withdrawal" "Prerequisite" "No mail"
        $courseResponse = Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses" -Token $AdminToken -Body @{
            organizationId = $OrganizationId
            courseCode = "MAIL-WD-$courseSuffix"
            courseName = "Mail Withdrawal Test $courseSuffix"
            description = "Created by mail test script"
            startDate = $courseStartDate.ToString("yyyy-MM-dd")
            endDate = $courseStartDate.AddDays(30).ToString("yyyy-MM-dd")
            enrollmentOpenAt = (Get-Date).ToUniversalTime().AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
            enrollmentCloseAt = $courseStartDate.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ssZ")
            beforeStartRefundPercentage = 100
            afterStartRefundPercentage = 50
        }
        $CourseId = [long]$courseResponse.data.courseId

        Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses/$CourseId/fees" -Token $AdminToken -Body @{
            feeComponentId = $CourseFeeComponentId
            feeValue = 1.00
            sequenceNumber = 1
        } | Out-Null

        $planResponse = Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses/$CourseId/payment-plans" -Token $AdminToken -Body @{
            displayName = "Pay in full"
            planTypeCode = "FULL_PAYMENT"
            installmentCount = 1
        }
        $CoursePaymentPlanId = [long]$planResponse.data.coursePaymentPlanId

        Invoke-MoeApi -Method POST -Path "/api/admin/v1/courses/$CourseId/publish" -Token $AdminToken -Body $null | Out-Null
        $CreateMockEnrollment = $true
        Write-Host "Created published CourseId: $CourseId" -ForegroundColor Green
        Write-Host "Created CoursePaymentPlanId: $CoursePaymentPlanId" -ForegroundColor Green
    }

    if ($CreateMockEnrollment)
    {
        Assert-Value ($CourseId -gt 0) "-CourseId is required with -CreateMockEnrollment."
        Assert-Value ($CoursePaymentPlanId -gt 0) "-CoursePaymentPlanId is required with -CreateMockEnrollment."
        Write-FlowHeader "Create self-enrollment prerequisite" "No mail until payment success" "authenticated student"
        $enrollmentResponse = Invoke-MoeApi -Method POST -Path "/api/eservice/v1/course-enrollments" -Token $StudentToken -Body @{
            courseId = $CourseId
            coursePaymentPlanId = $CoursePaymentPlanId
            fasApplicationSchemeIds = @()
        }
        $EnrollmentId = [long]$enrollmentResponse.data.courseEnrollmentId
        Write-Host "Created CourseEnrollmentId: $EnrollmentId" -ForegroundColor Green
    }

    Assert-Value ($EnrollmentId -gt 0) "-EnrollmentId is required for CourseWithdrawal, or use -CreateMockEnrollment with course and plan IDs."
    Write-FlowHeader "Cancel course enrollment" "NOTI-10" "Enrollment.PersonId -> newest STUDENT iam.LoginAccount.ContactEmail"
    Invoke-MoeApi -Method POST -Path "/api/eservice/v1/course-enrollments/$EnrollmentId/cancel" -Token $StudentToken -Body @{
        idempotencyKey = "MAIL-TEST-CANCEL-$([Guid]::NewGuid().ToString('N'))"
    } | Out-Null
}

if ($WaitSeconds -gt 0)
{
    Write-Host ""
    Write-Host "Waiting $WaitSeconds second(s) for QueuedEmailDeliveryWorker..." -ForegroundColor Cyan
    Start-Sleep -Seconds $WaitSeconds
}

Write-Host ""
Write-Host "Mail test calls completed." -ForegroundColor Green
Write-Host "If mail is missing, inspect API logs for: 'Queued email', 'no valid recipient', or 'email delivery failed'." -ForegroundColor Green
Write-Host "Recipient SQL is documented in scripts/mail-test/README.md." -ForegroundColor Green
