$ErrorActionPreference = 'Stop'
$repositoryPath = Split-Path -Parent $PSScriptRoot

function Save-CrmCommit {
    param([string]$Message, [string[]]$Files)

    & git -C $repositoryPath add -- @Files
    if ($LASTEXITCODE -ne 0) { throw 'Could not stage the CRM changes.' }

    & git -C $repositoryPath diff --cached --quiet
    if ($LASTEXITCODE -eq 0) { return }
    if ($LASTEXITCODE -ne 1) { throw 'Could not inspect staged changes.' }

    & git -C $repositoryPath commit -m $Message
    if ($LASTEXITCODE -ne 0) { throw 'Could not create the CRM commit.' }
}

# Avoid including unrelated changes already staged by the user.
& git -C $repositoryPath diff --cached --quiet
if ($LASTEXITCODE -ne 0) { throw 'Commit or unstage existing staged changes before running this script.' }

Save-CrmCommit -Message 'feat(crm): support optional tags and independent customer contacts' -Files @(
    'NEXT.Application/DTOs/Common/ReferenceOptionDto.cs',
    'NEXT.Application/DTOs/CustomerContacts/CustomerContactInputDto.cs',
    'NEXT.Application/DTOs/Customers/CustomerInputDto.cs',
    'NEXT.Application/DTOs/Suppliers/SupplierInputDto.cs',
    'NEXT.Application/Features/Common/Mapping/RecordMappingRegister.cs',
    'NEXT.Application/Features/Common/Validation/ReferenceGuard.cs',
    'NEXT.Application/Features/CustomerContacts/CustomerContactRules.cs',
    'NEXT.Application/Features/Customers/CustomerRules.cs',
    'NEXT.Application/Features/Suppliers/SupplierRules.cs',
    'NEXT.Domain/Entities/Customer.cs',
    'NEXT.Domain/Entities/CustomerContact.cs',
    'NEXT.Domain/Entities/Supplier.cs',
    'NEXT.Infrastructure/Migrations/NEXTDbContextModelSnapshot.cs',
    'NEXT.Infrastructure/Migrations/20261109103423_MakeCrmReferencesOptional.cs',
    'NEXT.Infrastructure/Migrations/20261109103423_MakeCrmReferencesOptional.Designer.cs',
    'NEXT.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs',
    'NEXT.Infrastructure/Persistence/Configurations/CustomerContactConfiguration.cs',
    'NEXT.Infrastructure/Persistence/Configurations/SupplierConfiguration.cs',
    'NEXT.Tests/PostgresInventoryTests.cs'
)

Save-CrmCommit -Message 'feat(crm): add localized partner forms and searchable contact selection' -Files @(
    'NEXT.web/Controllers/EntityController.cs',
    'NEXT.web/Filters/OperationNotificationFilter.cs',
    'NEXT.web/Models/FieldControlModel.cs',
    'NEXT.web/Models/FieldOption.cs',
    'NEXT.web/Models/FormField.cs',
    'NEXT.web/Models/RecordDetailsDto.cs',
    'NEXT.web/Resources/Localization/UiText.ar.resx',
    'NEXT.web/Resources/Localization/UiText.resx',
    'NEXT.web/Services/EntityPresenter.cs',
    'NEXT.web/Services/FormFieldPresenter.cs',
    'NEXT.web/Services/UiIcons.cs',
    'NEXT.web/Views/Shared/RecordDetails.cshtml',
    'NEXT.web/Views/Shared/Table.cshtml',
    'NEXT.web/Views/Shared/_FieldControl.cshtml',
    'NEXT.web/Views/Shared/_Layout.cshtml',
    'NEXT.web/Views/Shared/_ModalForm.cshtml',
    'NEXT.web/Views/Shared/_ProfileImagePicker.cshtml',
    'NEXT.web/Views/Shared/_ReferencePicker.cshtml',
    'NEXT.web/wwwroot/css/site.css',
    'NEXT.web/wwwroot/js/profile-image-picker.js',
    'NEXT.web/wwwroot/js/reference-picker.js'
)

Save-CrmCommit -Message 'test(crm): verify optional relationships migrations and bilingual presentation' -Files @(
    'NEXT.Tests/CrmTests.cs',
    'NEXT.Tests/Browser/presentation.test.cjs',
    'NEXT.Tests/Browser/product-pricing.test.cjs',
    'docs/crm-qa-report.md',
    'docs/commit-crm-changes.ps1'
)
