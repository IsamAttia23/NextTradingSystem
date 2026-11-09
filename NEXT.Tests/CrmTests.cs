using System.Net;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NEXT.Application.DTOs.CustomerContacts;
using NEXT.Application.DTOs.Customers;
using NEXT.Application.DTOs.Suppliers;
using NEXT.Application.Exceptions;
using NEXT.Application.Features.Common.CQRS;
using NEXT.Domain.Entities;
using NEXT.Domain.Enums;
using NEXT.Infrastructure.Persistence;
using NEXT.web.Services;
using Xunit;

namespace NEXT.Tests;

public sealed class CrmTests
{
    [Fact]
    public async Task CustomersClearIndividualCompanyNamesAndAllowRemovingTagsOnUpdate()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var company = await sender.Send(new CreateEntityCommand<CustomerDto, CustomerInputDto>(new()
        {
            Name = "Company customer", Type = CustomerType.Company, CompanyName = "Company"
        }));
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var tag = new Tag { Name = "Blue", Color = "#153d97" };
        db.Add(tag);
        await db.SaveChangesAsync();
        var updated = await sender.Send(new UpdateEntityCommand<CustomerDto, CustomerInputDto>(company.Id, new()
        {
            Name = "Individual customer", Type = CustomerType.Individual,
            CompanyName = new string('x', 201), TagId = tag.Id
        }, company.RowVersion));
        Assert.Null(updated.CompanyName);
        Assert.Equal(tag.Id, updated.TagId);
        updated = await sender.Send(new UpdateEntityCommand<CustomerDto, CustomerInputDto>(company.Id, new()
        {
            Name = "No tag", Type = CustomerType.Company, CompanyName = "Retained company"
        }, updated.RowVersion));
        Assert.Null(updated.TagId);
        Assert.Equal("Retained company", updated.CompanyName);
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new CreateEntityCommand<CustomerDto, CustomerInputDto>(new()
        {
            Name = "Invalid company", Type = CustomerType.Company, CompanyName = new string('x', 201)
        })));
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new CreateEntityCommand<CustomerDto, CustomerInputDto>(new()
        {
            Name = "Invalid tag", TagId = Guid.NewGuid()
        })));
    }

    [Fact]
    public async Task SuppliersShareOptionalTagValidationAndSupportRemovingExistingTags()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var tag = new Tag { Name = "Supplier tag", Color = "#147550" };
        db.Add(tag);
        await db.SaveChangesAsync();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var supplier = await sender.Send(new CreateEntityCommand<SupplierDto, SupplierInputDto>(new()
        {
            Name = "Supplier", TagId = tag.Id
        }));
        supplier = await sender.Send(new UpdateEntityCommand<SupplierDto, SupplierInputDto>(supplier.Id, new()
        {
            Name = "Supplier without tag"
        }, supplier.RowVersion));
        Assert.Null(supplier.TagId);
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new CreateEntityCommand<SupplierDto, SupplierInputDto>(new()
        {
            Name = "Invalid supplier", TagId = Guid.Empty
        })));
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new CreateEntityCommand<SupplierDto, SupplierInputDto>(new()
        {
            Name = "Unknown tag", TagId = Guid.NewGuid()
        })));
    }

    [Fact]
    public async Task ContactsRequirePhonesAndCanBeCreatedAndUpdatedWithoutCustomers()
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var contact = await sender.Send(new CreateEntityCommand<CustomerContactDto, CustomerContactInputDto>(new()
        {
            Name = "Independent contact", Phone = "+249123456789"
        }));
        Assert.Null(contact.CustomerId);
        var customer = await sender.Send(new CreateEntityCommand<CustomerDto, CustomerInputDto>(new() { Name = "Owner" }));
        contact = await sender.Send(new UpdateEntityCommand<CustomerContactDto, CustomerContactInputDto>(contact.Id, new()
        {
            Name = "Linked contact", Phone = "+249123456789", CustomerId = customer.Id
        }, null));
        Assert.Equal(customer.Id, contact.CustomerId);
        contact = await sender.Send(new UpdateEntityCommand<CustomerContactDto, CustomerContactInputDto>(contact.Id, new()
        {
            Name = "Independent again", Phone = "+249123456789"
        }, null));
        Assert.Null(contact.CustomerId);
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new CreateEntityCommand<CustomerContactDto, CustomerContactInputDto>(new() { Name = "Missing phone" })));
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new UpdateEntityCommand<CustomerContactDto, CustomerContactInputDto>(contact.Id, new() { Name = "Missing phone" }, null)));
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new CreateEntityCommand<CustomerContactDto, CustomerContactInputDto>(new()
        {
            Name = "Unknown customer", Phone = "+249123456789", CustomerId = Guid.NewGuid()
        })));
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        (await db.Customers.SingleAsync(x => x.Id == customer.Id)).IsActive = false;
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<AppValidationException>(() => sender.Send(new CreateEntityCommand<CustomerContactDto, CustomerContactInputDto>(new()
        {
            Name = "Inactive customer", Phone = "+249123456789", CustomerId = customer.Id
        })));
        Assert.True(db.Model.FindEntityType(typeof(CustomerContact))!.FindProperty(nameof(CustomerContact.CustomerId))!.IsNullable);
        Assert.False(db.Model.FindEntityType(typeof(CustomerContact))!.FindProperty(nameof(CustomerContact.Phone))!.IsNullable);
    }

    [Theory]
    [InlineData("en", "Add contact", "Search for a customer")]
    [InlineData("ar", "إضافة جهة اتصال", "ابحث عن العميل")]
    public async Task FormsDetailsAndListsReuseComponentsAndSupportBothLanguages(string culture, string addLabel, string searchLabel)
    {
        await using var app = await WorkspaceUiTests.StartAsync();
        using var client = WorkspaceUiTests.Client(app);
        await WorkspaceUiTests.Login(client, "ui-admin");
        client.DefaultRequestHeaders.Add("Accept-Language", culture);
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<NEXTDbContext>();
        var tag = new Tag { Name = "Visible chip", Color = "#147550" };
        var customer = new Customer { Name = "Customer preview", Tag = tag, IsActive = false };
        var contact = new CustomerContact { Name = "No customer", Phone = "12345678" };
        db.AddRange(tag, customer, contact);
        await db.SaveChangesAsync();

        var html = WebUtility.HtmlDecode(await client.GetStringAsync("/Customers/Create"));
        Assert.Contains("profile-picker-circle", html);
        Assert.Contains("data-original-src=", html);
        Assert.Contains("form-check form-switch", html);
        Assert.Contains("data-visible-when-value=\"Company\" hidden", html);
        Assert.Contains("background-color:#147550", html);
        Assert.Contains("data-reference-option", html);
        var companyError = await WorkspaceUiTests.Post(client, "/Customers/Create", WorkspaceUiTests.Token(html),
            ("Name", "Company"), ("Type", "Company"), ("CompanyName", new string('x', 201)));
        Assert.Equal(HttpStatusCode.BadRequest, companyError.StatusCode);
        var errorBody = System.Text.Json.JsonDocument.Parse(await companyError.Content.ReadAsStringAsync());
        var errorMessage = errorBody.RootElement.GetProperty("errors").GetProperty("CompanyName")[0].GetString();
        Assert.Equal(culture == "ar" ? "يجب ألا يتجاوز اسم الشركة 200 حرف." : "The company name must not exceed 200 characters.", errorMessage);
        html = WebUtility.HtmlDecode(await client.GetStringAsync("/Suppliers/Create"));
        Assert.Contains("data-reference-option", html);
        Assert.DoesNotContain("name=\"Type\"", html);
        Assert.DoesNotContain("name=\"CompanyName\"", html);
        Assert.DoesNotContain("name=\"CreditLimit\"", html);

        html = WebUtility.HtmlDecode(await client.GetStringAsync($"/Customers/Details/{customer.Id}"));
        Assert.Contains(addLabel, html);
        Assert.Contains($"/CustomersContact/Create?customerId={customer.Id}", html);
        Assert.Contains("customer-detail-grid", html);
        html = WebUtility.HtmlDecode(await client.GetStringAsync($"/CustomersContact/Create?customerId={customer.Id}"));
        Assert.Contains($"name=\"CustomerId\" value=\"{customer.Id}\"", html);
        Assert.Contains("value=\"Customer preview\"", html);
        Assert.Contains(searchLabel, html);
        Assert.Contains("name=\"Phone\"", html);
        html = await client.GetStringAsync($"/CustomersContact/Details/{contact.Id}");
        Assert.DoesNotContain("related-card", html);
        Assert.Contains("No customer", html);
        html = await client.GetStringAsync("/Customers");
        Assert.Contains("inactive-tag", html);
        Assert.Contains("#eye", html);
        Assert.Contains("#edit", html);
        Assert.Contains("#trash", html);

        var tags = scope.ServiceProvider.GetRequiredService<EntityPresenter<NEXT.Application.DTOs.Tags.TagDto, NEXT.Application.DTOs.Tags.TagInputDto>>();
        var table = await tags.TableAsync("Tags", (action, _) => "/Tags/" + action, new(), default);
        Assert.DoesNotContain("Color", table.Columns);
        Assert.Contains((await tags.FormAsync("Edit", "/Tags/Edit", tag.Id, default)).Fields, field => field.Name == "Color" && field.Type == "color");
    }

    [PostgresFact]
    public async Task MigrationPreservesLegacyContactsAndAllowsOptionalRelationshipsOnPostgres()
    {
        await using var fixture = await PostgresInventoryTests.Database.Create(migrate: true);
        var db = fixture.Db;
        await db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20261107121526_AddAccountSecurity");
        var tag = new Tag { Name = "Legacy tag", Color = "#147550" };
        var customer = new Customer { Name = "Legacy customer", Tag = tag };
        var supplier = new Supplier { Name = "Legacy supplier", Tag = tag };
        var contact = new CustomerContact { Name = "Legacy contact", Customer = customer };
        db.AddRange(tag, customer, supplier, contact);
        await db.SaveChangesAsync();
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CustomerContacts\" SET \"Phone\" = NULL WHERE \"Id\" = {contact.Id}");
        db.ChangeTracker.Clear();
        await db.Database.MigrateAsync();
        var migrated = await db.CustomerContacts.SingleAsync(x => x.Id == contact.Id);
        Assert.Equal("", migrated.Phone);
        Assert.Equal(customer.Id, migrated.CustomerId);
        Assert.Equal(tag.Id, (await db.Customers.SingleAsync(x => x.Id == customer.Id)).TagId);
        Assert.Equal(tag.Id, (await db.Suppliers.SingleAsync(x => x.Id == supplier.Id)).TagId);
        db.AddRange(new Customer { Name = "No tag" }, new Supplier { Name = "No tag" },
            new CustomerContact { Name = "No customer", Phone = "12345678" });
        await db.SaveChangesAsync();
        Assert.False(db.Database.HasPendingModelChanges());
        var error = await Assert.ThrowsAsync<Npgsql.PostgresException>(() =>
            db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>().MigrateAsync("20261107121526_AddAccountSecurity"));
        Assert.Contains("Assign tags and customers", error.MessageText);
    }
}
