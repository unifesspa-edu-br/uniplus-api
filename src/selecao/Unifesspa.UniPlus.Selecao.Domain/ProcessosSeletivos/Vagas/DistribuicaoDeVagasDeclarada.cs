namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;

using System.Collections.Frozen;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a configuração declara da distribuição de vagas de uma oferta, antes de virar quadro: a
/// regra de distribuição, as vagas ofertadas base, o percentual da reserva legal (só no ramo da Lei de
/// Cotas), a referência demográfica, a regra e o motor de ajuste e as modalidades selecionadas. Um dado que o
/// cadastro recusou fica ausente e consta em <see cref="Recusados"/>: a conferência só deixa de rodar a regra
/// que lê o valor dele.
/// </summary>
public sealed record DistribuicaoDeVagasDeclarada
{
    /// <summary>A regra de distribuição; nula só quando o cadastro a recusou.</summary>
    public ReferenciaRegra? RegraDeDistribuicao { get; init; }

    public required int VagasOfertadasBase { get; init; }

    public decimal? PercentualDaReservaLegal { get; init; }

    public ReferenciaReservaDemograficaSnapshot? ReferenciaDemografica { get; init; }

    public ReferenciaRegra? RegraDeAjuste { get; init; }

    public ParametrosRegraAjusteDistribuicao? AjusteDoQuadro { get; init; }

    /// <summary>As modalidades que o cadastro aceitou; as recusadas não constam.</summary>
    public required IReadOnlyList<ModalidadeSelecionada> Modalidades { get; init; }

    /// <summary>Os dados que o cadastro recusou.</summary>
    public IReadOnlySet<DadoDaDistribuicaoDeVagas> Recusados { get; init; } = FrozenSet<DadoDaDistribuicaoDeVagas>.Empty;

    /// <summary>O código da regra de distribuição aceita; nulo quando ela foi recusada.</summary>
    public string? CodigoDaRegra =>
        RegraDeDistribuicao is { } regra && !Recusado(DadoDaDistribuicaoDeVagas.RegraDeDistribuicao) ? regra.Codigo : null;

    /// <summary>Se a regra de distribuição foi aceita; só então a declaração tem ramo.</summary>
    public bool RamoDeterminado => CodigoDaRegra is not null;

    public bool NoRamoFederal => CodigoDaRegra is { } codigo && PoliticaDaDistribuicaoDeVagas.EhRegraFederal(codigo);

    public bool NoQuadroFixo => CodigoDaRegra is { } codigo && PoliticaDaDistribuicaoDeVagas.EhQuadroFixo(codigo);

    public bool Recusado(DadoDaDistribuicaoDeVagas dado) => Recusados.Contains(dado);

    /// <exception cref="InvalidOperationException">Algum dado foi recusado ou falta a regra de distribuição.</exception>
    public void ExigirSemRecusados()
    {
        if (Recusados.Count > 0 || RegraDeDistribuicao is null)
        {
            throw new InvalidOperationException("A distribuição só vira configuração ou quadro quando nenhum dado foi recusado.");
        }
    }
}
