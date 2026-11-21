namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Conformidade;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma.ModeloDeFase;

/// <summary>
/// As declarações que a gravação recusaria, carregadas do banco, nunca tiram um item do painel: os vinte e quatro saem, e fica
/// vermelho exatamente o item da regra que a declaração viola, ou o que não se consegue conferir por causa dela.
/// </summary>
public sealed class ComposicaoDosItensTests
{
    private static readonly DateTimeOffset Inicio = new(2026, 3, 2, 12, 0, 0, TimeSpan.Zero);

    private static IReadOnlyList<ItemConformidade> Itens(ProcessoSeletivo processo) =>
        ItensDoCronograma.Avaliar(processo.Declaracao(), CadastrosDeTeste.Para(processo));

    private static string[] Pendentes(IReadOnlyList<ItemConformidade> itens) => [.. itens.Where(static item => !item.Ok).Select(static item => item.Codigo)];

    private static FaseCronograma FaseCarregada(string codigo, int ordem, DateTimeOffset inicio, DateTimeOffset fim, bool comRecurso = false)
    {
        ProdutoDaFase preliminar = Preliminar($"RESULTADO_{codigo}");

        return FaseCronograma.Reidratar(
            Guid.CreateVersion7(),
            new OrigemCanonicaDaFase(Guid.CreateVersion7(), codigo, OrigemDataFase.NoEdital, false, false, false, false),
            new DecisoesDeCronogramaDaFase(ordem, inicio, fim, UnidadesDeTeste.Ceps),
            new PublicacoesDaFase([preliminar, Definitivo($"RESULTADO_{codigo}")], false, comRecurso ? RecursoAncoradoEm(preliminar.Id) : null),
            []);
    }

    private static EtapaProcesso EtapaCarregada(string nome, int ordem, string faseCodigo, CaraterEtapa carater = CaraterEtapa.Classificatoria, decimal? peso = 1m) =>
        ApoioDeEtapaReidratada.DeProvaObjetiva(Guid.CreateVersion7(), nome, ordem, faseCodigo, peso ?? 0m, carater: carater);

    [Fact(DisplayName = "O processo conforme tem os trinta e três itens do cronograma, todos conformes")]
    public void Itens_ProcessoConforme_TodosVerdes()
    {
        IReadOnlyList<ItemConformidade> itens = Itens(ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme());

        itens.Should().HaveCount(33);
        Pendentes(itens).Should().BeEmpty();
    }

    [Fact(DisplayName = "A fase carregada com a âncora do recurso inválida deixa vermelhos o item da âncora e o do recurso sem banca de análise, e nenhum outro")]
    public void Itens_FaseComAncoraInvalida_AncoraERecursoSemBancaPendentes()
    {
        ProcessoSeletivo processo = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme();
        FaseCronograma fase = FaseCarregada("RECURSO", 2, Inicio, Inicio.AddDays(9), comRecurso: true);
        ProcessoCarregadoDoBanco.SemOsProdutos(fase);
        ProcessoCarregadoDoBanco.Fase(processo, fase);

        IReadOnlyList<ItemConformidade> itens = Itens(processo);

        itens.Should().HaveCount(33);
        Pendentes(itens).Should().Equal("cronograma_ancora_do_recurso", "cronograma_recurso_com_banca_que_julga");
    }

    [Fact(DisplayName = "A fase carregada com a janela invertida deixa pendentes o item das declarações das fases e o da janela da etapa que a declara")]
    public void Itens_FaseComJanelaInvertida_ItemDaJanelaDaEtapaVermelho()
    {
        ProcessoSeletivo processo = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme();
        ProcessoCarregadoDoBanco.Fase(processo, FaseCarregada("PROVA", 2, Inicio.AddDays(9), Inicio));
        EtapaProcesso etapa = EtapaCarregada("Redação", 2, "PROVA");
        etapa.DefinirJanelaEParecer(Inicio.AddDays(2), Inicio.AddDays(3), emiteParecerIndividual: false).IsSuccess.Should().BeTrue();
        ProcessoCarregadoDoBanco.Etapa(processo, etapa);

        IReadOnlyList<ItemConformidade> itens = Itens(processo);

        itens.Should().HaveCount(33);
        Pendentes(itens).Should().Equal("cronograma_fases_declaradas", "cronograma_etapa_fora_da_janela_da_fase");
    }

    [Fact(DisplayName = "A etapa carregada com a avaliação recusada deixa pendente só o item das declarações das etapas")]
    public void Itens_EtapaComAvaliacaoRecusada_SoOItemDasDeclaracoesPendente()
    {
        ProcessoSeletivo processo = ApoioDaPublicacaoDoProcessoSeletivo.NovoProcessoConforme();
        ProcessoCarregadoDoBanco.Etapa(processo, EtapaCarregada("Entrevista", 2, "RESULTADO_FINAL", CaraterEtapa.Eliminatoria, peso: 3m));

        IReadOnlyList<ItemConformidade> itens = Itens(processo);

        itens.Should().HaveCount(33);
        Pendentes(itens).Should().Equal("etapas_declaradas");
    }
}
