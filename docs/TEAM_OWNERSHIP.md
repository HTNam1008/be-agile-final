# Suggested six-developer ownership

1. Dev 1: Identity, authorization, organizations and relevant UI.
2. Dev 2: Person, Student, contact profile and relevant UI.
3. Dev 3: Education Account, ledger and relevant Admin/e-Service UI.
4. Dev 4: Top-up, interest, settlement and relevant UI.
5. Dev 5: Course, enrollment, billing and relevant UI.
6. Dev 6: FAS, payment, receipt and relevant UI.

The backend groups Dev 1 and Dev 2 in IdentityPlatform, and Dev 3 and Dev 4 in EducationAccountTopUp. They work in separate feature folders inside the module. CODEOWNERS can be made more granular when actual usernames are known.

One migration owner is nominated per sprint. Other developers add entity configurations but do not independently commit generated migrations.
