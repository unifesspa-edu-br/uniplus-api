namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O estado de entrega do aviso de um fato da reclassificação, separado do fato: a unicidade por origem e identificador
/// do fato garante que cada fato avise uma vez.
/// </summary>
public sealed class AvisoDaReclassificacao
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public OrigemDoAviso Origem { get; private init; }

    public Guid OrigemId { get; private init; }

    public DateTimeOffset EnviadoEm { get; private init; }

    // Construtor de materialização do EF Core.
    private AvisoDaReclassificacao()
    {
    }

    public static AvisoDaReclassificacao Registrar(OrigemDoAviso origem, Guid origemId, DateTimeOffset instante) =>
        new() { Origem = origem, OrigemId = origemId, EnviadoEm = instante };
}
