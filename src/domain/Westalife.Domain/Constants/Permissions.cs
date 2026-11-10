namespace Westalife.Domain.Constants;

/// <summary>Personel yetki kodları. Sakin portalı bu kataloğa girmez.</summary>
public static class Permissions
{
    public static class Apartments
    {
        public const string View = "Apartments.View";
        public const string Readings = "Apartments.Readings";
        public const string Analysis = "Apartments.Analysis";
        public const string Create = "Apartments.Create";
        public const string Edit = "Apartments.Edit";
        public const string Delete = "Apartments.Delete";
        public const string ManageResident = "Apartments.ManageResident";
    }

    public static class Invoices
    {
        public const string View = "Invoices.View";
        public const string Create = "Invoices.Create";
        public const string Edit = "Invoices.Edit";
        public const string Cancel = "Invoices.Cancel";
        public const string Calculate = "Invoices.Calculate";
    }

    public static class Payments
    {
        public const string View = "Payments.View";
        public const string Create = "Payments.Create";
        public const string Cancel = "Payments.Cancel";
    }

    public static class MeterReadings
    {
        public const string View = "MeterReadings.View";
        public const string ManualEntry = "MeterReadings.ManualEntry";
        public const string TriggerRead = "MeterReadings.TriggerRead";
    }

    public static class Budget
    {
        public const string View = "Budget.View";
        public const string Create = "Budget.Create";
        public const string Edit = "Budget.Edit";
        public const string Transfer = "Budget.Transfer";
    }

    public static class Bank
    {
        public const string View = "Bank.View";
        public const string Manage = "Bank.Manage";
    }

    public static class Reports
    {
        public const string GeneralAssembly = "Reports.GeneralAssembly";
        public const string IncomeExpense = "Reports.IncomeExpense";
        public const string ApartmentTracking = "Reports.ApartmentTracking";
        public const string FuelBreakdown = "Reports.FuelBreakdown";
        public const string BankControl = "Reports.BankControl";
        public const string GasNotifications = "Reports.GasNotifications";

        public static readonly string[] Features =
        [
            GeneralAssembly, IncomeExpense, ApartmentTracking,
            FuelBreakdown, BankControl, GasNotifications,
        ];
    }

    public static class Notifications
    {
        public const string View = "Notifications.View";
        public const string Send = "Notifications.Send";
    }

    public static class Settings
    {
        public const string General = "Settings.General";
        public const string Notification = "Settings.Notification";
        public const string MBus = "Settings.MBus";
        public const string ManageUsers = "Settings.ManageUsers";

        public static readonly string[] Features = [General, Notification, MBus];
    }

    /// <summary>Eski modül kodları. Katalogda yok; açılışta yeni kodlara taşınır.</summary>
    public static class Legacy
    {
        public const string ReportsView = "Reports.View";
        public const string ReportsDownload = "Reports.Download";
        public const string SettingsView = "Settings.View";
        public const string SettingsEdit = "Settings.Edit";
    }

    public static class Roles
    {
        public const string View = "Roles.View";
        public const string Manage = "Roles.Manage";
    }

    public sealed record Item(string Code, string Group, string Label);

    public static readonly Item[] Catalog =
    [
        new(Apartments.View, "Daireler", "Daireleri gör"),
        new(Apartments.Readings, "Daireler", "Okuma listesi"),
        new(Apartments.Analysis, "Daireler", "Tüketim analizi"),
        new(Apartments.Create, "Daireler", "Daire ekle"),
        new(Apartments.Edit, "Daireler", "Daire düzenle"),
        new(Apartments.Delete, "Daireler", "Daire sil"),
        new(Apartments.ManageResident, "Daireler", "Sakin ve portal hesabı"),
        new(Invoices.View, "Finans", "Faturaları gör"),
        new(Invoices.Create, "Finans", "Fatura kes"),
        new(Invoices.Edit, "Finans", "Fatura düzenle"),
        new(Invoices.Cancel, "Finans", "Fatura iptal"),
        new(Invoices.Calculate, "Finans", "Isınma hesapla"),
        new(Payments.View, "Finans", "Tahsilatları gör"),
        new(Payments.Create, "Finans", "Tahsilat gir"),
        new(Payments.Cancel, "Finans", "Tahsilat iptal"),
        new(Budget.View, "Finans", "Bütçeyi gör"),
        new(Budget.Create, "Finans", "Bütçe kalemi ekle"),
        new(Budget.Edit, "Finans", "Bütçe kalemi düzenle"),
        new(Budget.Transfer, "Finans", "Bütçe virmanı"),
        new(Bank.View, "Finans", "Banka hesaplarını gör"),
        new(Bank.Manage, "Finans", "Banka hesabı yönet"),
        new(Reports.GeneralAssembly, "Raporlar", "Genel Kurul / Denetim"),
        new(Reports.IncomeExpense, "Raporlar", "Gelir-Gider"),
        new(Reports.ApartmentTracking, "Raporlar", "Daire Takip"),
        new(Reports.FuelBreakdown, "Raporlar", "Yakıt Dökümü"),
        new(Reports.BankControl, "Raporlar", "Banka Kontrolü"),
        new(Reports.GasNotifications, "Raporlar", "Doğalgaz Bildirimleri"),
        new(MeterReadings.View, "Sayaç", "Okumaları gör"),
        new(MeterReadings.ManualEntry, "Sayaç", "Manuel okuma"),
        new(MeterReadings.TriggerRead, "Sayaç", "Sayaç okut"),
        new(Notifications.View, "Bildirimler", "Bildirimleri gör"),
        new(Notifications.Send, "Bildirimler", "Bildirim gönder"),
        new(Settings.General, "Ayarlar", "Genel ayarlar"),
        new(Settings.Notification, "Ayarlar", "Bildirim ayarları"),
        new(Settings.MBus, "Ayarlar", "M-Bus bağlantısı"),
        new(Settings.ManageUsers, "Ayarlar", "Personel hesabı aç"),
        new(Roles.View, "Roller", "Rolleri Görme"),
        new(Roles.Manage, "Roller", "Rol ve Yetki Yönetimi")
    ];

    public static readonly string[] All = Catalog.Select(item => item.Code).ToArray();

    public static readonly string[] Accountant =
    [
        Invoices.View, Invoices.Create, Invoices.Edit, Invoices.Cancel, Invoices.Calculate,
        Payments.View, Payments.Create, Payments.Cancel,
        Budget.View, Budget.Create, Budget.Edit, Budget.Transfer,
        Bank.View, Bank.Manage,
        Reports.GeneralAssembly, Reports.IncomeExpense, Reports.ApartmentTracking,
        Reports.FuelBreakdown, Reports.BankControl, Reports.GasNotifications,
    ];

    public static readonly string[] MeterReader =
    [
        Apartments.View, Apartments.Readings,
        MeterReadings.View, MeterReadings.ManualEntry, MeterReadings.TriggerRead
    ];

    public static bool IsKnown(string code) => All.Contains(code);
}
