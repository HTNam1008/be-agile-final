# Architecture review and requirement flow

## Confirmed financial distinctions

1. Top-up is an MOE credit to the Education Account ledger.
2. FAS is a subsidy against BillLine and must not credit the Education Account.
3. Online payment pays a Bill and must not increase the Education Account balance.
4. CPF or bank is a settlement destination after account closure.
5. Student and Account Holder are different roles of a Person.

## Main flows

### Account lifecycle

`Upstream citizen event -> Person -> EducationAccount -> ledger -> close at 30 -> Bank/CPF settlement`

### Top-up

`TopUpPlan -> target/rules -> preview -> TopUpExecution -> TopUpAllocation -> ledger credit`

### Course and billing

`Course -> CourseOffering -> fee versions -> Enrollment -> Bill -> BillLine`

### FAS

`Scheme -> eligibility -> tier -> benefit -> Application -> assessment -> subsidy allocation -> BillLine reduction`

### Payment

`Bill outstanding -> Payment -> one or more tenders -> account reservation + online authorization -> bill payment`

## Module dependency rule

- IdentityPlatform depends only on Shared.
- EducationAccountTopUp may use IdentityPlatform.Contracts.
- CourseBilling may use IdentityPlatform.Contracts.
- FasPayment may use contracts from the other three modules.
- No module may reference another module's implementation or database entities.

## Open decisions kept out of the template

- FAS stacking.
- Whether FAS subsidizes GST.
- Mandatory human approval for every FAS application.
- Partial bill payments across multiple sessions.
- Refund destination.
- Dropout proration formula.
- Production CPF/bank integration.
- Two-level top-up approval.
- Annual interest as core or bonus.
- Foreign-student identity provider.
