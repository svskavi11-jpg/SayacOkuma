using Westalife.Application.Common.Models;

namespace Westalife.Application.Common.Interfaces;

/// <summary>
/// Factory Pattern: yapılandırmaya göre uygun IMeterReader stratejisini üretir.
/// </summary>
public interface IMeterReaderFactory
{
    IMeterReader Create(MeteringOptions options);
}
