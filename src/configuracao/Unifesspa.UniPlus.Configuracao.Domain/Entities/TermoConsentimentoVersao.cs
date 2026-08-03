namespace Unifesspa.UniPlus.Configuracao.Domain.Entities;

using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;

/// <summary>
/// Uma versão promovida e imutável de um <see cref="TermoConsentimento"/>
/// (UNI-REQ-0086/RN-COL-05). Corrigir o texto exige um novo rascunho revisado
/// e uma nova promoção — nunca editar esta linha.
/// </summary>
/// <remarks>
/// Implementa <see cref="IForensicEntity"/> per ADR-0063: forensic append-only,
/// deliberadamente NÃO herda <see cref="Kernel.Domain.Entities.EntityBase"/> e NÃO
/// carrega soft-delete — qualquer <c>UPDATE</c>/<c>DELETE</c> em produção é
/// incidente operacional, não fluxo normal.
/// </remarks>
public sealed class TermoConsentimentoVersao : IForensicEntity
{
    public Guid Id { get; private init; } = Guid.CreateVersion7();

    public Guid TermoConsentimentoId { get; private init; }

    public string Texto { get; private init; } = null!;

    public string BaseLegal { get; private init; } = null!;

    public FormaAceite FormaAceite { get; private init; }

    public string Hash { get; private init; } = null!;

    public DateTimeOffset PromovidaEm { get; private init; }

    public string PromovidaPor { get; private init; } = null!;

    // Construtor de materialização do EF Core.
    private TermoConsentimentoVersao()
    {
    }

    /// <summary>Promove um rascunho revisado a versão imutável. Chamado só pelo agregado.</summary>
    internal static TermoConsentimentoVersao Promover(
        Guid termoConsentimentoId,
        string texto,
        string baseLegal,
        FormaAceite formaAceite,
        string hash,
        DateTimeOffset promovidaEm,
        string promovidaPor)
    {
        return new TermoConsentimentoVersao
        {
            TermoConsentimentoId = termoConsentimentoId,
            Texto = texto,
            BaseLegal = baseLegal,
            FormaAceite = formaAceite,
            Hash = hash,
            PromovidaEm = promovidaEm,
            PromovidaPor = promovidaPor,
        };
    }
}
