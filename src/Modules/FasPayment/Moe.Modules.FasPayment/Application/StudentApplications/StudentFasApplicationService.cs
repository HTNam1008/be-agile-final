using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moe.Application.Abstractions.Security;
using Moe.Modules.FasPayment.Domain.Fas;
using Moe.Modules.FasPayment.Infrastructure.Documents;
using Moe.Modules.IdentityPlatform.Domain.People;
using Moe.Modules.IdentityPlatform.Domain.Schooling;
using Moe.StudentFinance.Persistence;

namespace Moe.Modules.FasPayment.Application.StudentApplications;

public sealed class StudentFasApplicationService(MoeDbContext db, ICurrentUser currentUser, IFasDocumentStorage storage, IFasDocumentScanner scanner)
{
    private (long PersonId, long ActorId) Identity() =>
        (currentUser.PersonId ?? throw new UnauthorizedAccessException("FAS.AUTHENTICATION_REQUIRED"),
         currentUser.UserAccountId ?? throw new UnauthorizedAccessException("FAS.AUTHENTICATION_REQUIRED"));
    private long Actor() => currentUser.UserAccountId ?? throw new UnauthorizedAccessException("FAS.AUTHENTICATION_REQUIRED");

    private async Task<ProfileRow> Profile(CancellationToken ct)
    {
        var (personId, _) = Identity();
        var person = await db.Set<Person>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == personId, ct)
            ?? throw new KeyNotFoundException("FAS.PROFILE_REQUIRED");
        var enrollment = await db.Set<SchoolEnrollment>().AsNoTracking()
            .Where(x => x.PersonId == personId && x.SchoolingStatusCode == "ACTIVE" &&
                        x.StartDate <= DateOnly.FromDateTime(DateTime.UtcNow) &&
                        (x.EndDate == null || x.EndDate >= DateOnly.FromDateTime(DateTime.UtcNow)))
            .OrderByDescending(x => x.StartDate).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("FAS.CURRENT_SCHOOL_REQUIRED");
        var schoolName = IsSqlServer()
            ? await db.Database.SqlQuery<string>($"SELECT OrganizationName AS Value FROM org.Organization WHERE OrganizationId = {enrollment.OrganizationId}").FirstOrDefaultAsync(ct)
            : null;
        schoolName ??= $"School {enrollment.OrganizationId}";
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

    public async Task<object> ListSchemes(CancellationToken ct)
    {
        var p = await Profile(ct); var today = DateOnly.FromDateTime(DateTime.UtcNow); var applicable = await ApplicableSchemeIds(p.SchoolOrganizationId, ct);
        var schemes = await db.Set<FasScheme>().AsNoTracking().Where(x => x.StatusCode == "ACTIVE" && applicable.Contains(x.Id))
            .OrderBy(x => x.StartDate).Select(x => new
            {
                id = x.Id,
                x.Name,
                shortDescription = x.Description,
                applicationStartDate = x.StartDate,
                applicationEndDate = x.EndDate,
                isOpenForApplication = x.StartDate <= today && x.EndDate >= today
            }).ToListAsync(ct);
        return new { currentSchool = new { id = p.SchoolOrganizationId, name = p.SchoolName }, items = schemes };
    }

