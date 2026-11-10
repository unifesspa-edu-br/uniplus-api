namespace Unifesspa.UniPlus.Autorizacao.Application.Abstractions;

using Unifesspa.UniPlus.Autorizacao.Application.Monitoramento;

/// <summary>A leitura, só de consulta, do estado da governança de perfis que as métricas publicam.</summary>
public interface ILeituraDoMonitoramento
{
    Task<AmostraDaGovernanca> LerAsync(DateTimeOffset agora, CancellationToken cancellationToken = default);
}
