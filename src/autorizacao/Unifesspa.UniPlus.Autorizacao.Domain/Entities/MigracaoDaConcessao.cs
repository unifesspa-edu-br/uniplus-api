namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Onde uma concessão da versão anterior está na revisão das concessões, desde o envio das decisões da gestão: uma linha por
/// concessão e por revisão. As situações finais não mudam mais; a concessão que ainda não tem linha aguarda o envio de decisões.
/// </summary>
public sealed class MigracaoDaConcessao : EntityBase
{
    public Guid RevisaoId { get; private init; }

    public Guid PerfilId { get; private init; }

    public Guid ConcessaoId { get; private init; }

    public SituacaoDaMigracao Situacao { get; private set; }

    /// <summary>O acréscimo que a migração traria para a concessão é pessoal, identificador ou sensível.</summary>
    public bool ExigeProtecaoDeDados { get; private set; }

    public DateTimeOffset AtualizadaEm { get; private set; }

    // Construtor de materialização do EF Core.
    private MigracaoDaConcessao()
    {
    }

    internal static MigracaoDaConcessao Criar(
        Guid revisaoId, Guid perfilId, Guid concessaoId, SituacaoDaMigracao situacao, bool exigeProtecaoDeDados, DateTimeOffset instante) =>
        new()
        {
            RevisaoId = revisaoId,
            PerfilId = perfilId,
            ConcessaoId = concessaoId,
            Situacao = situacao,
            ExigeProtecaoDeDados = exigeProtecaoDeDados,
            AtualizadaEm = instante,
        };

    internal void Mover(SituacaoDaMigracao situacao, DateTimeOffset instante)
    {
        Situacao = situacao;
        AtualizadaEm = instante;
    }
}
