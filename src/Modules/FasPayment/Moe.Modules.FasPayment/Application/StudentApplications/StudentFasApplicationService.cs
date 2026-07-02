using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moe.Application.Abstractions.Audit;
using Moe.Application.Abstractions.Clock;
using Moe.Application.Abstractions.Security;
using Moe.Modules.CourseBilling.Domain.Courses;
using Moe.Modules.EducationAccountTopUp.Domain.EducationAccounts;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.Application.Notifications;
using Moe.Modules.FasPayment.Infrastructure.Documents;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.Modules.IdentityPlatform.IGateway.Repositories;
using Moe.Modules.IdentityPlatform.IGateway.Students;
using Moe.Modules.Notifications.Domain.Notifications;
using Moe.Modules.Notifications.IGateway.Notifications;
using Moe.SharedKernel.Results;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.StudentApplications;

public sealed class StudentFasApplicationService(
    MoeDbContext db,
    ICurrentUser currentUser,
    IFasDocumentStorage storage,
    IFasDocumentScanner scanner,
    IOrganizationUnitRepository organizations,
    IAuditService audit,
    FasEmailNotificationService fasEmails,
    IClock clock,
    IStudentNotificationRecipientResolver studentNotificationRecipients,
    ISchoolAdminNotificationRecipientResolver schoolAdminRecipients,
    INotificationWriter notificationWriter,
    ILogger<StudentFasApplicationService> logger)
{
    private (long PersonId, long ActorId) Identity() =>
        (currentUser.PersonId ?? throw new UnauthorizedAccessException("FAS.AUTHENTICATION_REQUIRED"),
         currentUser.UserAccountId ?? throw new UnauthorizedAccessException("FAS.AUTHENTICATION_REQUIRED"));
    private long Actor() => currentUser.UserAccountId ?? throw new UnauthorizedAccessException("FAS.AUTHENTICATION_REQUIRED");
    private DateOnly Today() => DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);

    private async Task<ProfileRow> Profile(CancellationToken ct)
    {
        var (personId, _) = Identity();
        DateOnly today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var person = await db.Set<Person>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == personId, ct)
            ?? throw new KeyNotFoundException("FAS.PROFILE_REQUIRED");
        var enrollment = await db.Set<SchoolEnrollment>().AsNoTracking()
            .Where(x => x.PersonId == personId && x.SchoolingStatusCode == "ACTIVE" &&
                        x.StartDate <= today &&
                        (x.EndDate == null || x.EndDate >= today))
            .OrderByDescending(x => x.StartDate).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("FAS.CURRENT_SCHOOL_REQUIRED");
        string? schoolName = (await organizations.FindActiveSchoolByIdAsync(enrollment.OrganizationId, ct))?.UnitName
            ?? $"School {enrollment.OrganizationId}";
        return new(person.Id, person.OfficialFullName, person.IdentityNumberMasked, person.DateOfBirth,
            person.NationalityCode, person.PreferredMobile ?? person.OfficialMobile,
            person.PreferredAddress ?? person.OfficialAddress, person.PreferredEmail ?? person.OfficialEmail,
            enrollment.OrganizationId, schoolName, enrollment.StudentNumber);
    }

    public async Task<object> Prefill(CancellationToken ct)
    {
        var p = await Profile(ct);
        var accountTypeCode = await ResolveAccountType(p.PersonId, ct);
        return new
        {
            p.PersonId,
            p.Name,
            p.NricFinMasked,
            p.DateOfBirth,
            p.NationalityCode,
            p.Mobile,
            p.Address,
            p.Email,
            p.SchoolOrganizationId,
            p.SchoolName,
            p.StudentNumber,
            accountTypeCode
        };
    }

    public async Task<EligibilityCriteriaPlan> EligibilityCriteriaPlan(CancellationToken ct)
    {
        var p = await Profile(ct);
        var accountType = await ResolveAccountType(p.PersonId, ct);
        var today = Today();
        var applicable = await ApplicableSchemeIds(p.SchoolOrganizationId, ct);
        var openSchemes = await db.Set<FasScheme>()
            .AsNoTracking()
            .Where(x => x.StatusCode == "ACTIVE" && x.StartDate <= today && x.EndDate >= today && applicable.Contains(x.Id))
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(ct);

        long[] schemeIds = openSchemes.Select(x => x.Id).ToArray();
        string[] requiredCriteriaTypes = schemeIds.Length == 0
            ? []
            : await (
                from tier in db.Set<FasTier>().AsNoTracking()
                join criteria in db.Set<FasTierCriteria>().AsNoTracking()
                    on tier.Id equals criteria.FasTierId
                where schemeIds.Contains(tier.FasSchemeId)
                select criteria.CriteriaType)
                .Distinct()
                .OrderBy(x => x)
                .ToArrayAsync(ct);

        List<string> profileFacts = [$"school: {p.SchoolName}", $"student nationality: {p.NationalityCode}", $"account type: {accountType}", "date of birth"];
        if (!string.IsNullOrWhiteSpace(p.Email)) profileFacts.Add("email");

        List<string> userRequiredFacts = ["welfare home status"];
        if (requiredCriteriaTypes.Any(IsIncomeCriterion))
        {
            userRequiredFacts.Add("monthly household income");
            userRequiredFacts.Add("household member count");
            userRequiredFacts.Add("other monthly income");
        }
        userRequiredFacts.Add("parent or guardian nationality");

        return new EligibilityCriteriaPlan(
            openSchemes.Select(x => new EligibilitySchemeOption(x.Id, x.Name)).OrderBy(x => x.Name).ToArray(),
            openSchemes.Select(x => x.Name).OrderBy(x => x).ToArray(),
            requiredCriteriaTypes,
            profileFacts,
            userRequiredFacts.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
    }

    public async Task<object> ListSchemes(int page, int pageSize, string? search, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100) throw new ArgumentException("FAS.INVALID_PAGING");
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search?.Length > 255) throw new ArgumentException("FAS.SEARCH_TOO_LONG");

        var p = await Profile(ct); var today = Today(); var applicable = await ApplicableSchemeIds(p.SchoolOrganizationId, ct);
        IQueryable<FasScheme> query = db.Set<FasScheme>().AsNoTracking().Where(x => x.StatusCode == "ACTIVE" && applicable.Contains(x.Id));
        if (search is not null) query = query.Where(x => x.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)));
        long totalCount = await query.LongCountAsync(ct);
        var schemes = await query
            .OrderBy(x => x.StartDate)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                id = x.Id,
                x.Name,
                shortDescription = x.Description,
                applicationStartDate = x.StartDate,
                applicationEndDate = x.EndDate,
                isOpenForApplication = x.StartDate <= today && x.EndDate >= today
            }).ToListAsync(ct);
        return new { currentSchool = new { id = p.SchoolOrganizationId, name = p.SchoolName }, items = schemes, page, pageSize, totalCount };
    }

    public async Task<object> SchemeDetail(long id, CancellationToken ct)
    {
        var profile = await Profile(ct); var today = Today(); var applicable = await ApplicableSchemeIds(profile.SchoolOrganizationId, ct);
        var scheme = await db.Set<FasScheme>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.StatusCode == "ACTIVE", ct)
            ?? throw new KeyNotFoundException("FAS.SCHEME_NOT_FOUND");
        var tiers = await db.Set<FasTier>().AsNoTracking().Where(x => x.FasSchemeId == id).OrderBy(x => x.DisplayOrder)
            .Select(x => new { id = x.Id, x.Label, subsidyType = x.SubsidyType, subsidyValue = x.SubsidyValue, x.DisplayOrder }).ToListAsync(ct);
        List<CourseRow> courses = await (
            from schemeCourse in db.Set<FasSchemeCourse>().AsNoTracking()
            join course in db.Set<Course>().AsNoTracking()
                on schemeCourse.CourseId equals course.Id
            where schemeCourse.FasSchemeId == id
            orderby course.CourseName
            select new CourseRow(course.Id, course.CourseCode, course.CourseName))
            .ToListAsync(ct);
        return new
        {
            scheme.Id,
            scheme.Name,
            scheme.Description,
            applicationStartDate = scheme.StartDate,
            applicationEndDate = scheme.EndDate,
            canApply = applicable.Contains(id) && scheme.StartDate <= today && scheme.EndDate >= today,
            benefits = tiers,
            courses,
            appliesToAllCourses = courses.Count == 0
        };
    }

    public async Task<object> CheckEligibility(EligibilityRequest request, CancellationToken ct)
    {
        if (request.MonthlyHouseholdIncome < 0 || request.OtherMonthlyIncome < 0 || request.HouseholdMemberCount <= 0)
            throw new ArgumentException("FAS.INVALID_INCOME");
        var p = await Profile(ct); var pci = (request.MonthlyHouseholdIncome + request.OtherMonthlyIncome) / request.HouseholdMemberCount;
        var parentNationalities = request.ParentNationalities?.Distinct(StringComparer.Ordinal).ToArray() ?? Array.Empty<string>();
        var accountType = await ResolveAccountType(p.PersonId, ct);
        var age = clock.UtcNow.Year - p.DateOfBirth.Year;
        var applicable = await ApplicableSchemeIds(p.SchoolOrganizationId, ct); var schemes = await db.Set<FasScheme>().AsNoTracking().Where(x => x.StatusCode == "ACTIVE" && applicable.Contains(x.Id)).ToListAsync(ct);
        var matches = new List<object>();
        foreach (var scheme in schemes)
        {
            var tiers = await db.Set<FasTier>().AsNoTracking().Where(x => x.FasSchemeId == scheme.Id).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
            foreach (var tier in tiers)
            {
                var groups = await db.Set<FasTierCriteriaGroup>().AsNoTracking().Where(x => x.FasTierId == tier.Id).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
                var criteria = await db.Set<FasTierCriteria>().AsNoTracking().Where(x => x.FasTierId == tier.Id).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
                var values = new Dictionary<long, bool>();
                foreach (FasTierCriteria c in criteria)
                {
                    bool ok = c.CriteriaType switch
                    {
                        "AGE" => age >= c.NumberFrom && age <= c.NumberTo,
                        "GDP" or "GHI" => request.MonthlyHouseholdIncome >= c.NumberFrom && request.MonthlyHouseholdIncome <= c.NumberTo,
                        "PCI" => pci >= c.NumberFrom && pci <= c.NumberTo,
                        "NATIONALITY" => await db.Set<FasTierCriteriaNationality>().AsNoTracking().AnyAsync(n => n.FasTierCriteriaId == c.Id &&
                            (n.Nationality == p.NationalityCode || (p.NationalityCode == "SG" && n.Nationality == "Singapore Citizen")), ct),
                        "PARENT_NATIONALITY" => parentNationalities.Length > 0 && await db.Set<FasTierCriteriaNationality>().AsNoTracking().AnyAsync(n => n.FasTierCriteriaId == c.Id && parentNationalities.Contains(n.Nationality), ct),
                        "ACCOUNT_TYPE" => await db.Set<FasTierCriteriaNationality>().AsNoTracking().AnyAsync(n => n.FasTierCriteriaId == c.Id && n.Nationality == accountType, ct),
                        _ => false
                    };
                    values[c.Id] = ok;
                }
                bool matched = criteria.Count == 0 || GroupsMatch(groups, criteria, values);
                if (matched)
                {
                    matches.Add(new
                    {
                        schemeId = scheme.Id,
                        schemeName = scheme.Name,
                        scheme.Description,
                        tierId = tier.Id,
                        tierLabel = tier.Label,
                        tier.SubsidyType,
                        tier.SubsidyValue
                    }); break;
                }
            }
        }
        return new
        {
            currentSchool = new { id = p.SchoolOrganizationId, name = p.SchoolName },
            age,
            nationality = p.NationalityCode,
            monthlyHouseholdIncome = request.MonthlyHouseholdIncome,
            request.HouseholdMemberCount,
            parentNationalities,
            accountType,
            perCapitaIncome = decimal.Round(pci, 2),
            matchedSchemes = matches
        };
    }

    public async Task<object> CreateOrResumeDraft(CreateDraftRequest request, CancellationToken ct)
    {
        long[] ids = ValidateSelectedSchemeIds(request.SchemeIds);
        var p = await Profile(ct);
        var (personId, actorId) = Identity();
        DateTime now = clock.UtcNow.UtcDateTime;

        var valid = await OpenApplicableSchemeIds(p.SchoolOrganizationId, ids, ct);
        if (valid.Count != ids.Length) throw new InvalidOperationException("FAS.SCHEME_NOT_AVAILABLE");

        var app = await db.Set<FasApplication>().SingleOrDefaultAsync(x => x.StudentPersonId == personId && x.StatusCode == FasApplicationStatuses.Draft, ct);
        await EnsureNoDuplicateApplications(personId, ids, app?.Id, ct);

        bool created = app == null;
        if (app == null)
        {
            string accountType = await ResolveAccountType(personId, ct);
            app = FasApplication.CreateDraft($"FAS-{now:yyyyMMddHHmmss}-{personId}", personId, ids[0], p.StudentNumber,
                p.Name, p.NricFinMasked, p.DateOfBirth, p.NationalityCode, p.Mobile, p.Address, p.Email,
                p.SchoolOrganizationId, p.SchoolName, accountType, actorId, now);

            db.Add(app);
            await db.SaveChangesAsync(ct);
            db.Add(FasStatusHistory.Create(app.Id, null, null, FasApplicationStatuses.Draft, "Application draft created", actorId, "STUDENT", now));
        }

        var added = await ReplaceSchemesCore(app, ids, actorId, now, ct);
        await db.SaveChangesAsync(ct);

        foreach (var item in added)
        {
            db.Add(FasStatusHistory.Create(app.Id, item.Id, null, "DRAFT", "Scheme selected", actorId, "STUDENT", now));
        }

        if (created)
        {
            await RecordFasApplicationAuditAsync(
                AuditActionCodes.FasApplicationCreated,
                app,
                "FAS application created",
                "DRAFT",
                ct);
        }

        if (created || added.Count > 0) await db.SaveChangesAsync(ct);
        return await ApplicationReview(app.Id, ct);
    }

    public async Task<object> ReplaceSchemes(long appId, ReplaceSchemesRequest request, CancellationToken ct)
    {
        var (personId, actorId) = Identity();
        var app = await OwnedDraft(appId, personId, ct);
        var profile = await Profile(ct);
        long[] ids = ValidateSelectedSchemeIds(request.SchemeIds);

        var valid = await OpenApplicableSchemeIds(profile.SchoolOrganizationId, ids, ct);
        if (valid.Count != ids.Length) throw new InvalidOperationException("FAS.SCHEME_NOT_AVAILABLE");

        await EnsureNoDuplicateApplications(personId, ids, app.Id, ct);
        DateTime now = clock.UtcNow.UtcDateTime;
        var added = await ReplaceSchemesCore(app, ids, actorId, now, ct);
        await db.SaveChangesAsync(ct);

        foreach (var item in added)
        {
            db.Add(FasStatusHistory.Create(app.Id, item.Id, null, "DRAFT", "Scheme selected", actorId, "STUDENT", now));
        }

        if (added.Count > 0) await db.SaveChangesAsync(ct);
        return await ApplicationReview(app.Id, ct);
    }

    private async Task<List<FasApplicationScheme>> ReplaceSchemesCore(FasApplication app, long[] ids, long actor, DateTime now, CancellationToken ct)
    {
        var old = await db.Set<FasApplicationScheme>().Where(x => x.FasApplicationId == app.Id && x.StatusCode == "DRAFT").ToListAsync(ct);
        db.RemoveRange(old.Where(x => !ids.Contains(x.FasSchemeId)));
        var added = ids
            .Where(id => old.All(x => x.FasSchemeId != id))
            .Select(id => FasApplicationScheme.CreateDraft(app.Id, id, actor, now))
            .ToList();

        db.AddRange(added);
        app.ReplacePrimaryScheme(ids[0], actor, now);
        return added;
    }

    public async Task<object> UpdateParticulars(long id, UpdateParticularsRequest r, CancellationToken ct)
    {
        var (person, actor) = Identity();
        var app = await OwnedEditable(id, person, ct);
        DateTime now = clock.UtcNow.UtcDateTime;

        app.UpdateEmail(r.Email, actor, now);
        app.UpdateParentNationalities(r.ParentNationalities, actor, now);
        await db.SaveChangesAsync(ct);

        return await ApplicationReview(id, ct);
    }

    public async Task<object> UpdateIncome(long id, UpdateIncomeRequest r, CancellationToken ct)
    {
        var (person, actor) = Identity();
        var app = await OwnedEditable(id, person, ct);

        app.UpdateIncome(r.IsWelfareHomeResident, r.EmploymentStatusCode, r.MonthlyHouseholdIncome, r.HouseholdMemberCount, r.OtherMonthlyIncome, actor, clock.UtcNow.UtcDateTime);
        await db.SaveChangesAsync(ct);

        return await RequiredDocuments(id, ct);
    }

    public async Task<object> RequiredDocuments(long id, CancellationToken ct)
    {
        var (person, _) = Identity();
        var app = await Owned(id, person, ct);
        return await BuildChecklist(app, ct);
    }

    private async Task<List<ChecklistItem>> BuildChecklist(FasApplication app, CancellationToken ct)
    {
        var required = new List<(string Code, string Label)>();
        if (app.IsWelfareHomeResident == true) required.Add(("WELFARE_LETTER", "Welfare home confirmation letter"));
        else switch (app.EmploymentStatusCode)
            {
                case "EMPLOYED": required.Add(("PAYSLIP", "Latest payslip")); required.Add(("CPF_STATEMENT", "CPF transaction statement")); break;
                case "SELF_EMPLOYED": required.Add(("NOA", "Latest Notice of Assessment")); break;
                case "UNEMPLOYED": required.Add(("CPF_STATEMENT", "CPF transaction statement")); break;
            }
        var docs = await db.Set<FasDocument>().AsNoTracking().Where(x => x.FasApplicationId == app.Id && x.UploadStatusCode != "REMOVED").ToListAsync(ct);
        return required.DistinctBy(x => x.Code).Select(x => new ChecklistItem(x.Code, x.Label, true,
            docs.Where(d => d.ChecklistItemCode == x.Code).OrderByDescending(d => d.UploadedAtUtc).Select(d => new { id = d.Id, d.FileName, d.MimeType, d.FileSizeBytes, scanStatus = d.UploadStatusCode }).FirstOrDefault(),
            docs.Any(d => d.ChecklistItemCode == x.Code && d.UploadStatusCode is "UPLOADED" or "SCAN_PASSED"))).ToList();
    }

    public async Task<object> SaveDeclarations(long id, SaveDeclarationsRequest r, string? ip, string? agent, CancellationToken ct)
    {
        var (person, actor) = Identity(); await OwnedEditable(id, person, ct); if (!r.TrueAndAccurate || !r.AcceptTerms) throw new ArgumentException("FAS.DECLARATIONS_REQUIRED");
        var old = await db.Set<FasDeclaration>().Where(x => x.FasApplicationId == id).ToListAsync(ct); db.RemoveRange(old);
        db.Add(FasDeclaration.Accept(id, "TRUE_AND_ACCURATE", r.TrueAndAccurateText, actor, clock.UtcNow.UtcDateTime, ip, agent));
        db.Add(FasDeclaration.Accept(id, "ACCEPT_TERMS", r.AcceptTermsText, actor, clock.UtcNow.UtcDateTime, ip, agent)); await db.SaveChangesAsync(ct);
        return new { applicationId = id, declarationsComplete = true };
    }

    public async Task<object> UploadDocument(long id, string checklistCode, string fileName, string mime, long size, Stream stream, CancellationToken ct)
    {
        var (person, actor) = Identity(); await OwnedEditable(id, person, ct); ValidateFile(fileName, mime, size);
        var app = await OwnedEditable(id, person, ct); var required = await BuildChecklist(app, ct); if (!required.Any(x => x.ChecklistItemCode == checklistCode)) throw new ArgumentException("FAS.INVALID_CHECKLIST_ITEM");
        var key = await storage.UploadAsync(id, fileName, stream, ct); var doc = FasDocument.Create(id, checklistCode, checklistCode, true, fileName, key, mime, size, actor, clock.UtcNow.UtcDateTime, scanner.RequiresScan);
        db.Add(doc); await db.SaveChangesAsync(ct); return new { id = doc.Id, doc.FileName, doc.MimeType, doc.FileSizeBytes, scanStatus = doc.UploadStatusCode };
    }

    public async Task RemoveDocument(long id, long documentId, CancellationToken ct)
    { var (person, actor) = Identity(); await OwnedEditable(id, person, ct); var d = await db.Set<FasDocument>().SingleOrDefaultAsync(x => x.Id == documentId && x.FasApplicationId == id && x.UploadStatusCode != "REMOVED", ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); d.Remove(actor, clock.UtcNow.UtcDateTime); await db.SaveChangesAsync(ct); await storage.DeleteAsync(d.BlobKey, ct); }

    public async Task<object> ReplaceDocument(long id, long documentId, string fileName, string mime, long size, Stream stream, CancellationToken ct)
    { var (person, actor) = Identity(); await OwnedEditable(id, person, ct); var old = await db.Set<FasDocument>().SingleOrDefaultAsync(x => x.Id == documentId && x.FasApplicationId == id && x.UploadStatusCode != "REMOVED", ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); ValidateFile(fileName, mime, size); var key = await storage.UploadAsync(id, fileName, stream, ct); var replacement = FasDocument.Create(id, old.DocumentTypeCode, old.ChecklistItemCode, old.IsMandatory, fileName, key, mime, size, actor, clock.UtcNow.UtcDateTime, scanner.RequiresScan); db.Add(replacement); await db.SaveChangesAsync(ct); old.Replace(replacement.Id, actor, clock.UtcNow.UtcDateTime); await db.SaveChangesAsync(ct); await storage.DeleteAsync(old.BlobKey, ct); return new { id = replacement.Id, replacement.FileName, replacement.MimeType, replacement.FileSizeBytes, scanStatus = replacement.UploadStatusCode }; }

    public async Task<(Stream Stream, string Mime, string Name)> DownloadDocument(long documentId, CancellationToken ct)
    { var (person, _) = Identity(); var doc = await db.Set<FasDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.UploadStatusCode != "REMOVED", ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); await Owned(doc.FasApplicationId, person, ct); return (await storage.OpenReadAsync(doc.BlobKey, ct), doc.MimeType, doc.FileName); }
    public async Task<(Stream Stream, string Mime, string Name)> AdminDownloadDocument(long documentId, CancellationToken ct) { Actor(); var doc = await db.Set<FasDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.UploadStatusCode != "REMOVED", ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); return (await storage.OpenReadAsync(doc.BlobKey, ct), doc.MimeType, doc.FileName); }
    public async Task<object> RecordScanResult(long documentId, bool passed, CancellationToken ct) { Actor(); var doc = await db.Set<FasDocument>().SingleOrDefaultAsync(x => x.Id == documentId, ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); if (passed) doc.MarkScanPassed(); else doc.MarkScanFailed(); await db.SaveChangesAsync(ct); return new { id = doc.Id, scanStatus = doc.UploadStatusCode }; }

    public async Task<object> Activate(long itemId, CancellationToken ct)
    { var (person, actor) = Identity(); return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => { await using var tx = await db.Database.BeginTransactionAsync(ct); var item = await (from i in db.Set<FasApplicationScheme>() join a in db.Set<FasApplication>() on i.FasApplicationId equals a.Id where i.Id == itemId && a.StudentPersonId == person select i).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("FAS.APPLICATION_SCHEME_NOT_FOUND"); if (item.ValidFrom == null || item.ValidTo == null) throw new InvalidOperationException("FAS.APPROVAL_VALIDITY_REQUIRED"); if (await db.Set<FasActiveScheme>().AnyAsync(x => x.FasApplicationSchemeId == item.Id && x.StatusCode == "ACTIVE", ct)) return new { applicationSchemeId = item.Id, status = "ACTIVE", item.ValidFrom, item.ValidTo }; var now = clock.UtcNow.UtcDateTime; item.Activate(now); db.Add(FasActiveScheme.Activate(person, item.Id, item.FasSchemeId, item.ValidFrom.Value, item.ValidTo.Value, actor, now)); db.Add(FasStatusHistory.Create(item.FasApplicationId, item.Id, "APPROVED", "ACTIVE", null, actor, "STUDENT", now)); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new { applicationSchemeId = item.Id, status = "ACTIVE", item.ValidFrom, item.ValidTo }; }); }

    private static void ValidateFile(string name, string mime, long size) { var extensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" }; var mimes = new[] { "application/pdf", "image/jpeg", "image/png" }; if (size <= 0 || size > 20 * 1024 * 1024) throw new ArgumentException("FAS.FILE_SIZE_INVALID"); if (!extensions.Contains(Path.GetExtension(name).ToLowerInvariant()) || !mimes.Contains(mime.ToLowerInvariant())) throw new ArgumentException("FAS.FILE_TYPE_INVALID"); }

    public async Task<object> AdminApplications(
        string? status,
        long? schemeId,
        string? keyword,
        DateOnly? submittedFrom,
        DateOnly? submittedTo,
        string? sortBy,
        string? sortDirection,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query =
            from application in db.Set<FasApplication>().AsNoTracking()
            join selection in db.Set<FasApplicationScheme>().AsNoTracking() on application.Id equals selection.FasApplicationId
            join scheme in db.Set<FasScheme>().AsNoTracking() on selection.FasSchemeId equals scheme.Id
            join account in db.Set<EducationAccount>().AsNoTracking() on application.StudentPersonId equals account.PersonId into accounts
            from account in accounts.DefaultIfEmpty()
            select new { application, selection, scheme, account };

        if (schemeId.HasValue) query = query.Where(x => x.scheme.Id == schemeId);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            string value = keyword.Trim();
            query = query.Where(x => x.application.StudentName.Contains(value) ||
                                     x.application.ApplicationNo.Contains(value) ||
                                     x.application.StudentId.Contains(value) ||
                                     (x.account != null && x.account.AccountNumber.Contains(value)));
        }
        if (submittedFrom.HasValue)
        {
            DateTime fromUtc = submittedFrom.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(x => x.application.SubmittedAtUtc >= fromUtc);
        }
        if (submittedTo.HasValue)
        {
            DateTime toUtc = submittedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            query = query.Where(x => x.application.SubmittedAtUtc < toUtc);
        }

        var filteredRows = (await query.ToListAsync(ct))
            .Where(x => MatchesAdminApplicationStatus(ToAdminVisibleStatus(x.application.StatusCode, x.selection.StatusCode), status))
            .ToArray();

        int total = filteredRows.Length;
        var pageRows = ApplyAdminApplicationSort(filteredRows, sortBy, sortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        long[] schemeIds = pageRows.Select(x => x.selection.FasSchemeId).Distinct().ToArray();
        var tiers = await db.Set<FasTier>().AsNoTracking().Where(x => schemeIds.Contains(x.FasSchemeId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        long[] tierIds = tiers.Select(x => x.Id).ToArray();
        var groups = await db.Set<FasTierCriteriaGroup>().AsNoTracking().Where(x => tierIds.Contains(x.FasTierId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var criteria = await db.Set<FasTierCriteria>().AsNoTracking().Where(x => tierIds.Contains(x.FasTierId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        long[] criteriaIds = criteria.Select(x => x.Id).ToArray();
        var categorical = await db.Set<FasTierCriteriaNationality>().AsNoTracking().Where(x => criteriaIds.Contains(x.FasTierCriteriaId)).ToListAsync(ct);

        var items = pageRows.Select(row =>
        {
            string? approvedTier = ExtractApprovedTierLabel(row.selection.ApprovedComponentsJson);
            string? recommendedTier = tiers
                .Where(t => t.FasSchemeId == row.selection.FasSchemeId)
                .OrderBy(t => t.DisplayOrder)
                .Where(t => TierMatches(t.Id, row.application, groups, criteria, categorical, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)))
                .Select(t => t.Label)
                .FirstOrDefault();

            return new
            {
                applicationId = row.application.Id,
                applicationSchemeId = row.selection.Id,
                applicationReference = row.application.ApplicationNo,
                studentName = row.application.StudentName,
                studentId = row.application.StudentId,
                accountNumber = row.account?.AccountNumber,
                schemeId = row.scheme.Id,
                schemeName = row.scheme.Name,
                schemeTier = approvedTier ?? recommendedTier,
                submittedAt = row.application.SubmittedAtUtc,
                status = ToAdminVisibleStatus(row.application.StatusCode, row.selection.StatusCode)
            };
        }).ToArray();

        return new { items, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
    }

    public async Task<object> AdminApplication(long id, CancellationToken ct)
    {
        var app = await db.Set<FasApplication>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException("FAS.APPLICATION_NOT_FOUND");
        var items = await (
                from i in db.Set<FasApplicationScheme>().AsNoTracking()
                join s in db.Set<FasScheme>().AsNoTracking() on i.FasSchemeId equals s.Id
                where i.FasApplicationId == id
                select new
                {
                    i.Id,
                    i.FasSchemeId,
                    s.Name,
                    s.StartDate,
                    s.EndDate,
                    i.StatusCode,
                    i.ApprovedAmount,
                    i.ApprovedComponentsJson,
                    i.RejectionNotes,
                    i.ValidFrom,
                    i.ValidTo,
                    i.IsActive
                })
            .ToListAsync(ct);

        var schemeIds = items.Select(x => x.FasSchemeId).Distinct().ToArray();
        var tiers = await db.Set<FasTier>().AsNoTracking().Where(x => schemeIds.Contains(x.FasSchemeId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var tierIds = tiers.Select(x => x.Id).ToArray();
        var groups = await db.Set<FasTierCriteriaGroup>().AsNoTracking().Where(x => tierIds.Contains(x.FasTierId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var criteria = await db.Set<FasTierCriteria>().AsNoTracking().Where(x => tierIds.Contains(x.FasTierId)).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
        var criteriaIds = criteria.Select(x => x.Id).ToArray();
        var categorical = await db.Set<FasTierCriteriaNationality>().AsNoTracking().Where(x => criteriaIds.Contains(x.FasTierCriteriaId)).ToListAsync(ct);
        var schemes = items.Select(item => new
        {
            item.Id,
            item.FasSchemeId,
            item.Name,
            item.StatusCode,
            item.ApprovedAmount,
            item.ApprovedComponentsJson,
            item.RejectionNotes,
            item.ValidFrom,
            item.ValidTo,
            item.IsActive,
            tiers = tiers
                .Where(t => t.FasSchemeId == item.FasSchemeId)
                .Select(t => new { tierId = t.Id, t.Label, t.SubsidyType, t.SubsidyValue, t.DisplayOrder })
                .ToArray(),
            recommendedTierId = tiers
                .Where(t => t.FasSchemeId == item.FasSchemeId)
                .OrderBy(t => t.DisplayOrder)
                .Where(t => TierMatches(t.Id, app, groups, criteria, categorical, DateOnly.FromDateTime(clock.UtcNow.UtcDateTime)))
                .Select(t => (long?)t.Id)
                .FirstOrDefault()
        }).ToArray();

        var documents = await db.Set<FasDocument>()
            .AsNoTracking()
            .Where(x => x.FasApplicationId == id && x.UploadStatusCode != "REMOVED")
            .Select(x => new { x.Id, x.ChecklistItemCode, x.FileName, x.MimeType, x.FileSizeBytes, x.UploadStatusCode })
            .ToListAsync(ct);

        var declarations = await db.Set<FasDeclaration>()
            .AsNoTracking()
            .Where(x => x.FasApplicationId == id)
            .Select(x => new { x.DeclarationTypeCode, x.IsAccepted, x.AcceptedAtUtc, x.DeclarationTextSnapshot })
            .ToListAsync(ct);

        var history = await db.Set<FasStatusHistory>()
            .AsNoTracking()
            .Where(x => x.FasApplicationId == id)
            .OrderBy(x => x.ChangedAtUtc)
            .Select(x => new { x.FasApplicationSchemeId, x.OldStatusCode, x.NewStatusCode, x.Notes, x.ChangedAtUtc, x.ChangedByRole })
            .ToListAsync(ct);

        return new
        {
            app.Id,
            applicationReference = app.ApplicationNo,
            app.StudentName,
            app.StudentId,
            app.NricFinMasked,
            app.DateOfBirth,
            app.NationalityCode,
            app.Mobile,
            app.Address,
            app.Email,
            currentSchool = new { id = app.SchoolOrganizationId, name = app.SchoolName, app.StudentNumber },
            income = new { app.IsWelfareHomeResident, app.EmploymentStatusCode, app.MonthlyHouseholdIncome, app.HouseholdMemberCount, app.OtherMonthlyIncome, app.PerCapitaIncome },
            app.StatusCode,
            app.SubmittedAtUtc,
            schemes,
            documents,
            declarations,
            history
        };
    }

    public async Task<object> ApproveScheme(long id, AdminApproveSchemeRequest r, CancellationToken ct)
    {
        long actor = Actor();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = await db.Set<FasApplicationScheme>().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("FAS.APPLICATION_SCHEME_NOT_FOUND");
            var tier = await db.Set<FasTier>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == r.TierId && x.FasSchemeId == item.FasSchemeId, ct)
                ?? throw new ArgumentException("FAS.INVALID_TIER");
            var scheme = await db.Set<FasScheme>().AsNoTracking().SingleAsync(x => x.Id == item.FasSchemeId, ct);

            DateOnly today = Today();
            if (scheme.StatusCode != "ACTIVE" || scheme.EndDate < today) throw new InvalidOperationException("FAS.SCHEME_NOT_AVAILABLE");

            DateTime now = clock.UtcNow.UtcDateTime;
            string components = JsonSerializer.Serialize(new { tierId = tier.Id, tier.Label, tier.SubsidyType, tier.SubsidyValue });
            item.Approve(actor, tier.SubsidyValue, components, scheme.StartDate, scheme.EndDate, now);
            db.Add(FasStatusHistory.Create(item.FasApplicationId, item.Id, "PENDING", "APPROVED", r.Remarks, actor, "ADMIN", now));

            var app = await db.Set<FasApplication>().AsNoTracking().SingleAsync(x => x.Id == item.FasApplicationId, ct);
            await RecordFasApplicationSchemeAuditAsync(
                AuditActionCodes.FasApplicationApproved,
                app,
                item,
                "FAS application approved by admin",
                "PENDING",
                "APPROVED",
                ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await fasEmails.SendSchemeApprovedAsync(item.Id, ct);
            await NotifyStudentForApprovedSchemeAsync(item.FasApplicationId, tier.Label, ct);

            return new { applicationSchemeId = item.Id, status = item.StatusCode, selectedTierId = tier.Id, item.ApprovedAmount, item.ValidFrom, item.ValidTo };
        });
    }

    public async Task<object> RejectScheme(long id, AdminRejectSchemeRequest r, CancellationToken ct)
    {
        long actor = Actor();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = await db.Set<FasApplicationScheme>().SingleOrDefaultAsync(x => x.Id == id, ct)
                ?? throw new KeyNotFoundException("FAS.APPLICATION_SCHEME_NOT_FOUND");
            DateTime now = clock.UtcNow.UtcDateTime;

            item.Reject(actor, r.Notes, now);
            db.Add(FasStatusHistory.Create(item.FasApplicationId, item.Id, "PENDING", "REJECTED", r.Notes, actor, "ADMIN", now));

            var app = await db.Set<FasApplication>().AsNoTracking().SingleAsync(x => x.Id == item.FasApplicationId, ct);
            await RecordFasApplicationSchemeAuditAsync(
                AuditActionCodes.FasApplicationRejected,
                app,
                item,
                "FAS application rejected by admin",
                "PENDING",
                "REJECTED",
                ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await fasEmails.SendSchemeRejectedAsync(item.Id, r.Notes, ct);
            await NotifyStudentForRejectedSchemeAsync(item.FasApplicationId, r.Notes, ct);

            return new { applicationSchemeId = item.Id, status = item.StatusCode, item.RejectionNotes };
        });
    }

    public async Task<object> ReviewValidation(long id, CancellationToken ct)
    {
        var review = await ApplicationReview(id, ct);
        var (person, _) = Identity();
        var app = await Owned(id, person, ct);
        var missing = new List<string>();

        if (string.IsNullOrWhiteSpace(app.StudentName)) missing.Add("particulars.name");
        if (string.IsNullOrWhiteSpace(app.NricFinMasked)) missing.Add("particulars.nricFin");
        if (app.DateOfBirth == null) missing.Add("particulars.dateOfBirth");
        if (string.IsNullOrWhiteSpace(app.NationalityCode)) missing.Add("particulars.nationality");
        if (string.IsNullOrWhiteSpace(app.ParentNationalitiesJson)) missing.Add("particulars.parentNationalities");
        if (string.IsNullOrWhiteSpace(app.Mobile)) missing.Add("particulars.mobile");
        if (string.IsNullOrWhiteSpace(app.Address)) missing.Add("particulars.address");
        if (app.SchoolOrganizationId == null || string.IsNullOrWhiteSpace(app.StudentNumber)) missing.Add("particulars.currentSchool");
        if (string.IsNullOrWhiteSpace(app.Email)) missing.Add("particulars.email");
        if (app.IsWelfareHomeResident == null || (app.IsWelfareHomeResident == false && (app.MonthlyHouseholdIncome == null || app.HouseholdMemberCount == null || string.IsNullOrWhiteSpace(app.EmploymentStatusCode)))) missing.Add("income");
        var docsOk = await DocumentsComplete(id, ct); if (!docsOk) missing.Add("documents");
        if (await db.Set<FasDeclaration>().CountAsync(x => x.FasApplicationId == id && x.IsAccepted, ct) < 2) missing.Add("declarations");

        return new { application = review, missingItems = missing, canSubmit = missing.Count == 0 };
    }

    public async Task<object> Submit(long id, CancellationToken ct)
    {
        var (person, actor) = Identity();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var app = await OwnedDraft(id, person, ct);
            var items = await db.Set<FasApplicationScheme>().Where(x => x.FasApplicationId == id && x.StatusCode == "DRAFT").ToListAsync(ct);
            if (items.Count == 0) throw new InvalidOperationException("FAS.SCHEME_REQUIRED");

            if (app.SchoolOrganizationId == null) throw new InvalidOperationException("FAS.SUBMISSION_INCOMPLETE");
            var schemeIds = items.Select(i => i.FasSchemeId).ToArray();
            var openSchemes = await OpenApplicableSchemeIds(app.SchoolOrganizationId.Value, schemeIds, ct);
            if (openSchemes.Count != schemeIds.Distinct().Count()) throw new InvalidOperationException("FAS.SCHEME_NOT_AVAILABLE");

            await EnsureNoDuplicateApplications(person, items.Select(x => x.FasSchemeId).ToArray(), id, ct);
            if (string.IsNullOrWhiteSpace(app.StudentName) || string.IsNullOrWhiteSpace(app.NricFinMasked) || app.DateOfBirth == null || string.IsNullOrWhiteSpace(app.NationalityCode) || string.IsNullOrWhiteSpace(app.ParentNationalitiesJson) || string.IsNullOrWhiteSpace(app.Mobile) || string.IsNullOrWhiteSpace(app.Address) || app.SchoolOrganizationId == null || string.IsNullOrWhiteSpace(app.StudentNumber) || string.IsNullOrWhiteSpace(app.Email) || app.IsWelfareHomeResident == null || (app.IsWelfareHomeResident == false && (app.MonthlyHouseholdIncome == null || app.HouseholdMemberCount == null || string.IsNullOrWhiteSpace(app.EmploymentStatusCode))) || !await DocumentsComplete(id, ct) || await db.Set<FasDeclaration>().CountAsync(x => x.FasApplicationId == id && x.IsAccepted, ct) < 2)
                throw new InvalidOperationException("FAS.SUBMISSION_INCOMPLETE");

            DateTime now = clock.UtcNow.UtcDateTime;
            app.SubmitDraft(actor, now);
            db.Add(FasStatusHistory.Create(id, null, FasApplicationStatuses.Draft, FasApplicationStatuses.Submitted, "Application submitted", actor, "STUDENT", now));
            foreach (var item in items)
            {
                item.Submit();
                db.Add(FasStatusHistory.Create(id, item.Id, "DRAFT", "PENDING", "Submitted for review", actor, "STUDENT", now));
            }

            await RecordFasApplicationAuditAsync(
                AuditActionCodes.FasApplicationSubmitted,
                app,
                "FAS application submitted by student",
                FasApplicationStatuses.Submitted,
                ct,
                beforeStatus: FasApplicationStatuses.Draft,
                count: items.Count);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            await NotifySubmissionAsync(app.Id, ct);
            return await ApplicationReview(id, ct);
        });
    }

    public async Task<object> Withdraw(long id, CancellationToken ct)
    {
        var (person, actor) = Identity();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var app = await OwnedSubmitted(id, person, ct);
            var items = await db.Set<FasApplicationScheme>()
                .Where(x => x.FasApplicationId == id)
                .ToListAsync(ct);

            if (items.Count == 0 || items.Any(x => x.StatusCode != "PENDING"))
            {
                throw new InvalidOperationException("FAS.WITHDRAW_PENDING_ONLY");
            }

            string previousStatus = app.StatusCode;
            var now = clock.UtcNow.UtcDateTime;
            app.Withdraw(actor, now);
            db.Add(FasStatusHistory.Create(id, null, previousStatus, FasApplicationStatuses.Withdrawn, "Application withdrawn by student", actor, "STUDENT", now));

            foreach (var item in items)
            {
                item.Withdraw();
                db.Add(FasStatusHistory.Create(id, item.Id, "PENDING", "CANCELLED", "Withdrawn by student", actor, "STUDENT", now));
            }

            await RecordFasApplicationAuditAsync(
                AuditActionCodes.FasApplicationWithdrawn,
                app,
                "FAS application withdrawn by student",
                FasApplicationStatuses.Withdrawn,
                ct,
                beforeStatus: previousStatus,
                count: items.Count);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await ApplicationReview(id, ct);
        });
    }

    public async Task<object> WithdrawScheme(long applicationSchemeId, CancellationToken ct)
    {
        var (person, actor) = Identity();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var target = await (from item in db.Set<FasApplicationScheme>()
                                join app in db.Set<FasApplication>() on item.FasApplicationId equals app.Id
                                where item.Id == applicationSchemeId && app.StudentPersonId == person
                                select new { Application = app, Scheme = item })
                .SingleOrDefaultAsync(ct)
                ?? throw new KeyNotFoundException("FAS.APPLICATION_SCHEME_NOT_FOUND");

            if (target.Application.StatusCode is not (FasApplicationStatuses.Draft or FasApplicationStatuses.Submitted or FasApplicationStatuses.PendingReview) ||
                target.Scheme.StatusCode is not ("DRAFT" or "PENDING"))
            {
                throw new InvalidOperationException("FAS.WITHDRAW_PENDING_ONLY");
            }

            DateTime now = clock.UtcNow.UtcDateTime;
            string previousSchemeStatus = target.Scheme.StatusCode;
            target.Scheme.Withdraw();
            db.Add(FasStatusHistory.Create(
                target.Application.Id,
                target.Scheme.Id,
                previousSchemeStatus,
                "CANCELLED",
                "Scheme withdrawn by student",
                actor,
                "STUDENT",
                now));

            bool hasRemainingActiveScheme = await db.Set<FasApplicationScheme>()
                .AnyAsync(
                    x => x.FasApplicationId == target.Application.Id &&
                         x.Id != target.Scheme.Id &&
                         x.StatusCode != "CANCELLED",
                    ct);
            if (!hasRemainingActiveScheme)
            {
                string previousApplicationStatus = target.Application.StatusCode;
                target.Application.Withdraw(actor, now);
                db.Add(FasStatusHistory.Create(
                    target.Application.Id,
                    null,
                    previousApplicationStatus,
                    FasApplicationStatuses.Withdrawn,
                    "All schemes withdrawn by student",
                    actor,
                    "STUDENT",
                    now));
            }

            await RecordFasApplicationSchemeAuditAsync(
                AuditActionCodes.FasSchemeSelectionWithdrawn,
                target.Application,
                target.Scheme,
                "FAS scheme selection withdrawn by student",
                previousSchemeStatus,
                "CANCELLED",
                ct);

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await ApplicationReview(target.Application.Id, ct);
        });
    }


    public async Task<object> MyApplications(int page, int pageSize, string? search, string? status, string? sortBy, string? sortDirection, CancellationToken ct)
    {
        var (person, _) = Identity();
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = from a in db.Set<FasApplication>().AsNoTracking()
                    join i in db.Set<FasApplicationScheme>().AsNoTracking() on a.Id equals i.FasApplicationId
                    join s in db.Set<FasScheme>().AsNoTracking() on i.FasSchemeId equals s.Id
                    where a.StudentPersonId == person
                    select new
                    {
                        applicationId = a.Id,
                        applicationSchemeId = i.Id,
                        applicationReference = a.ApplicationNo,
                        schemeId = s.Id,
                        schemeName = s.Name,
                        applicationStatus = a.StatusCode,
                        submittedDate = a.SubmittedAtUtc,
                        itemStatus = i.StatusCode,
                        schemeStatus = s.StatusCode,
                        canReview = true,
                        i.RejectionNotes,
                        i.ApprovedAmount,
                        i.ApprovedComponentsJson,
                        i.IsActive,
                        isReserved = db.Set<FasVoucherRedemption>().Any(r =>
                            r.FasApplicationSchemeId == i.Id && r.StatusCode == "PENDING"),
                        i.ValidFrom,
                        i.ValidTo,
                        redeemedAt = i.RedeemedAtUtc
                    };

        var allRows = await query.ToListAsync(ct);
        var filteredRows = allRows
            .Where(x => MatchesMyApplicationSearch(x.applicationReference, x.schemeName, search))
            .Where(x => MatchesMyApplicationStatus(StudentVisibleStatus(x.itemStatus, x.schemeStatus), status))
            .ToArray();
        var totalCount = filteredRows.LongLength;
        var rows = ApplyMyApplicationSort(filteredRows, sortBy, sortDirection)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var items = rows.Select(x => new
        {
            x.applicationId,
            x.applicationSchemeId,
            x.applicationReference,
            x.schemeId,
            x.schemeName,
            x.applicationStatus,
            x.submittedDate,
            status = StudentVisibleStatus(x.itemStatus, x.schemeStatus),
            applicationSchemeStatus = x.itemStatus,
            x.schemeStatus,
            availabilityStatus = SchemeAvailabilityStatus(x.schemeStatus),
            availabilityMessage = SchemeAvailabilityMessage(x.schemeStatus),
            x.canReview,
            canWithdraw = (x.applicationStatus == FasApplicationStatuses.Submitted || x.applicationStatus == FasApplicationStatuses.PendingReview) &&
                          x.itemStatus == "PENDING" &&
                          SchemeIsAvailable(x.schemeStatus),
            x.RejectionNotes,
            x.ApprovedAmount,
            x.ApprovedComponentsJson,
            x.IsActive,
            x.isReserved,
            x.ValidFrom,
            x.ValidTo,
            x.redeemedAt
        }).ToList();

        return new { items, page, pageSize, totalCount };
    }

    private static bool MatchesMyApplicationSearch(string? applicationReference, string? schemeName, string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return true;

        string value = search.Trim();
        return (applicationReference?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false)
               || (schemeName?.Contains(value, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    private static bool MatchesMyApplicationStatus(string visibleStatus, string? status)
    {
        string normalized = status?.Trim().ToUpperInvariant() ?? "ALL";
        return string.IsNullOrWhiteSpace(normalized) || normalized == "ALL" || string.Equals(visibleStatus, normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<T> ApplyMyApplicationSort<T>(IReadOnlyCollection<T> rows, string? sortBy, string? sortDirection)
    {
        bool descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        string key = sortBy?.Trim().ToLowerInvariant() ?? string.Empty;

        IOrderedEnumerable<T> ordered = key switch
        {
            "applicationreference" => OrderByObject(rows, "applicationReference", descending),
            "schemename" => OrderByObject(rows, "schemeName", descending),
            "submitteddate" => OrderByObject(rows, "submittedDate", descending),
            "status" => OrderByObject(rows, "itemStatus", descending),
            "benefit" => OrderByObject(rows, "ApprovedAmount", descending),
            _ => OrderByObject(rows, "submittedDate", true)
        };

        return ordered.ThenBy(x => ReadObjectProperty(x, "applicationSchemeId"));
    }

    private static IOrderedEnumerable<T> OrderByObject<T>(IEnumerable<T> rows, string propertyName, bool descending)
        => descending
            ? rows.OrderByDescending(x => ReadObjectProperty(x, propertyName))
            : rows.OrderBy(x => ReadObjectProperty(x, propertyName));

    private static object? ReadObjectProperty<T>(T row, string propertyName)
        => row?.GetType().GetProperty(propertyName)?.GetValue(row);

    public async Task<object> ApplicableActiveSchemesForCourse(long courseId, CancellationToken ct)
    {
        if (courseId <= 0) throw new ArgumentException("FAS.COURSE_REQUIRED");
        var (person, _) = Identity();
        var profile = await Profile(ct);
        var today = Today();
        var courseExists = await db.Set<Course>()
            .AsNoTracking()
            .AnyAsync(x => x.Id == courseId && x.OrganizationId == profile.SchoolOrganizationId, ct);
        if (!courseExists) throw new KeyNotFoundException("COURSE.NOT_FOUND");

        var rows = await (
                from active in db.Set<FasActiveScheme>().AsNoTracking()
                join item in db.Set<FasApplicationScheme>().AsNoTracking()
                    on active.FasApplicationSchemeId equals item.Id
                join scheme in db.Set<FasScheme>().AsNoTracking()
                    on active.FasSchemeId equals scheme.Id
                where active.StudentPersonId == person
                      && active.StatusCode == "ACTIVE"
                      && item.IsActive
                      && scheme.StatusCode == "ACTIVE"
                      && active.ActiveFrom <= today
                      && active.ActiveTo >= today
                      && !db.Set<FasVoucherRedemption>().Any(redemption =>
                          redemption.FasApplicationSchemeId == item.Id &&
                          redemption.StatusCode == "PENDING")
                      && (!db.Set<FasSchemeCourse>().Any(schemeCourse =>
                              schemeCourse.FasSchemeId == scheme.Id) ||
                          db.Set<FasSchemeCourse>().Any(schemeCourse =>
                              schemeCourse.FasSchemeId == scheme.Id &&
                              schemeCourse.CourseId == courseId))
                orderby scheme.Name, item.Id
                select new
                {
                    applicationSchemeId = item.Id,
                    schemeId = scheme.Id,
                    schemeName = scheme.Name,
                    approvedAmount = item.ApprovedAmount,
                    approvedComponentsJson = item.ApprovedComponentsJson,
                    validFrom = active.ActiveFrom,
                    validTo = active.ActiveTo,
                    appliesToAllCourses = !db.Set<FasSchemeCourse>().Any(schemeCourse =>
                        schemeCourse.FasSchemeId == scheme.Id)
                })
            .ToListAsync(ct);

        return rows;
    }

    public async Task<object> Summary(CancellationToken ct)
    {
        var (person, _) = Identity();
        long? draft = await db.Set<FasApplication>()
            .AsNoTracking()
            .Where(x => x.StudentPersonId == person && x.StatusCode == FasApplicationStatuses.Draft)
            .Select(x => (long?)x.Id)
            .FirstOrDefaultAsync(ct);

        var active = await db.Set<FasActiveScheme>()
            .AsNoTracking()
            .Where(x => x.StudentPersonId == person && x.StatusCode == "ACTIVE")
            .Select(x => new { x.FasApplicationSchemeId, x.FasSchemeId, x.ActiveFrom, x.ActiveTo })
            .FirstOrDefaultAsync(ct);

        return new { canApply = true, blockingReason = (string?)null, activeScheme = active, resumableDraftId = draft };
    }

    public async Task<object> ApplicationReview(long id, CancellationToken ct)
    {
        var (person, _) = Identity();
        var app = await Owned(id, person, ct);
        var schemeRows = await (from i in db.Set<FasApplicationScheme>().AsNoTracking()
                                join s in db.Set<FasScheme>().AsNoTracking() on i.FasSchemeId equals s.Id
                                where i.FasApplicationId == id
                                select new
                                {
                                    i.Id,
                                    i.FasSchemeId,
                                    s.Name,
                                    itemStatus = i.StatusCode,
                                    schemeStatus = s.StatusCode,
                                    i.ApprovedAmount,
                                    i.ApprovedComponentsJson,
                                    i.RejectionNotes,
                                    i.ValidFrom,
                                    i.ValidTo,
                                    i.IsActive,
                                    i.RedeemedAtUtc
                                }).ToListAsync(ct);
        var schemes = schemeRows.Select(x => new
        {
            x.Id,
            x.FasSchemeId,
            x.Name,
            StatusCode = StudentVisibleStatus(x.itemStatus, x.schemeStatus),
            applicationSchemeStatus = x.itemStatus,
            x.schemeStatus,
            availabilityStatus = SchemeAvailabilityStatus(x.schemeStatus),
            availabilityMessage = SchemeAvailabilityMessage(x.schemeStatus),
            x.ApprovedAmount,
            x.ApprovedComponentsJson,
            x.RejectionNotes,
            x.ValidFrom,
            x.ValidTo,
            x.IsActive,
            x.RedeemedAtUtc
        }).ToList();
        var documents = await db.Set<FasDocument>().AsNoTracking()
            .Where(x => x.FasApplicationId == id && x.UploadStatusCode != "REMOVED")
            .OrderBy(x => x.ChecklistItemCode).ThenByDescending(x => x.UploadedAtUtc)
            .Select(x => new { x.Id, x.ChecklistItemCode, x.FileName, x.MimeType, x.FileSizeBytes, x.UploadStatusCode, x.UploadedAtUtc })
            .ToListAsync(ct);
        var declarations = await db.Set<FasDeclaration>().AsNoTracking()
            .Where(x => x.FasApplicationId == id)
            .OrderBy(x => x.DeclarationTypeCode)
            .Select(x => new { x.DeclarationTypeCode, x.IsAccepted, x.AcceptedAtUtc })
            .ToListAsync(ct);

        return new
        {
            app.Id,
            applicationReference = app.ApplicationNo,
            app.StatusCode,
            canWithdraw = (app.StatusCode == FasApplicationStatuses.Submitted || app.StatusCode == FasApplicationStatuses.PendingReview) &&
                          schemeRows.Count > 0 &&
                          schemeRows.All(x => x.itemStatus == "PENDING" && SchemeIsAvailable(x.schemeStatus)),
            app.StudentName,
            app.NricFinMasked,
            app.DateOfBirth,
            app.NationalityCode,
            parentNationalities = ParseParentNationalities(app.ParentNationalitiesJson),
            app.AccountTypeCode,
            app.Mobile,
            app.Address,
            app.Email,
            currentSchool = new { id = app.SchoolOrganizationId, name = app.SchoolName, app.StudentNumber },
            income = new { app.IsWelfareHomeResident, app.EmploymentStatusCode, app.MonthlyHouseholdIncome, app.HouseholdMemberCount, app.OtherMonthlyIncome, app.PerCapitaIncome },
            app.SubmittedAtUtc,
            schemes,
            documents,
            declarations
        };
    }

    private async Task<bool> DocumentsComplete(long id, CancellationToken ct)
    {
        var app = await db.Set<FasApplication>().SingleAsync(x => x.Id == id, ct);
        return (await BuildChecklist(app, ct)).All(x => x.IsComplete);
    }

    private static long[] ValidateSelectedSchemeIds(IReadOnlyCollection<long> schemeIds)
    {
        if (schemeIds.Count != schemeIds.Distinct().Count()) throw new InvalidOperationException("FAS.DUPLICATE_SCHEME_SELECTION");
        long[] ids = schemeIds.Distinct().ToArray();
        if (ids.Length == 0) throw new ArgumentException("FAS.SCHEME_REQUIRED");
        return ids;
    }

    private async Task<List<long>> OpenApplicableSchemeIds(long schoolId, IReadOnlyCollection<long> schemeIds, CancellationToken ct)
    {
        var today = Today();
        var applicable = await ApplicableSchemeIds(schoolId, ct);
        return await db.Set<FasScheme>()
            .AsNoTracking()
            .Where(x => schemeIds.Contains(x.Id) &&
                        x.StatusCode == "ACTIVE" &&
                        x.StartDate <= today &&
                        x.EndDate >= today &&
                        applicable.Contains(x.Id))
            .Select(x => x.Id)
            .ToListAsync(ct);
    }

    private Task<List<long>> ApplicableSchemeIds(long schoolId, CancellationToken ct)
    {
        IQueryable<long> schoolCourseIds = db.Set<Course>()
            .AsNoTracking()
            .Where(course => course.OrganizationId == schoolId)
            .Select(course => course.Id);

        return db.Set<FasScheme>()
            .AsNoTracking()
            .Where(scheme =>
                !db.Set<FasSchemeCourse>().Any(schemeCourse => schemeCourse.FasSchemeId == scheme.Id) ||
                db.Set<FasSchemeCourse>().Any(schemeCourse =>
                    schemeCourse.FasSchemeId == scheme.Id &&
                    schoolCourseIds.Contains(schemeCourse.CourseId)))
            .Select(scheme => scheme.Id)
            .ToListAsync(ct);
    }

    private static bool SchemeIsAvailable(string schemeStatus) => schemeStatus is "ACTIVE";
    private static bool IsIncomeCriterion(string criteriaType) => criteriaType is "GDP" or "GHI" or "PCI";
    private static string SchemeAvailabilityStatus(string schemeStatus) => SchemeIsAvailable(schemeStatus) ? "AVAILABLE" : "NOT_AVAILABLE";
    private static string? SchemeAvailabilityMessage(string schemeStatus) => SchemeIsAvailable(schemeStatus) ? null : "This FAS scheme is no longer available.";
    private static string StudentVisibleStatus(string itemStatus, string schemeStatus)
    {
        if (string.Equals(itemStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase)) return "WITHDRAWN";
        return !SchemeIsAvailable(schemeStatus) && itemStatus is "DRAFT" or "PENDING" ? "NOT_AVAILABLE" : itemStatus;
    }

    private static string ToAdminVisibleStatus(string applicationStatus, string selectionStatus)
    {
        if (string.Equals(applicationStatus, FasApplicationStatuses.Withdrawn, StringComparison.OrdinalIgnoreCase)) return "WITHDRAWN";
        return string.Equals(selectionStatus, "CANCELLED", StringComparison.OrdinalIgnoreCase) ? "WITHDRAWN" : selectionStatus;
    }

    private static bool MatchesAdminApplicationStatus(string visibleStatus, string? status)
    {
        string normalized = status?.Trim().ToUpperInvariant() ?? "ALL";
        return string.IsNullOrWhiteSpace(normalized) || normalized == "ALL" || string.Equals(visibleStatus, normalized, StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<T> ApplyAdminApplicationSort<T>(IReadOnlyCollection<T> rows, string? sortBy, string? sortDirection)
    {
        bool descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase) || string.IsNullOrWhiteSpace(sortDirection);
        string key = sortBy?.Trim() ?? string.Empty;

        IOrderedEnumerable<T> ordered = key switch
        {
            "applicationId" => AdminOrderByObject(rows, "application.ApplicationNo", descending),
            "studentName" => AdminOrderByObject(rows, "application.StudentName", descending),
            "schemeName" => AdminOrderByObject(rows, "scheme.Name", descending),
            "status" => AdminOrderByObject(rows, "selection.StatusCode", descending),
            "submittedDate" or _ => AdminOrderByObject(rows, "application.SubmittedAtUtc", descending)
        };

        return ordered.ThenByDescending(x => AdminReadObjectProperty(x, "application.Id"));
    }

    private static IOrderedEnumerable<T> AdminOrderByObject<T>(IEnumerable<T> rows, string propertyPath, bool descending)
        => descending
            ? rows.OrderByDescending(x => AdminReadObjectProperty(x, propertyPath))
            : rows.OrderBy(x => AdminReadObjectProperty(x, propertyPath));

    private static object? AdminReadObjectProperty<T>(T row, string propertyPath)
    {
        object? current = row;
        foreach (string segment in propertyPath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            current = current?.GetType().GetProperty(segment)?.GetValue(current);
        }

        return current;
    }

    private async Task RecordFasApplicationAuditAsync(
        string actionCode,
        FasApplication app,
        string summary,
        string afterStatus,
        CancellationToken ct,
        string? beforeStatus = null,
        int? count = null)
    {
        if (app.SchoolOrganizationId is not long schoolOrganizationId)
        {
            return;
        }

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                actionCode,
                "FasApplication",
                app.Id,
                schoolOrganizationId,
                new SchoolAuditDetails(
                    summary,
                    EntityDisplayName: app.ApplicationNo,
                    RelatedIds: new Dictionary<string, long>
                    {
                        ["studentPersonId"] = app.StudentPersonId,
                        ["applicationId"] = app.Id
                    },
                    StatusTransition: new SchoolAuditStatusTransition(beforeStatus, afterStatus),
                    Count: count)),
            ct);
    }

    private async Task RecordFasApplicationSchemeAuditAsync(
        string actionCode,
        FasApplication app,
        FasApplicationScheme item,
        string summary,
        string beforeStatus,
        string afterStatus,
        CancellationToken ct)
    {
        if (app.SchoolOrganizationId is not long schoolOrganizationId)
        {
            return;
        }

        await audit.RecordSchoolActionAsync(
            new SchoolAuditContext(
                actionCode,
                "FasApplicationScheme",
                item.Id,
                schoolOrganizationId,
                new SchoolAuditDetails(
                    summary,
                    EntityDisplayName: app.ApplicationNo,
                    RelatedIds: new Dictionary<string, long>
                    {
                        ["studentPersonId"] = app.StudentPersonId,
                        ["applicationId"] = app.Id,
                        ["schemeId"] = item.FasSchemeId
                    },
                    StatusTransition: new SchoolAuditStatusTransition(beforeStatus, afterStatus))),
            ct);
    }

    private async Task EnsureNoDuplicateApplications(long person, IReadOnlyCollection<long> schemeIds, long? currentApplicationId, CancellationToken ct)
    {
        var pendingSchemes = await (
                from item in db.Set<FasApplicationScheme>().AsNoTracking()
                join application in db.Set<FasApplication>().AsNoTracking() on item.FasApplicationId equals application.Id
                join scheme in db.Set<FasScheme>().AsNoTracking() on item.FasSchemeId equals scheme.Id
                where application.StudentPersonId == person
                      && (!currentApplicationId.HasValue || application.Id != currentApplicationId.Value)
                      && application.StatusCode != FasApplicationStatuses.Withdrawn
                      && schemeIds.Contains(item.FasSchemeId)
                      && item.StatusCode == "PENDING"
                select new { item.FasSchemeId, scheme.Name })
            .Distinct()
            .ToListAsync(ct);

        if (pendingSchemes.Count > 0)
        {
            var pendingSchemeLabels = pendingSchemes
                .Select(x => $"{x.FasSchemeId}:{x.Name.Replace("|", " ").Replace(",", " ")}");
            throw new InvalidOperationException($"FAS.PENDING_SCHEME_APPLICATION:{string.Join('|', pendingSchemeLabels)}");
        }
    }
    private async Task<FasApplication> Owned(long id, long person, CancellationToken ct) => await db.Set<FasApplication>().SingleOrDefaultAsync(x => x.Id == id && x.StudentPersonId == person, ct) ?? throw new KeyNotFoundException("FAS.APPLICATION_NOT_FOUND");
    private async Task<FasApplication> OwnedDraft(long id, long person, CancellationToken ct) { var a = await Owned(id, person, ct); if (a.StatusCode != FasApplicationStatuses.Draft) throw new InvalidOperationException("FAS.APPLICATION_LOCKED"); return a; }
    private async Task<FasApplication> OwnedSubmitted(long id, long person, CancellationToken ct) { var a = await Owned(id, person, ct); if (a.StatusCode != FasApplicationStatuses.Submitted && a.StatusCode != FasApplicationStatuses.PendingReview) throw new InvalidOperationException("FAS.WITHDRAW_PENDING_ONLY"); return a; }
    private async Task<FasApplication> OwnedEditable(long id, long person, CancellationToken ct)
    {
        var application = await Owned(id, person, ct);
        if (application.StatusCode is not (FasApplicationStatuses.Draft or FasApplicationStatuses.Submitted or FasApplicationStatuses.PendingReview))
        {
            throw new InvalidOperationException("FAS.APPLICATION_LOCKED");
        }

        return application;
    }
    private sealed record ProfileRow(long PersonId, string Name, string? NricFinMasked, DateOnly DateOfBirth, string NationalityCode, string? Mobile, string? Address, string? Email, long SchoolOrganizationId, string SchoolName, string StudentNumber);

    private async Task<string> ResolveAccountType(long personId, CancellationToken ct)
    {
        bool hasEducationAccount = await db.Set<EducationAccount>()
            .AsNoTracking()
            .AnyAsync(x => x.PersonId == personId, ct);
        return hasEducationAccount ? "EDUCATION_ACCOUNT" : "PERSONAL_ACCOUNT";
    }
    private static bool TierMatches(long tierId, FasApplication app, IReadOnlyCollection<FasTierCriteriaGroup> allGroups, IReadOnlyCollection<FasTierCriteria> allCriteria, IReadOnlyCollection<FasTierCriteriaNationality> allValues, DateOnly today)
    {
        var criteria = allCriteria
            .Where(x => x.FasTierId == tierId)
            .OrderBy(x => x.DisplayOrder)
            .ToArray();
        if (criteria.Length == 0) return true;

        int? age = app.DateOfBirth.HasValue ? today.Year - app.DateOfBirth.Value.Year : null;
        if (age.HasValue && app.DateOfBirth!.Value.AddYears(age.Value) > today) age--;

        var parents = ParseParentNationalities(app.ParentNationalitiesJson);
        bool Match(FasTierCriteria c)
        {
            var values = allValues.Where(x => x.FasTierCriteriaId == c.Id).Select(x => x.Nationality).ToArray();
            return c.CriteriaType switch
            {
                "AGE" => age.HasValue && age.Value >= c.NumberFrom && age.Value <= c.NumberTo,
                "GDP" or "GHI" => app.MonthlyHouseholdIncome.HasValue && app.MonthlyHouseholdIncome.Value >= c.NumberFrom && app.MonthlyHouseholdIncome.Value <= c.NumberTo,
                "PCI" => app.PerCapitaIncome.HasValue && app.PerCapitaIncome.Value >= c.NumberFrom && app.PerCapitaIncome.Value <= c.NumberTo,
                "NATIONALITY" => values.Contains(app.NationalityCode ?? string.Empty, StringComparer.OrdinalIgnoreCase) ||
                                 app.NationalityCode == "SG" && values.Contains("Singapore Citizen", StringComparer.OrdinalIgnoreCase),
                "PARENT_NATIONALITY" => parents.Any(p => values.Contains(p, StringComparer.OrdinalIgnoreCase)),
                "ACCOUNT_TYPE" => values.Contains(app.AccountTypeCode, StringComparer.OrdinalIgnoreCase),
                _ => false
            };
        }

        var values = criteria.ToDictionary(criteriaItem => criteriaItem.Id, Match);
        FasTierCriteriaGroup[] groups = allGroups
            .Where(x => x.FasTierId == tierId)
            .OrderBy(x => x.DisplayOrder)
            .ToArray();

        return GroupsMatch(groups, criteria, values);
    }

    private static bool GroupsMatch(
        IReadOnlyCollection<FasTierCriteriaGroup> groups,
        IReadOnlyCollection<FasTierCriteria> criteria,
        IReadOnlyDictionary<long, bool> values)
    {
        if (groups.Count == 0)
        {
            return LegacyGroups(criteria).Any(group => group.All(criteriaItem => values.GetValueOrDefault(criteriaItem.Id)));
        }

        return groups
            .OrderBy(group => group.DisplayOrder)
            .Any(group =>
            {
                FasTierCriteria[] groupCriteria = criteria
                    .Where(criteriaItem => criteriaItem.FasTierCriteriaGroupId == group.Id)
                    .ToArray();
                return groupCriteria.Length > 0 && groupCriteria.All(criteriaItem => values.GetValueOrDefault(criteriaItem.Id));
            });
    }

    private static IReadOnlyList<IReadOnlyList<FasTierCriteria>> LegacyGroups(IReadOnlyCollection<FasTierCriteria> criteria)
    {
        var groups = new List<IReadOnlyList<FasTierCriteria>>();
        var current = new List<FasTierCriteria>();

        foreach (FasTierCriteria item in criteria.OrderBy(item => item.DisplayOrder))
        {
            current.Add(item);
            if (item.ConnectorToNext != "OR")
            {
                continue;
            }

            groups.Add(current.ToArray());
            current = new List<FasTierCriteria>();
        }

        if (current.Count > 0)
        {
            groups.Add(current.ToArray());
        }

        return groups;
    }
    private static string[] ParseParentNationalities(string? json) => string.IsNullOrWhiteSpace(json) ? Array.Empty<string>() : JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    private static string? ExtractApprovedTierLabel(string? approvedComponentsJson)
    {
        if (string.IsNullOrWhiteSpace(approvedComponentsJson)) return null;
        try
        {
            using JsonDocument document = JsonDocument.Parse(approvedComponentsJson);
            return document.RootElement.TryGetProperty("tierLabel", out JsonElement label) && label.ValueKind == JsonValueKind.String
                ? label.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
    private async Task NotifySubmissionAsync(long applicationId, CancellationToken ct)
    {
        FasApplication? app = await db.Set<FasApplication>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicationId, ct);
        if (app is null) return;

        if (app.SchoolOrganizationId is null) return;

        IReadOnlyCollection<long> userAccountIds = await schoolAdminRecipients.FindUserAccountIdsByOrganizationIdAsync(
            app.SchoolOrganizationId.Value,
            ct);
        if (userAccountIds.Count == 0) return;

        foreach (long userAccountId in userAccountIds.Distinct())
        {
            await notificationWriter.CreateForBusinessFlowAsync(
                new NotificationCreateRequest(
                    userAccountId,
                    NotificationTypeCode.FasSubmitted,
                    $"FAS Application Submitted: {app.ApplicationNo}",
                    $"New FAS application {app.ApplicationNo} for {app.SchoolName} was submitted."),
                logger,
                "FAS application submitted school admin notification",
                ct);
        }
    }

    private async Task NotifyStudentForApprovedSchemeAsync(long applicationId, string tierName, CancellationToken ct)
    {
        FasApplication? app = await db.Set<FasApplication>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicationId, ct);
        if (app is null) return;

        FasScheme? scheme = await db.Set<FasScheme>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == app.FasSchemeId, ct);
        string schemeName = scheme?.Name ?? "FAS Scheme";

        long? userAccountId = await studentNotificationRecipients.FindUserAccountIdByPersonIdAsync(app.AccountHolderPersonId, ct);
        if (userAccountId is null) return;

        await notificationWriter.CreateForBusinessFlowAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.FasEligible,
                $"FAS Application Approved: {app.ApplicationNo}",
                $"Your FAS application {app.ApplicationNo} for {schemeName} was approved. You qualify for {tierName}."),
            logger,
            "FAS application approved student notification",
            ct);
    }

    private async Task NotifyStudentForRejectedSchemeAsync(long applicationId, string rejectionNotes, CancellationToken ct)
    {
        FasApplication? app = await db.Set<FasApplication>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == applicationId, ct);
        if (app is null) return;

        FasScheme? scheme = await db.Set<FasScheme>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == app.FasSchemeId, ct);
        string schemeName = scheme?.Name ?? "FAS Scheme";

        long? userAccountId = await studentNotificationRecipients.FindUserAccountIdByPersonIdAsync(app.AccountHolderPersonId, ct);
        if (userAccountId is null) return;

        string reason = string.IsNullOrWhiteSpace(rejectionNotes) ? "No rejection reason was provided." : rejectionNotes.Trim();
        await notificationWriter.CreateForBusinessFlowAsync(
            new NotificationCreateRequest(
                userAccountId.Value,
                NotificationTypeCode.FasRejected,
                $"FAS Application Rejected: {app.ApplicationNo}",
                $"Your FAS application {app.ApplicationNo} for {schemeName} was rejected. Reason: {reason}."),
            logger,
            "FAS application rejected student notification",
            ct);
    }
    private sealed record CourseRow(long Id, string CourseCode, string CourseName);
    private sealed record ChecklistItem(string ChecklistItemCode, string Label, bool IsMandatory, object? Document, bool IsComplete);
}