    public async Task<object> SchemeDetail(long id, CancellationToken ct)
    {
        var profile = await Profile(ct); var today = DateOnly.FromDateTime(DateTime.UtcNow); var applicable = await ApplicableSchemeIds(profile.SchoolOrganizationId, ct);
        var scheme = await db.Set<FasScheme>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.StatusCode == "ACTIVE", ct)
            ?? throw new KeyNotFoundException("FAS.SCHEME_NOT_FOUND");
        var tiers = await db.Set<FasTier>().AsNoTracking().Where(x => x.FasSchemeId == id).OrderBy(x => x.DisplayOrder)
            .Select(x => new { id = x.Id, x.Label, subsidyType = x.SubsidyType, subsidyValue = x.SubsidyValue, x.DisplayOrder }).ToListAsync(ct);
        var courses = IsSqlServer()
            ? await db.Database.SqlQuery<CourseRow>($"""
                SELECT c.CourseId AS Id, c.CourseCode, c.CourseName
                FROM fas.FASSchemeCourse sc JOIN course.Course c ON c.CourseId=sc.CourseId
                WHERE sc.FASSchemeId={id} ORDER BY c.CourseName
                """).ToListAsync(ct)
            : [];
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
        var age = DateTime.UtcNow.Year - p.DateOfBirth.Year;
        var applicable = await ApplicableSchemeIds(p.SchoolOrganizationId, ct); var schemes = await db.Set<FasScheme>().AsNoTracking().Where(x => x.StatusCode == "ACTIVE" && applicable.Contains(x.Id)).ToListAsync(ct);
        var matches = new List<object>();
        foreach (var scheme in schemes)
        {
            var tiers = await db.Set<FasTier>().AsNoTracking().Where(x => x.FasSchemeId == scheme.Id).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
            foreach (var tier in tiers)
            {
                var criteria = await db.Set<FasTierCriteria>().AsNoTracking().Where(x => x.FasTierId == tier.Id).OrderBy(x => x.DisplayOrder).ToListAsync(ct);
                var values = new List<(bool Match, string? Connector)>();
                foreach (var c in criteria)
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
                    values.Add((ok, c.ConnectorToNext));
                }
                var matched = values.Count == 0 || values.Select((v, i) => i == 0 ? v.Match :
                    (values[i - 1].Connector == "OR" ? v.Match : v.Match)).Aggregate(values.Count > 0 && values[0].Match,
                    (acc, next) => acc); // replaced below by exact ordered evaluation
                if (values.Count > 0) { matched = values[0].Match; for (var i = 1; i < values.Count; i++) matched = values[i - 1].Connector == "OR" ? matched || values[i].Match : matched && values[i].Match; }
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
        if (request.SchemeIds.Count != request.SchemeIds.Distinct().Count()) throw new InvalidOperationException("FAS.DUPLICATE_SCHEME_SELECTION");
        var ids = request.SchemeIds.Distinct().ToArray(); if (ids.Length == 0) throw new ArgumentException("FAS.SCHEME_REQUIRED");
        var p = await Profile(ct); var (personId, actorId) = Identity(); var now = DateTime.UtcNow;
        var applicable = await ApplicableSchemeIds(p.SchoolOrganizationId, ct); var valid = await db.Set<FasScheme>().Where(x => ids.Contains(x.Id) && x.StatusCode == "ACTIVE" && applicable.Contains(x.Id)).Select(x => x.Id).ToListAsync(ct);
        if (valid.Count != ids.Length) throw new InvalidOperationException("FAS.SCHEME_NOT_AVAILABLE");
        var app = await db.Set<FasApplication>().SingleOrDefaultAsync(x => x.StudentPersonId == personId && x.StatusCode == "DRAFT", ct);
        await EnsureNoDuplicateApplications(personId, ids, app?.Id, ct);
        var created = app == null;
        if (app == null)
        {
            var accountType = await ResolveAccountType(personId, ct);
            app = FasApplication.CreateDraft($"FAS-{now:yyyyMMddHHmmss}-{personId}", personId, ids[0], p.StudentNumber,
                p.Name, p.NricFinMasked, p.DateOfBirth, p.NationalityCode, p.Mobile, p.Address, p.Email,
                p.SchoolOrganizationId, p.SchoolName, accountType, actorId, now);
            db.Add(app); await db.SaveChangesAsync(ct);
            db.Add(FasStatusHistory.Create(app.Id, null, null, "DRAFT", "Application draft created", actorId, "STUDENT", now));
        }
        var added = await ReplaceSchemesCore(app, ids, actorId, now, ct); await db.SaveChangesAsync(ct);
        foreach (var item in added) db.Add(FasStatusHistory.Create(app.Id, item.Id, null, "DRAFT", "Scheme selected", actorId, "STUDENT", now));
        if (created || added.Count > 0) await db.SaveChangesAsync(ct);
        return await ApplicationReview(app.Id, ct);
    }

    public async Task<object> ReplaceSchemes(long appId, ReplaceSchemesRequest request, CancellationToken ct)
    {
        var (personId, actorId) = Identity(); var app = await OwnedDraft(appId, personId, ct); var profile = await Profile(ct);
        if (request.SchemeIds.Count != request.SchemeIds.Distinct().Count()) throw new InvalidOperationException("FAS.DUPLICATE_SCHEME_SELECTION");
        var ids = request.SchemeIds.Distinct().ToArray(); if (ids.Length == 0) throw new ArgumentException("FAS.SCHEME_REQUIRED");
        var applicable = await ApplicableSchemeIds(profile.SchoolOrganizationId, ct); var count = await db.Set<FasScheme>().CountAsync(x => ids.Contains(x.Id) && x.StatusCode == "ACTIVE" && applicable.Contains(x.Id), ct);
        if (count != ids.Length) throw new InvalidOperationException("FAS.SCHEME_NOT_AVAILABLE");
        await EnsureNoDuplicateApplications(personId, ids, app.Id, ct); var now = DateTime.UtcNow; var added = await ReplaceSchemesCore(app, ids, actorId, now, ct); await db.SaveChangesAsync(ct); foreach (var item in added) db.Add(FasStatusHistory.Create(app.Id, item.Id, null, "DRAFT", "Scheme selected", actorId, "STUDENT", now)); if (added.Count > 0) await db.SaveChangesAsync(ct); return await ApplicationReview(app.Id, ct);
    }

