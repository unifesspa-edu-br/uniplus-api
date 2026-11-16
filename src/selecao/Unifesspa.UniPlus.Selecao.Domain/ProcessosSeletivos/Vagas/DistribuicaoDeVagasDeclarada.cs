namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a configuração declara da distribuição de vagas de uma oferta, antes de virar quadro: a
/// regra de distribuição, as vagas ofertadas base, o percentual da reserva legal (só no ramo da Lei de
/// Cotas), a referência demográfica, a regra e o motor de ajuste e as modalidades selecionadas.
/// </summary>
public sealed record DistribuicaoDeVagasDeclarada
{
    public required ReferenciaRegra RegraDeDistribuicao { get; init; }

    public required int VagasOfertadasBase { get; init; }

    public decimal? PercentualDaReservaLegal { get; init; }

    public ReferenciaReservaDemograficaSnapshot? ReferenciaDemografica { get; init; }

    public ReferenciaRegra? RegraDeAjuste { get; init; }

    public ArgsRegraAjusteDistribuicao? AjusteDoQuadro { get; init; }

    public required IReadOnlyList<ModalidadeSelecionada> Modalidades { get; init; }
}
