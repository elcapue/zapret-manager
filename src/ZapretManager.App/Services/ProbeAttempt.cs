namespace ZapretManager.App.Services;

public enum ProbeOutcome
{
    /// <summary>Сервер ответил (любой HTTP-код) и тело прочитано без обрыва.</summary>
    Ok,

    /// <summary>Сброс, таймаут или обрыв передачи — то, что zapret может исправить.</summary>
    Failed,

    /// <summary>Хост не резолвится. zapret DNS не чинит, поэтому в счёт стратегии не идёт.</summary>
    DnsFailure,

    /// <summary>Невалидный сертификат (подмена DNS/заглушка провайдера). В счёт стратегии не идёт.</summary>
    CertificateError
}

/// <param name="ResolvedViaFallbackDns">Адрес найден не DNS провайдера, а через DNS-over-HTTPS.</param>
public sealed record ProbeAttempt(
    StrategyTestTarget Target,
    ProbeOutcome Outcome,
    TimeSpan Latency,
    bool ResolvedViaFallbackDns = false)
{
    /// <summary>Попытка отражает работу DPI-обхода и учитывается в оценке стратегии.</summary>
    public bool IsCounted => Outcome is ProbeOutcome.Ok or ProbeOutcome.Failed;
}