    private async Task<List<FasApplicationScheme>> ReplaceSchemesCore(FasApplication app, long[] ids, long actor, DateTime now, CancellationToken ct)
    {
        var old = await db.Set<FasApplicationScheme>().Where(x => x.FasApplicationId == app.Id && x.StatusCode == "DRAFT").ToListAsync(ct);
        db.RemoveRange(old.Where(x => !ids.Contains(x.FasSchemeId)));
        var added = ids.Where(id => old.All(x => x.FasSchemeId != id)).Select(id => FasApplicationScheme.CreateDraft(app.Id, id, actor, now)).ToList(); db.AddRange(added);
        app.ReplacePrimaryScheme(ids[0], actor, now);
        return added;
    }

    public async Task<object> UpdateParticulars(long id, UpdateParticularsRequest r, CancellationToken ct)
    { var (person, actor) = Identity(); var app = await OwnedDraft(id, person, ct); var now = DateTime.UtcNow; app.UpdateEmail(r.Email, actor, now); app.UpdateParentNationalities(r.ParentNationalities, actor, now); await db.SaveChangesAsync(ct); return await ApplicationReview(id, ct); }
    public async Task<object> UpdateIncome(long id, UpdateIncomeRequest r, CancellationToken ct)
    { var (person, actor) = Identity(); var app = await OwnedDraft(id, person, ct); app.UpdateIncome(r.IsWelfareHomeResident, r.EmploymentStatusCode, r.MonthlyHouseholdIncome, r.HouseholdMemberCount, r.OtherMonthlyIncome, actor, DateTime.UtcNow); await db.SaveChangesAsync(ct); return await RequiredDocuments(id, ct); }

    public async Task<object> RequiredDocuments(long id, CancellationToken ct)
    {
        var (person, _) = Identity(); var app = await Owned(id, person, ct); return await BuildChecklist(app, ct);
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
        var (person, actor) = Identity(); await OwnedDraft(id, person, ct); if (!r.TrueAndAccurate || !r.AcceptTerms) throw new ArgumentException("FAS.DECLARATIONS_REQUIRED");
        var old = await db.Set<FasDeclaration>().Where(x => x.FasApplicationId == id).ToListAsync(ct); db.RemoveRange(old);
        db.Add(FasDeclaration.Accept(id, "TRUE_AND_ACCURATE", r.TrueAndAccurateText, actor, DateTime.UtcNow, ip, agent));
        db.Add(FasDeclaration.Accept(id, "ACCEPT_TERMS", r.AcceptTermsText, actor, DateTime.UtcNow, ip, agent)); await db.SaveChangesAsync(ct);
        return new { applicationId = id, declarationsComplete = true };
    }

    public async Task<object> UploadDocument(long id, string checklistCode, string fileName, string mime, long size, Stream stream, CancellationToken ct)
    {
        var (person, actor) = Identity(); await OwnedDraft(id, person, ct); ValidateFile(fileName, mime, size);
        var app = await OwnedDraft(id, person, ct); var required = await BuildChecklist(app, ct); if (!required.Any(x => x.ChecklistItemCode == checklistCode)) throw new ArgumentException("FAS.INVALID_CHECKLIST_ITEM");
        var key = await storage.UploadAsync(id, fileName, stream, ct); var doc = FasDocument.Create(id, checklistCode, checklistCode, true, fileName, key, mime, size, actor, DateTime.UtcNow, scanner.RequiresScan);
        db.Add(doc); await db.SaveChangesAsync(ct); return new { id = doc.Id, doc.FileName, doc.MimeType, doc.FileSizeBytes, scanStatus = doc.UploadStatusCode };
    }

    public async Task RemoveDocument(long id, long documentId, CancellationToken ct)
    { var (person, actor) = Identity(); await OwnedDraft(id, person, ct); var d = await db.Set<FasDocument>().SingleOrDefaultAsync(x => x.Id == documentId && x.FasApplicationId == id && x.UploadStatusCode != "REMOVED", ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); d.Remove(actor, DateTime.UtcNow); await db.SaveChangesAsync(ct); await storage.DeleteAsync(d.BlobKey, ct); }

