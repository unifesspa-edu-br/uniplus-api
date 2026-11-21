namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre a distribuição de vagas, a exclusividade de cota e ação afirmativa e o grafo de dependência.</summary>
internal static class ItensDaDistribuicaoDeVagas
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(ConfiguracaoDeclarada configuracao) =>
    [
        new ItemConformidade("modalidade_regra_incoerente_com_a_natureza", DimensaoConformidade.DistribuicaoVagas, "Modalidades: a regra de cada cota da lei exige a opção pelas cotas, e a de cada ação afirmativa a recusa, salvo a reserva de vagas para pessoa com deficiência, que não a cita", RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(configuracao.Vagas.DistribuicaoVagas) is null),
        new ItemConformidade("modalidade_remanejamento_incoerente_com_a_natureza", DimensaoConformidade.DistribuicaoVagas, "Modalidades: a ampla concorrência não remaneja as vagas ociosas, e cada ação afirmativa as remaneja para um destino único ou para o par", RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(configuracao.Vagas.DistribuicaoVagas) is null),
        new ItemConformidade("modalidade_cotas_da_lei_diferentes_entre_ofertas", DimensaoConformidade.DistribuicaoVagas, "Modalidades: todos os cursos ofertam as mesmas cotas da lei", PendenciasDaDistribuicaoDeVagas.PendenciaDasCotasDaLeiEntreOfertas(configuracao) is null),
        new ItemConformidade("derivacao_cota_e_acao_afirmativa_juntas", DimensaoConformidade.ColetaDeFatos, "Modalidades: quem concorre a cota da lei não concorre a outra ação afirmativa além da reserva de vagas para pessoa com deficiência", ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(configuracao) is null),
        new ItemConformidade("grafo_dependencia_com_ciclo", DimensaoConformidade.ColetaDeFatos, "Grafo de dependência conjunto: sem ciclo", PendenciasDosFatos.PendenciaDoGrafoConjunto(configuracao) is null),
    ];
}
