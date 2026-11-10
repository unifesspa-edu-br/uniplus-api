namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Decisão de uma etapa da retomada de um perfil suspenso. Só se acrescenta, nunca se altera: é evidência forense
/// (ADR-0063). Guarda se quem decidiu tinha o perfil e se o perfil era protegido no instante, para que a trilha mostre o
/// que foi avaliado.
/// </summary>
public sealed class DecisaoDaRetomada : IForensicEntity, IRegistroDaTrilhaDeContencao
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid SuspensaoId { get; private init; }

    public EtapaDaVersao Etapa { get; private init; }

    public string DecisorEmissor { get; private init; } = string.Empty;

    public string DecisorSub { get; private init; } = string.Empty;

    public string? DecisorNome { get; private init; }

    public bool DecisorTemConcessao { get; private init; }

    public string Motivo { get; private init; } = string.Empty;

    public bool PerfilProtegidoNaDecisao { get; private init; }

    public DateTimeOffset Instante { get; private init; }

    public int OrdinalLocal { get; private init; }

    public long OrdemNaTrilha { get; private set; }

    public Identidade Decisor => new(DecisorEmissor, DecisorSub, DecisorNome);

    // Construtor de materialização do EF Core.
    private DecisaoDaRetomada()
    {
    }

    internal static DecisaoDaRetomada Registrar(Guid suspensaoId, ContextoDaRetomada contexto, string motivo, bool protegido, int ordinal) =>
        new()
        {
            SuspensaoId = suspensaoId,
            Etapa = contexto.Etapa,
            DecisorEmissor = contexto.Decisor.Emissor,
            DecisorSub = contexto.Decisor.Sub,
            DecisorNome = contexto.Decisor.Nome,
            DecisorTemConcessao = contexto.DecisorTemConcessao,
            Motivo = motivo,
            PerfilProtegidoNaDecisao = protegido,
            Instante = contexto.Instante,
            OrdinalLocal = ordinal,
        };
}
