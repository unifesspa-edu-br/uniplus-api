namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;

using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Fatos;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Os itens do painel sobre a distribuição de vagas, a exclusividade de cota e ação afirmativa e o grafo de dependência.</summary>
internal static class ItensDaDistribuicaoDeVagas
{
    internal static IReadOnlyList<ItemConformidade> Avaliar(EstadoDaConfiguracao estado) =>
    [
        new ItemConformidade("modalidade_regra_incoerente_com_a_natureza", DimensaoConformidade.DistribuicaoVagas, "Modalidades: a regra de cada cota da lei exige a opção pelas cotas, e a de cada ação afirmativa a recusa", RegrasDaDistribuicaoDeVagas.RegraIncoerenteComANatureza(estado.DistribuicaoVagas) is null),
        new ItemConformidade("modalidade_remanejamento_incoerente_com_a_natureza", DimensaoConformidade.DistribuicaoVagas, "Modalidades: a ampla concorrência não remaneja as vagas ociosas, e cada ação afirmativa as remaneja para um destino único ou para o par", RegrasDaDistribuicaoDeVagas.RemanejamentoIncoerenteComANatureza(estado.DistribuicaoVagas) is null),
        new ItemConformidade("modalidade_cotas_da_lei_diferentes_entre_ofertas", DimensaoConformidade.DistribuicaoVagas, "Modalidades: todos os cursos ofertam as mesmas cotas da lei", PendenciasDaDistribuicaoDeVagas.PendenciaDasCotasDaLeiEntreOfertas(estado) is null),
        new ItemConformidade("modalidade_reserva_pcd_sem_cota_pcd_da_lei", DimensaoConformidade.DistribuicaoVagas, "Modalidades: com cota da lei, a reserva de pessoa com deficiência de ampla concorrência vem com a cota da lei para ela", PendenciasDaDistribuicaoDeVagas.PendenciaDaReservaPcdSemCotaPcdDaLei(estado) is null),
        new ItemConformidade("derivacao_cota_e_acao_afirmativa_juntas", DimensaoConformidade.ColetaDeFatos, "Modalidades: cota da lei e ação afirmativa não se aplicam ao mesmo candidato", ExclusividadeEntreCotaEAcaoAfirmativa.PendenciaDaExclusividadeEntreCotaEAcaoAfirmativa(estado) is null),
        new ItemConformidade("grafo_dependencia_com_ciclo", DimensaoConformidade.ColetaDeFatos, "Grafo de dependência conjunto: sem ciclo", PendenciasDosFatos.PendenciaDoGrafoConjunto(estado) is null),
    ];
}
