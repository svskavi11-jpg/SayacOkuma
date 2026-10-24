using Microsoft.Extensions.Logging;
using Westalife.Application.Common.Enums;
using Westalife.Application.Common.Interfaces;
using Westalife.Application.Common.Models;
using Westalife.Infrastructure.Metering.Connections;
using Westalife.Infrastructure.Metering.Exceptions;
using Westalife.Infrastructure.Metering.Protocol;
using Westalife.Infrastructure.Metering.Resilience;

namespace Westalife.Infrastructure.Metering.Readers;

/// <summary>
/// GERÇEK OKUMA stratejisi: Izar Center'a M-Bus üzerinden bağlanır (USB-Serial veya TCP),
/// her sayaç için SND_NKE → REQ_UD2 → RSP_UD akışını çalıştırır.
/// Kısmi başarı modeli: tek sayacın hatası seansı bozmaz; ham telegram (RawTelegramHex) her yanıtta taşınır.
/// </summary>
public sealed class MbusMeterReader : IMeterReader
{
    private readonly IMbusChannel _channel;
    private readonly MeteringOptions _options;
    private readonly IReadOnlyList<byte> _primaryAddresses;
    private readonly ILogger<MbusMeterReader> _logger;

    public MbusMeterReader(
        IMbusChannel channel,
        MeteringOptions options,
        IReadOnlyList<byte> primaryAddresses,
        ILogger<MbusMeterReader> logger)
    {
        _channel = channel;
        _options = options;
        _primaryAddresses = primaryAddresses;
        _logger = logger;
    }

    public async Task<MeterReadBatch> ReadAllAsync(CancellationToken cancellationToken = default)
    {
        var startedAt = DateTimeOffset.UtcNow;

        if (_primaryAddresses.Count == 0)
        {
            _logger.LogWarning(
                "M-Bus okuma listesi boş: okunacak sayaç birincil adresleri yapılandırılmamış. " +
                "Faz 3'te adresler Meters tablosundan alınacaktır.");
            return new MeterReadBatch { StartedAtUtc = startedAt, Results = [] };
        }

        await _channel.OpenAsync(cancellationToken);

        try
        {
            var results = new List<MeterReadResult>(_primaryAddresses.Count);

            foreach (var address in _primaryAddresses)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(await ReadSingleMeterAsync(address, cancellationToken));
            }

            _logger.LogInformation(
                "M-Bus okuma seansı tamamlandı: {Success} başarılı, {Failed} hatalı, toplam {Total} sayaç.",
                results.Count(r => r.IsSuccess), results.Count(r => !r.IsSuccess), results.Count);

            return new MeterReadBatch { StartedAtUtc = startedAt, Results = results };
        }
        finally
        {
            _channel.Dispose();
        }
    }

    private async Task<MeterReadResult> ReadSingleMeterAsync(byte address, CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await RetryPolicy.ExecuteAsync(
                async _ => await ReadWithProtocolAsync(address, cancellationToken),
                _options.MaxRetryCount, _options.RetryBaseDelayMs, _logger,
                $"Sayaç okuma (adres {address})", cancellationToken);

            return MeterReadResult.Success(snapshot);
        }
        catch (MbusTimeoutException ex)
        {
            _logger.LogWarning(ex, "Sayaç (adres {Address}) zaman aşımı: {Message}", address, ex.Message);
            return MeterReadResult.Failure(null, address, MeterReadErrorCode.Timeout, ex.Message);
        }
        catch (MbusConnectionException ex)
        {
            _logger.LogError(ex, "Sayaç (adres {Address}) bağlantı hatası: {Message}", address, ex.Message);
            return MeterReadResult.Failure(null, address, MeterReadErrorCode.ConnectionFailure, ex.Message);
        }
        catch (MbusProtocolException ex)
        {
            _logger.LogWarning(ex, "Sayaç (adres {Address}) protokol hatası. Ham telegram: {Raw}", address, ex.RawTelegramHex);
            return MeterReadResult.Failure(null, address, MeterReadErrorCode.ProtocolError, ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Sayaç (adres {Address}) beklenmeyen hata.", address);
            return MeterReadResult.Failure(null, address, MeterReadErrorCode.Unknown, ex.Message);
        }
    }

    /// <summary>SND_NKE → REQ_UD2 → RSP_UD protokol akışı.</summary>
    private async Task<MeterSnapshot> ReadWithProtocolAsync(byte address, CancellationToken cancellationToken)
    {
        // 1) Adres resetleme (FCB senkronizasyonu) — ACK (0xE5) beklenir.
        await _channel.SendAsync(MbusFrameBuilder.BuildSndNke(address), cancellationToken);
        var ack = await _channel.ReceiveAsync(_options.ResponseTimeoutMs, cancellationToken);
        if (!MbusFrameParser.IsAck(ack))
        {
            throw new MbusProtocolException(
                $"SND_NKE sonrası ACK beklenirken farklı yanıt geldi ({Convert.ToHexString(ack)}).");
        }

        // 2) REQ_UD2 — değişken veri yapısı talebi.
        await _channel.SendAsync(MbusFrameBuilder.BuildReqUd2(address, frameCountBitSet: true), cancellationToken);

        // 3) RSP_UD — yanıt frame'ini al, doğrula, kayıtlara ayır, snapshot'a eşle.
        var response = await _channel.ReceiveAsync(_options.ResponseTimeoutMs, cancellationToken);
        var frame = MbusFrameParser.ParseResponse(response);
        var records = MbusDataRecordParser.Parse(frame.Data);

        var snapshot = DanfossRecordMapper.MapToSnapshot(records, frame.RawHex, DateTimeOffset.UtcNow);
        if (string.IsNullOrEmpty(snapshot.SerialNumber))
        {
            _logger.LogWarning("Sayaç (adres {Address}) yanıtında ikincil adres kaydı bulunamadı.", address);
        }

        if (snapshot.HasDataError)
        {
            throw new MbusProtocolException("Cihaz veri hatası bildirdi (Operating Time -> Data Error).", frame.RawHex);
        }

        return snapshot;
    }
}
