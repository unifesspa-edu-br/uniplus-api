namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.Entities;

/// <summary>
/// Onde uma concessão da versão anterior está na janela de migração, desde o envio do lote da gestão: uma linha por
/// concessão e por janela. As situações finais não mudam mais; a concessão que ainda não tem linha aguarda o lote.
/// </summary>
public sealed class MigracaoDaConcessao : EntityBase
{
    public Guid JanelaId { get; private init; }

    public Guid PerfilId { get; private init; }

    public Guid ConcessaoId { get; private init; }

    public SituacaoDaMigracao Situacao { get; private set; }

    /// <summary>O acréscimo que a migração traria para a concessão é pessoal, identificador ou sensível.</summary>
    public bool ExigeProtecaoDeDados { get; private set; }

    /// <summary>Por que a proteção de dados não decide a concessão; nulo quando decide ou o acréscimo não a exige.</summary>
    public string? Dispensa { get; private set; }

    public DateTimeOffset AtualizadaEm { get; private set; }

    // Construtor de materialização do EF Core.
    private MigracaoDaConcessao()
    {
    }

    internal static MigracaoDaConcessao Criar(
        Guid janelaId, Guid perfilId, Guid concessaoId, SituacaoDaMigracao situacao, bool exigeProtecaoDeDados, DateTimeOffset instante) =>
        new()
        {
            JanelaId = janelaId,
            PerfilId = perfilId,
            ConcessaoId = concessaoId,
            Situacao = situacao,
            ExigeProtecaoDeDados = exigeProtecaoDeDados,
            AtualizadaEm = instante,
        };

    internal void Mover(SituacaoDaMigracao situacao, string? dispensa, DateTimeOffset instante)
    {
        Situacao = situacao;
        Dispensa = dispensa;
        AtualizadaEm = instante;
    }
}
