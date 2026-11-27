namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Bancas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static ApoioDasBancasDoProcesso;

/// <summary>
/// A exigência de banca de análise de recursos, na fase e na etapa, e a regra de deliberação de cada banca: a mesma pendência
/// alimenta a conferência de congelamento e o painel.
/// </summary>
public sealed class ExigenciaDeBancaDeRecursoDoProcessoTests
{
    [Fact(DisplayName = "Fase com recurso sem banca de análise de recursos é recusada, citando a fase")]
    public void FaseComRecursoSemBanca_RecusaCitandoAFase()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(
            fase: ProcessoConformeFactory.FaseConforme(RegraDeRecurso(), bancas: []));

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("Cronograma.RecursoSemBanca");
        pendencia.Message.Should().Contain("'Resultado final'").And.NotContain("RESULTADO_FINAL");
    }

    [Fact(DisplayName = "Fase com recurso e banca de análise de recursos declarada é aceita")]
    public void FaseComRecursoEBanca_Aceita()
    {
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(RegraDeRecurso()));

        Pendencia(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Etapa com recurso e banca de recursos só na fase dela é aceita")]
    public void EtapaComRecursoEBancaNaFase_Aceita()
    {
        ProcessoSeletivo processo = ProcessoComEtapas([BancasDeTeste.DeRecursosDaFase()], ComRecurso(NovaEtapa("Redação", 1)));

        Pendencia(processo).Should().BeNull();
    }

    [Fact(DisplayName = "Etapa com recurso e banca de recursos só em outra etapa é recusada, citando a etapa")]
    public void EtapaComRecursoEBancaSoEmOutraEtapa_RecusaCitandoAEtapa()
    {
        EtapaProcesso semBanca = ComRecurso(NovaEtapa("Redação", 1));
        EtapaProcesso comBanca = ComRecurso(NovaEtapa("Objetiva", 2));
        ProcessoSeletivo processo = ProcessoComEtapas(null, semBanca, comBanca);
        processo.Etapas.Single(e => e.Nome == "Objetiva").DefinirBancas([BancasDeTeste.DeRecursosDaEtapa()]);

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("Cronograma.RecursoSemBanca");
        pendencia.Message.Should().Contain("A etapa na posição 1 admite recurso");
    }

    // A fase criada recusa a declaração dos candidatos atendidos na banca de recursos; o estado violado chega pela reidratação, como o que o banco hidrata.
    private static FaseCronograma FaseReidratadaComBancaDeRecursosComCandidatosDeclarados(BancaRequerida banca) => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        Guid.CreateVersion7(),
        OrigensCanonicasDeTeste.Da(Guid.CreateVersion7(), "RESULTADO_FINAL", OrigemDataFase.NoEdital, true, false, true, false),
        new DecisoesDeCronogramaDaFase(
            1, new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(
            [
                ProdutoDaFase.Reidratar(ProcessoConformeFactory.ProdutoPreliminarId, "RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Preliminar),
                ProdutoDaFase.Criar("RESULTADO_CLASSIFICACAO", PapelDaPublicacaoPrevista.Definitivo),
            ],
            false,
            RegraDeRecurso()),
        [banca]);

    [Fact(DisplayName = "Banca de análise de recursos com candidatos atendidos declarados é recusada")]
    public void BancaDeRecursosComCandidatosDeclarados_Recusa()
    {
        BancaRequerida comCandidatosDeclarados = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            TiposDeBancaCopiadosDeTeste.DeRecursos(Tipo),
            [CategoriaJulgada.Criar(Guid.CreateVersion7(), "RENDA", "RENDA")], BancasDeTeste.Regra());
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: FaseReidratadaComBancaDeRecursosComCandidatosDeclarados(comCandidatosDeclarados));

        Pendencia(processo)!.Code.Should().Be("Banca.CandidatosAtendidosEmBancaDeRecursos");
        ExigenciaDeBancaDeRecurso.CandidatosAtendidosNaDeclaracao(TiposDeBancaCopiadosDeTeste.DeRecursos(), 1, 1)!.Code
            .Should().Be("Banca.CandidatosAtendidosEmBancaDeRecursos");
        ExigenciaDeBancaDeRecurso.CandidatosAtendidosNaDeclaracao(TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental), 1, 1).Should().BeNull();
    }

    [Fact(DisplayName = "Banca da etapa sem regra de deliberação é recusada, citando o nome da banca")]
    public void BancaDaEtapaSemRegra_Recusa()
    {
        EtapaProcesso etapa = NovaEtapa("Redação", 1);
        ProcessoSeletivo processo = ProcessoComEtapas(null, etapa);
        etapa.DefinirBancas([BancaDaEtapa.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.CorrecaoDeRedacoes, Tipo))]);

        DomainError? pendencia = Pendencia(processo);

        pendencia!.Code.Should().Be("Banca.RegraDeDeliberacaoObrigatoria");
        pendencia.Message.Should().Contain("A banca 'Banca de correção de redações 1' da etapa na posição 1");
    }

    [Fact(DisplayName = "Banca da fase sem regra de deliberação é recusada")]
    public void BancaDaFaseSemRegra_Recusa()
    {
        BancaRequerida semRegra = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, Tipo), []);
        ProcessoSeletivo processo = ProcessoConformeFactory.Criar(fase: ProcessoConformeFactory.FaseConforme(bancas: [semRegra]));

        Pendencia(processo)!.Code.Should().Be("Banca.RegraDeDeliberacaoObrigatoria");
    }
}
