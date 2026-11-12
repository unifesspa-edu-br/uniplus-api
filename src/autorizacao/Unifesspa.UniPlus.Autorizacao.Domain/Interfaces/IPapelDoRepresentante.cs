namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O papel de Representante do controlador como um todo: a trava transacional única que serializa quem concede, aprova a
/// segunda etapa ou revoga a concessão do perfil dele, e a leitura de quem o exerce agora (ADR-0162).
/// </summary>
public interface IPapelDoRepresentante
{
    /// <summary>
    /// Trava o perfil de Representante até o fim da transação. Toma-se antes da trava de qualquer pessoa ou concessão, e
    /// antes de ler o perfil para conceder, para que duas designações simultâneas não leiam ambas que não há Representante.
    /// </summary>
    Task TravarAsync(CancellationToken cancellationToken = default);

    /// <summary>Alguém além de <paramref name="exceto"/>, quando informado, tem concessão vigente de Representante no instante.</summary>
    Task<bool> HaOutroVigenteAsync(Identidade? exceto, DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>Quem tem concessão vigente de Representante no instante; a vencida ou revogada não conta.</summary>
    Task<IReadOnlySet<Identidade>> ListarVigentesAsync(DateTimeOffset agora, CancellationToken cancellationToken = default);

    /// <summary>A pessoa tem concessão vigente de Representante no instante; a vencida ou revogada não conta.</summary>
    Task<bool> EVigenteAsync(Identidade pessoa, DateTimeOffset agora, CancellationToken cancellationToken = default);
}
