using Westalife.Domain.Constants;

namespace Westalife.Domain.Tests;

public class PermissionCatalogTests
{
    [Fact]
    public void Catalog_matches_the_granular_codes()
    {
        string[] expected =
        [
            Permissions.Apartments.View,
            Permissions.Apartments.Readings,
            Permissions.Apartments.Analysis,
            Permissions.Apartments.Create,
            Permissions.Apartments.Edit,
            Permissions.Apartments.Delete,
            Permissions.Apartments.ManageResident,
            Permissions.Invoices.View,
            Permissions.Invoices.Create,
            Permissions.Invoices.Edit,
            Permissions.Invoices.Cancel,
            Permissions.Invoices.Calculate,
            Permissions.Payments.View,
            Permissions.Payments.Create,
            Permissions.Payments.Cancel,
            Permissions.Budget.View,
            Permissions.Budget.Create,
            Permissions.Budget.Edit,
            Permissions.Budget.Transfer,
            Permissions.Bank.View,
            Permissions.Bank.Manage,
            Permissions.Reports.GeneralAssembly,
            Permissions.Reports.IncomeExpense,
            Permissions.Reports.ApartmentTracking,
            Permissions.Reports.FuelBreakdown,
            Permissions.Reports.BankControl,
            Permissions.Reports.GasNotifications,
            Permissions.MeterReadings.View,
            Permissions.MeterReadings.ManualEntry,
            Permissions.MeterReadings.TriggerRead,
            Permissions.Notifications.View,
            Permissions.Notifications.Send,
            Permissions.Settings.General,
            Permissions.Settings.Notification,
            Permissions.Settings.MBus,
            Permissions.Settings.ManageUsers,
            Permissions.Roles.View,
            Permissions.Roles.Manage
        ];

        Assert.Equal(expected, Permissions.All);
        Assert.All(expected, code => Assert.True(Permissions.IsKnown(code)));
        Assert.False(Permissions.IsKnown(Permissions.Legacy.ReportsView));
        Assert.False(Permissions.IsKnown(Permissions.Legacy.SettingsEdit));
    }

    [Fact]
    public void Accountant_keeps_finance_invoice_payment_and_report_codes()
    {
        Assert.Contains(Permissions.Invoices.Calculate, Permissions.Accountant);
        Assert.Contains(Permissions.Payments.Cancel, Permissions.Accountant);
        Assert.Contains(Permissions.Budget.Transfer, Permissions.Accountant);
        Assert.Contains(Permissions.Bank.Manage, Permissions.Accountant);
        Assert.Contains(Permissions.Reports.GeneralAssembly, Permissions.Accountant);
        Assert.Contains(Permissions.Reports.GasNotifications, Permissions.Accountant);
        Assert.Equal(6, Permissions.Reports.Features.Count(code => Permissions.Accountant.Contains(code)));
        Assert.DoesNotContain(Permissions.Apartments.View, Permissions.Accountant);
        Assert.DoesNotContain(Permissions.Apartments.Readings, Permissions.Accountant);
        Assert.DoesNotContain(Permissions.MeterReadings.View, Permissions.Accountant);
        Assert.DoesNotContain(Permissions.Notifications.Send, Permissions.Accountant);
        Assert.DoesNotContain(Permissions.Settings.General, Permissions.Accountant);
        Assert.DoesNotContain(Permissions.Roles.Manage, Permissions.Accountant);
    }

    [Fact]
    public void MeterReader_can_view_apartments_and_work_the_meters()
    {
        Assert.Equal(
            [
                Permissions.Apartments.View,
                Permissions.Apartments.Readings,
                Permissions.MeterReadings.View,
                Permissions.MeterReadings.ManualEntry,
                Permissions.MeterReadings.TriggerRead
            ],
            Permissions.MeterReader);
    }
}