    public async Task<object> ReplaceDocument(long id, long documentId, string fileName, string mime, long size, Stream stream, CancellationToken ct)
    { var (person, actor) = Identity(); await OwnedDraft(id, person, ct); var old = await db.Set<FasDocument>().SingleOrDefaultAsync(x => x.Id == documentId && x.FasApplicationId == id && x.UploadStatusCode != "REMOVED", ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); ValidateFile(fileName, mime, size); var key = await storage.UploadAsync(id, fileName, stream, ct); var replacement = FasDocument.Create(id, old.DocumentTypeCode, old.ChecklistItemCode, old.IsMandatory, fileName, key, mime, size, actor, DateTime.UtcNow, scanner.RequiresScan); db.Add(replacement); await db.SaveChangesAsync(ct); old.Replace(replacement.Id, actor, DateTime.UtcNow); await db.SaveChangesAsync(ct); await storage.DeleteAsync(old.BlobKey, ct); return new { id = replacement.Id, replacement.FileName, replacement.MimeType, replacement.FileSizeBytes, scanStatus = replacement.UploadStatusCode }; }

    public async Task<(Stream Stream, string Mime, string Name)> DownloadDocument(long documentId, CancellationToken ct)
    { var (person, _) = Identity(); var doc = await db.Set<FasDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.UploadStatusCode != "REMOVED", ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); await Owned(doc.FasApplicationId, person, ct); return (await storage.OpenReadAsync(doc.BlobKey, ct), doc.MimeType, doc.FileName); }
    public async Task<(Stream Stream, string Mime, string Name)> AdminDownloadDocument(long documentId, CancellationToken ct) { Actor(); var doc = await db.Set<FasDocument>().AsNoTracking().SingleOrDefaultAsync(x => x.Id == documentId && x.UploadStatusCode != "REMOVED", ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); return (await storage.OpenReadAsync(doc.BlobKey, ct), doc.MimeType, doc.FileName); }
    public async Task<object> RecordScanResult(long documentId, bool passed, CancellationToken ct) { Actor(); var doc = await db.Set<FasDocument>().SingleOrDefaultAsync(x => x.Id == documentId, ct) ?? throw new KeyNotFoundException("FAS.DOCUMENT_NOT_FOUND"); if (passed) doc.MarkScanPassed(); else doc.MarkScanFailed(); await db.SaveChangesAsync(ct); return new { id = doc.Id, scanStatus = doc.UploadStatusCode }; }

    public async Task<object> Activate(long itemId, CancellationToken ct)
    { var (person, actor) = Identity(); return await db.Database.CreateExecutionStrategy().ExecuteAsync(async () => { await using var tx = await db.Database.BeginTransactionAsync(ct); var item = await (from i in db.Set<FasApplicationScheme>() join a in db.Set<FasApplication>() on i.FasApplicationId equals a.Id where i.Id == itemId && a.StudentPersonId == person select i).SingleOrDefaultAsync(ct) ?? throw new KeyNotFoundException("FAS.APPLICATION_SCHEME_NOT_FOUND"); if (item.ValidFrom == null || item.ValidTo == null) throw new InvalidOperationException("FAS.APPROVAL_VALIDITY_REQUIRED"); if (await db.Set<FasActiveScheme>().AnyAsync(x => x.FasApplicationSchemeId == item.Id && x.StatusCode == "ACTIVE", ct)) return new { applicationSchemeId = item.Id, status = "ACTIVE", item.ValidFrom, item.ValidTo }; var now = DateTime.UtcNow; item.Activate(now); db.Add(FasActiveScheme.Activate(person, item.Id, item.FasSchemeId, item.ValidFrom.Value, item.ValidTo.Value, actor, now)); db.Add(FasStatusHistory.Create(item.FasApplicationId, item.Id, "APPROVED", "ACTIVE", null, actor, "STUDENT", now)); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return new { applicationSchemeId = item.Id, status = "ACTIVE", item.ValidFrom, item.ValidTo }; }); }

    private static void ValidateFile(string name, string mime, long size) { var extensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" }; var mimes = new[] { "application/pdf", "image/jpeg", "image/png" }; if (size <= 0 || size > 10 * 1024 * 1024) throw new ArgumentException("FAS.FILE_SIZE_INVALID"); if (!extensions.Contains(Path.GetExtension(name).ToLowerInvariant()) || !mimes.Contains(mime.ToLowerInvariant())) throw new ArgumentException("FAS.FILE_TYPE_INVALID"); }

    public async Task<object> AdminApplications(string? status, long? schemeId, string? keyword, DateOnly? submittedFrom, DateOnly? submittedTo, int page, int pageSize, CancellationToken ct)
    {
        page = Math.Max(1, page); pageSize = Math.Clamp(pageSize, 1, 100);
        var q = from a in db.Set<FasApplication>().AsNoTracking() join i in db.Set<FasApplicationScheme>().AsNoTracking() on a.Id equals i.FasApplicationId join s in db.Set<FasScheme>().AsNoTracking() on i.FasSchemeId equals s.Id select new { a, i, s };
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(x => x.i.StatusCode == status.ToUpper()); if (schemeId.HasValue) q = q.Where(x => x.s.Id == schemeId);
        if (!string.IsNullOrWhiteSpace(keyword)) q = q.Where(x => x.a.StudentName.Contains(keyword) || x.a.ApplicationNo.Contains(keyword) || x.a.StudentId.Contains(keyword));
        if (submittedFrom.HasValue) { var d = submittedFrom.Value.ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.a.SubmittedAtUtc >= d); }
        if (submittedTo.HasValue) { var d = submittedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue); q = q.Where(x => x.a.SubmittedAtUtc < d); }
        var total = await q.CountAsync(ct); var items = await q.OrderByDescending(x => x.a.SubmittedAtUtc).Skip((page - 1) * pageSize).Take(pageSize).Select(x => new { applicationId = x.a.Id, applicationSchemeId = x.i.Id, applicationReference = x.a.ApplicationNo, studentName = x.a.StudentName, studentId = x.a.StudentId, schemeId = x.s.Id, schemeName = x.s.Name, submittedAt = x.a.SubmittedAtUtc, status = x.i.StatusCode }).ToListAsync(ct); return new { items, page, pageSize, total, totalPages = (int)Math.Ceiling(total / (double)pageSize) };
    }

    public async Task<object> AdminApplication(long id,CancellationToken ct)
    {
        var app=await db.Set<FasApplication>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)
            ??throw new KeyNotFoundException("FAS.APPLICATION_NOT_FOUND");
        var items=await(from i in db.Set<FasApplicationScheme>().AsNoTracking()
            join s in db.Set<FasScheme>().AsNoTracking()on i.FasSchemeId equals s.Id
            where i.FasApplicationId==id
            select new{i.Id,i.FasSchemeId,s.Name,s.StartDate,s.EndDate,i.StatusCode,i.ApprovedAmount,i.ApprovedComponentsJson,i.RejectionNotes,i.ValidFrom,i.ValidTo,i.IsActive}).ToListAsync(ct);
        var schemeIds=items.Select(x=>x.FasSchemeId).Distinct().ToArray();
        var tiers=await db.Set<FasTier>().AsNoTracking().Where(x=>schemeIds.Contains(x.FasSchemeId)).OrderBy(x=>x.DisplayOrder).ToListAsync(ct);
        var tierIds=tiers.Select(x=>x.Id).ToArray();
        var criteria=await db.Set<FasTierCriteria>().AsNoTracking().Where(x=>tierIds.Contains(x.FasTierId)).OrderBy(x=>x.DisplayOrder).ToListAsync(ct);
        var criteriaIds=criteria.Select(x=>x.Id).ToArray();
        var categorical=await db.Set<FasTierCriteriaNationality>().AsNoTracking().Where(x=>criteriaIds.Contains(x.FasTierCriteriaId)).ToListAsync(ct);
        var schemes=items.Select(item=>new{
            item.Id,item.FasSchemeId,item.Name,item.StatusCode,item.ApprovedAmount,item.ApprovedComponentsJson,item.RejectionNotes,item.ValidFrom,item.ValidTo,item.IsActive,
            tiers=tiers.Where(t=>t.FasSchemeId==item.FasSchemeId).Select(t=>new{tierId=t.Id,t.Label,t.SubsidyType,t.SubsidyValue,t.DisplayOrder}).ToArray(),
            recommendedTierId=tiers.Where(t=>t.FasSchemeId==item.FasSchemeId).OrderBy(t=>t.DisplayOrder)
                .Where(t=>TierMatches(t.Id,app,criteria,categorical)).Select(t=>(long?)t.Id).FirstOrDefault()
        }).ToArray();
        var documents=await db.Set<FasDocument>().AsNoTracking().Where(x=>x.FasApplicationId==id&&x.UploadStatusCode!="REMOVED").Select(x=>new{x.Id,x.ChecklistItemCode,x.FileName,x.MimeType,x.FileSizeBytes,x.UploadStatusCode}).ToListAsync(ct);
        var declarations=await db.Set<FasDeclaration>().AsNoTracking().Where(x=>x.FasApplicationId==id).Select(x=>new{x.DeclarationTypeCode,x.IsAccepted,x.AcceptedAtUtc,x.DeclarationTextSnapshot}).ToListAsync(ct);
        var history=await db.Set<FasStatusHistory>().AsNoTracking().Where(x=>x.FasApplicationId==id).OrderBy(x=>x.ChangedAtUtc).Select(x=>new{x.FasApplicationSchemeId,x.OldStatusCode,x.NewStatusCode,x.Notes,x.ChangedAtUtc,x.ChangedByRole}).ToListAsync(ct);
        return new{app.Id,applicationReference=app.ApplicationNo,app.StudentName,app.StudentId,app.NricFinMasked,app.DateOfBirth,app.NationalityCode,app.Mobile,app.Address,app.Email,currentSchool=new{id=app.SchoolOrganizationId,name=app.SchoolName,app.StudentNumber},income=new{app.IsWelfareHomeResident,app.EmploymentStatusCode,app.MonthlyHouseholdIncome,app.HouseholdMemberCount,app.OtherMonthlyIncome,app.PerCapitaIncome},app.StatusCode,app.SubmittedAtUtc,schemes,documents,declarations,history};
    }

    public async Task<object> ApproveScheme(long id,AdminApproveSchemeRequest r,CancellationToken ct)
    {var actor=Actor();return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{await using var tx=await db.Database.BeginTransactionAsync(ct);var item=await db.Set<FasApplicationScheme>().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException("FAS.APPLICATION_SCHEME_NOT_FOUND");var tier=await db.Set<FasTier>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==r.TierId&&x.FasSchemeId==item.FasSchemeId,ct)??throw new ArgumentException("FAS.INVALID_TIER");var scheme=await db.Set<FasScheme>().AsNoTracking().SingleAsync(x=>x.Id==item.FasSchemeId,ct);var components=JsonSerializer.Serialize(new{tierId=tier.Id,tier.Label,tier.SubsidyType,tier.SubsidyValue});item.Approve(actor,tier.SubsidyValue,components,scheme.StartDate,scheme.EndDate,DateTime.UtcNow);db.Add(FasStatusHistory.Create(item.FasApplicationId,item.Id,"PENDING","APPROVED",r.Remarks,actor,"ADMIN",DateTime.UtcNow));await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new{applicationSchemeId=item.Id,status=item.StatusCode,selectedTierId=tier.Id,item.ApprovedAmount,item.ValidFrom,item.ValidTo};});}
    public async Task<object> RejectScheme(long id,AdminRejectSchemeRequest r,CancellationToken ct)
    {var actor=Actor();return await db.Database.CreateExecutionStrategy().ExecuteAsync(async()=>{await using var tx=await db.Database.BeginTransactionAsync(ct);var item=await db.Set<FasApplicationScheme>().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw new KeyNotFoundException("FAS.APPLICATION_SCHEME_NOT_FOUND");item.Reject(actor,r.Notes,DateTime.UtcNow);db.Add(FasStatusHistory.Create(item.FasApplicationId,item.Id,"PENDING","REJECTED",r.Notes,actor,"ADMIN",DateTime.UtcNow));await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return new{applicationSchemeId=item.Id,status=item.StatusCode,item.RejectionNotes};});}

    public async Task<object> ReviewValidation(long id, CancellationToken ct)
    {
        var review = await ApplicationReview(id, ct); var (person, _) = Identity(); var app = await Owned(id, person, ct);
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
            await using var tx = await db.Database.BeginTransactionAsync(ct); var app = await OwnedDraft(id, person, ct);
            var items = await db.Set<FasApplicationScheme>().Where(x => x.FasApplicationId == id && x.StatusCode == "DRAFT").ToListAsync(ct);
            if (items.Count == 0) throw new InvalidOperationException("FAS.SCHEME_REQUIRED");
            if (app.SchoolOrganizationId == null) throw new InvalidOperationException("FAS.SUBMISSION_INCOMPLETE"); var applicable = await ApplicableSchemeIds(app.SchoolOrganizationId.Value, ct); if (items.Any(x => !applicable.Contains(x.FasSchemeId)) || await db.Set<FasScheme>().AnyAsync(x => items.Select(i => i.FasSchemeId).Contains(x.Id) && x.StatusCode != "ACTIVE", ct)) throw new InvalidOperationException("FAS.SCHEME_NOT_AVAILABLE");
            await EnsureNoDuplicateApplications(person, items.Select(x => x.FasSchemeId).ToArray(), id, ct);
            if (string.IsNullOrWhiteSpace(app.StudentName) || string.IsNullOrWhiteSpace(app.NricFinMasked) || app.DateOfBirth == null || string.IsNullOrWhiteSpace(app.NationalityCode) || string.IsNullOrWhiteSpace(app.ParentNationalitiesJson) || string.IsNullOrWhiteSpace(app.Mobile) || string.IsNullOrWhiteSpace(app.Address) || app.SchoolOrganizationId == null || string.IsNullOrWhiteSpace(app.StudentNumber) || string.IsNullOrWhiteSpace(app.Email) || app.IsWelfareHomeResident == null || (app.IsWelfareHomeResident == false && (app.MonthlyHouseholdIncome == null || app.HouseholdMemberCount == null || string.IsNullOrWhiteSpace(app.EmploymentStatusCode))) || !await DocumentsComplete(id, ct) || await db.Set<FasDeclaration>().CountAsync(x => x.FasApplicationId == id && x.IsAccepted, ct) < 2)
                throw new InvalidOperationException("FAS.SUBMISSION_INCOMPLETE");
            var now = DateTime.UtcNow; app.SubmitDraft(actor, now); db.Add(FasStatusHistory.Create(id, null, "DRAFT", "SUBMITTED", "Application submitted", actor, "STUDENT", now)); foreach (var item in items) { item.Submit(); db.Add(FasStatusHistory.Create(id, item.Id, "DRAFT", "PENDING", "Submitted for review", actor, "STUDENT", now)); }
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await ApplicationReview(id, ct);
        });
    }



    public async Task<object> MyApplications(CancellationToken ct)
     { var (person, _) = Identity(); return await (from a in db.Set<FasApplication>().AsNoTracking() join i in db.Set<FasApplicationScheme>().AsNoTracking() on a.Id equals i.FasApplicationId join s in db.Set<FasScheme>().AsNoTracking() on i.FasSchemeId equals s.Id where a.StudentPersonId == person select new { applicationId=a.Id,applicationSchemeId=i.Id,applicationReference=a.ApplicationNo,schemeId=s.Id,schemeName=s.Name,submittedDate=a.SubmittedAtUtc,status=i.StatusCode,i.RejectionNotes,i.ApprovedAmount,i.ApprovedComponentsJson,i.IsActive,i.ValidFrom,i.ValidTo,redeemedAt=i.RedeemedAtUtc }).ToListAsync(ct); }
    public async Task<object> Summary(CancellationToken ct)
    { var (person, _) = Identity(); var draft = await db.Set<FasApplication>().AsNoTracking().Where(x => x.StudentPersonId == person && x.StatusCode == "DRAFT").Select(x => (long?)x.Id).FirstOrDefaultAsync(ct); var active = await db.Set<FasActiveScheme>().AsNoTracking().Where(x => x.StudentPersonId == person && x.StatusCode == "ACTIVE").Select(x => new { x.FasApplicationSchemeId, x.FasSchemeId, x.ActiveFrom, x.ActiveTo }).FirstOrDefaultAsync(ct); return new { canApply = true, blockingReason = (string?)null, activeScheme = active, resumableDraftId = draft }; }

    public async Task<object> ApplicationReview(long id, CancellationToken ct)
    { var (person, _) = Identity(); var app = await Owned(id, person, ct); var schemes = await (from i in db.Set<FasApplicationScheme>().AsNoTracking() join s in db.Set<FasScheme>().AsNoTracking() on i.FasSchemeId equals s.Id where i.FasApplicationId == id select new { i.Id, i.FasSchemeId, s.Name, i.StatusCode, i.ApprovedAmount, i.RejectionNotes, i.ValidFrom, i.ValidTo, i.IsActive }).ToListAsync(ct); return new { app.Id, applicationReference = app.ApplicationNo, app.StatusCode, app.StudentName, app.NricFinMasked, app.DateOfBirth, app.NationalityCode, parentNationalities = ParseParentNationalities(app.ParentNationalitiesJson), app.AccountTypeCode, app.Mobile, app.Address, app.Email, currentSchool = new { id = app.SchoolOrganizationId, name = app.SchoolName, app.StudentNumber }, income = new { app.IsWelfareHomeResident, app.EmploymentStatusCode, app.MonthlyHouseholdIncome, app.HouseholdMemberCount, app.OtherMonthlyIncome, app.PerCapitaIncome }, app.SubmittedAtUtc, schemes }; }

    private async Task<bool> DocumentsComplete(long id, CancellationToken ct) { var app = await db.Set<FasApplication>().SingleAsync(x => x.Id == id, ct); return (await BuildChecklist(app, ct)).All(x => x.IsComplete); }
    private Task<List<long>> ApplicableSchemeIds(long schoolId, CancellationToken ct) => IsSqlServer()
        ? db.Database.SqlQuery<long>($"""
            SELECT s.FASSchemeId AS Value FROM fas.FASScheme s
            WHERE NOT EXISTS (SELECT 1 FROM fas.FASSchemeCourse sc WHERE sc.FASSchemeId=s.FASSchemeId)
               OR EXISTS (SELECT 1 FROM fas.FASSchemeCourse sc JOIN course.Course c ON c.CourseId=sc.CourseId WHERE sc.FASSchemeId=s.FASSchemeId AND c.OrganizationId={schoolId})
            """).ToListAsync(ct)
        : db.Set<FasScheme>().AsNoTracking().Select(scheme => scheme.Id).ToListAsync(ct);
    private bool IsSqlServer() => db.Database.ProviderName == "Microsoft.EntityFrameworkCore.SqlServer";
    private async Task EnsureNoDuplicateApplications(long person, IReadOnlyCollection<long> schemeIds, long? currentApplicationId, CancellationToken ct) { var duplicates = await (from i in db.Set<FasApplicationScheme>().AsNoTracking() join a in db.Set<FasApplication>().AsNoTracking() on i.FasApplicationId equals a.Id where a.StudentPersonId == person && (!currentApplicationId.HasValue || a.Id != currentApplicationId.Value) && a.StatusCode != "WITHDRAWN" && schemeIds.Contains(i.FasSchemeId) && i.StatusCode != "CANCELLED" && i.StatusCode != "EXPIRED" select i.FasSchemeId).Distinct().ToListAsync(ct); if (duplicates.Count > 0) throw new InvalidOperationException($"FAS.DUPLICATE_SCHEME_APPLICATION:{string.Join(',', duplicates)}"); }
    private async Task<FasApplication> Owned(long id, long person, CancellationToken ct) => await db.Set<FasApplication>().SingleOrDefaultAsync(x => x.Id == id && x.StudentPersonId == person, ct) ?? throw new KeyNotFoundException("FAS.APPLICATION_NOT_FOUND");
    private async Task<FasApplication> OwnedDraft(long id, long person, CancellationToken ct) { var a = await Owned(id, person, ct); if (a.StatusCode != "DRAFT") throw new InvalidOperationException("FAS.APPLICATION_LOCKED"); return a; }
    private sealed record ProfileRow(long PersonId, string Name, string? NricFinMasked, DateOnly DateOfBirth, string NationalityCode, string? Mobile, string? Address, string? Email, long SchoolOrganizationId, string SchoolName, string StudentNumber);

    private async Task<string> ResolveAccountType(long personId, CancellationToken ct)
    {
        if (!IsSqlServer()) return "EDUCATION_ACCOUNT";
        int exists = await db.Database.SqlQuery<int>($"SELECT CASE WHEN EXISTS (SELECT 1 FROM account.EducationAccount WHERE PersonId = {personId}) THEN 1 ELSE 0 END AS Value").SingleAsync(ct);
        return exists == 1 ? "EDUCATION_ACCOUNT" : "PERSONAL_ACCOUNT";
    }
    private static bool TierMatches(long tierId,FasApplication app,IReadOnlyCollection<FasTierCriteria> allCriteria,IReadOnlyCollection<FasTierCriteriaNationality> allValues)
    {
        var criteria=allCriteria.Where(x=>x.FasTierId==tierId).OrderBy(x=>x.DisplayOrder).ToArray();
        if(criteria.Length==0)return true;
        var age=app.DateOfBirth.HasValue?DateOnly.FromDateTime(DateTime.UtcNow).Year-app.DateOfBirth.Value.Year:(int?)null;
        if(age.HasValue&&app.DateOfBirth!.Value.AddYears(age.Value)>DateOnly.FromDateTime(DateTime.UtcNow))age--;
        var parents=ParseParentNationalities(app.ParentNationalitiesJson);
        bool Match(FasTierCriteria c)
        {
            var values=allValues.Where(x=>x.FasTierCriteriaId==c.Id).Select(x=>x.Nationality).ToArray();
            return c.CriteriaType switch
            {
                "AGE"=>age.HasValue&&age.Value>=c.NumberFrom&&age.Value<=c.NumberTo,
                "GDP" or "GHI"=>app.MonthlyHouseholdIncome.HasValue&&app.MonthlyHouseholdIncome.Value>=c.NumberFrom&&app.MonthlyHouseholdIncome.Value<=c.NumberTo,
                "PCI"=>app.PerCapitaIncome.HasValue&&app.PerCapitaIncome.Value>=c.NumberFrom&&app.PerCapitaIncome.Value<=c.NumberTo,
                "NATIONALITY"=>values.Contains(app.NationalityCode??string.Empty,StringComparer.OrdinalIgnoreCase)||app.NationalityCode=="SG"&&values.Contains("Singapore Citizen",StringComparer.OrdinalIgnoreCase),
                "PARENT_NATIONALITY"=>parents.Any(p=>values.Contains(p,StringComparer.OrdinalIgnoreCase)),
                "ACCOUNT_TYPE"=>values.Contains(app.AccountTypeCode,StringComparer.OrdinalIgnoreCase),
                _=>false
            };
        }
        var result=Match(criteria[0]);
        for(var index=1;index<criteria.Length;index++)result=criteria[index-1].ConnectorToNext=="OR"?result||Match(criteria[index]):result&&Match(criteria[index]);
        return result;
    }
    private static string[] ParseParentNationalities(string? json) => string.IsNullOrWhiteSpace(json) ? Array.Empty<string>() : JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>();
    private sealed record CourseRow(long Id, string CourseCode, string CourseName);
    private sealed record ChecklistItem(string ChecklistItemCode, string Label, bool IsMandatory, object? Document, bool IsComplete);
}
