using MediatR;

namespace Westalife.Application.Features.GasInvoices.Commands.AddGasInvoice;

/// <summary>
/// Akmercan bina faturasının sisteme girişi. KDV ve geçişme bedeli dahil TOPLAM tutar
/// 30/70 dağıtımına girecektir (S1 kararı); kalem tutarları denetim için ayrıca saklanır.
/// </summary>
public sealed record AddGasInvoiceCommand(
    string InvoiceNumber,
    string SubscriberNumber,
    string MasterMeterSerial,
    DateTimeOffset ReadingDateFrom,
    DateTimeOffset ReadingDateTo,
    DateTimeOffset IssuedOn,
    decimal CorrectedVolumeM3,
    decimal TotalEnergyKWh,
    decimal PressureCorrectionFactor,
    decimal ConsumptionAmount,
    decimal ConsumptionVat,
    decimal DistributionAmount,
    decimal DistributionVat,
    decimal TotalAmount,
    string? Note = null,
    DateOnly? DueDate = null) : IRequest<Guid>;
