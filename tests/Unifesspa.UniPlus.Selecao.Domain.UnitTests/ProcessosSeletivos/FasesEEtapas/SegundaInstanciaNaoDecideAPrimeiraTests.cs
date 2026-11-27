namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.ApoioDaSegundaInstancia;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.CenarioDeProcesso;
using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.FasesEEtapas.RecursosParaQuemJulga;

using Nivel = Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas.QuemJulgaORecurso.Nivel;

/// <summary>
/// A banca de 2ª instância não decide quem julga o recurso em 1ª instância: sozinha, deixa o recurso sem banca que o julgue; ao
/// lado da dedicada ou da avaliadora, não entra entre as que o julgam e não se junta a elas em grupo de candidatos atendidos.
/// </summary>
public sealed class SegundaInstanciaNaoDecideAPrimeiraTests
{
    private const string CodigoDoRecursoSemBanca = "Cronograma.RecursoSemBanca";
    private const string ItemDoRecursoComBancaQueJulga = "cronograma_recurso_com_banca_que_julga";
    private const string Papel = "Julga o recurso em 1ª instância";

    private static readonly string[] CodigosDaSegundaInstancia =
    [
        "Banca.FaseSemBancaDaSegundaInstancia",
        "Banca.EtapaSemBancaDaSegundaInstancia",
        "Banca.FaseComBancaDaSegundaInstanciaSemRecursoParaJulgar",
        "Banca.EtapaComBancaDaSegundaInstanciaSemRecursoParaJulgar",
    ];

    private static readonly TipoDeBancaCopiado Entrevista = Avaliadora(TipoDeBanca.Entrevista, false, TipoMotivoDeDecisao.NotaDaEtapa);

    private static (string? Campo, string Codigo)[] SemBanca(Cenario cenario) =>
        Causas(QuemJulgaORecurso.SemBanca(cenario.Cronograma, cenario.Etapas));

    [Fact(DisplayName = "A fase com recurso e só a banca de 2ª instância continua com recurso sem banca que o julgue em 1ª instância, com o texto de sempre")]
    public void Fase_SoComBancaDeSegundaInstancia_RecursoSemBanca()
    {
        ModeloDeFase fase = FaseComRecurso(Fase, [Procedimento(TipoMotivoDeDecisao.HomologacaoDasInscricoes)], true, DaSegundaInstanciaNaFase());

        Cenario cenario = Montar([fase], []);

        FieldError recusa = QuemJulgaORecurso.SemBanca(cenario.Cronograma, cenario.Etapas).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("fases[0].bancasRequeridas");
        recusa.Error.Code.Should().Be(CodigoDoRecursoSemBanca);
        recusa.Error.Message.Should().Be(
            $"A fase '{NomeDaFase}' admite recurso de Homologação das inscrições, e nenhuma banca prevista o julga em 1ª instância: a fase não tem banca com o papel '{Papel}', e nem a fase nem as etapas dela têm banca que tenha avaliado esse item. Acrescente uma dessas bancas.");
    }

    [Fact(DisplayName = "A etapa com recurso e só a banca de 2ª instância continua com recurso sem banca que o julgue em 1ª instância")]
    public void Etapa_SoComBancaDeSegundaInstancia_RecursoSemBanca()
    {
        Cenario cenario = Montar([FaseSemRecurso()], [EtapaQueAdmite(DaSegundaInstanciaNaEtapa())]);

        FieldError recusa = QuemJulgaORecurso.SemBanca(cenario.Cronograma, cenario.Etapas).Should().ContainSingle().Subject;

        recusa.Field.Should().Be("etapas[0].bancas");
        recusa.Error.Code.Should().Be(CodigoDoRecursoSemBanca);
        recusa.Error.Message.Should().Be(
            $"A etapa na posição 1 admite recurso de Nota da etapa, e nenhuma banca prevista o julga em 1ª instância: nem a etapa nem a fase '{NomeDaFase}', em que ela acontece, têm banca com o papel '{Papel}', e a etapa não tem banca que tenha avaliado esse item. Acrescente uma dessas bancas.");
    }

    [Fact(DisplayName = "O recurso sem procedimento declarado, com só a banca de 2ª instância, continua sem banca que o julgue")]
    public void RecursoSemProcedimento_SoComBancaDeSegundaInstancia_RecursoSemBanca()
    {
        Cenario cenario = Montar([FaseComRecurso(Fase, [], true, DaSegundaInstanciaNaFase())], []);

        QuemJulgaORecurso.SemBanca(cenario.Cronograma, cenario.Etapas).Should().ContainSingle().Which.Error.Message.Should().Be(
            $"A fase '{NomeDaFase}' admite recurso sem procedimento declarado, e só uma banca com o papel '{Papel}' pode julgá-lo. Acrescente essa banca.");
    }

    [Fact(DisplayName = "Pela conferência da configuração, só a banca de 2ª instância deixa o recurso sem banca, o item do painel vermelho e sem causa da 2ª instância")]
    public void CaminhoDaConferencia_SoComBancaDeSegundaInstancia_RecursoSemBancaEItemVermelho()
    {
        Cenario cenario = Montar([FaseQueAdmite(DaSegundaInstanciaNaFase())], []);
        ContextoDaConferencia contexto = ContextoDaConferencia.De(cenario.Configuracao, CadastrosDeTeste.Padrao);
        AssuntoDaConferencia bancas = CausasQueImpedemCongelar.Assuntos.Single(static assunto => assunto.Nome == NomeDoAssunto.Bancas);

        Causas(bancas.Causas(contexto).Where(static causa => causa.Error.Code == CodigoDoRecursoSemBanca))
            .Should().Equal(("fases[0].bancasRequeridas", CodigoDoRecursoSemBanca));
        bancas.Causas(contexto).Select(static causa => causa.Error.Code).Should().NotContain(CodigosDaSegundaInstancia);
        ConferenciaDasBancas.Itens(OrigemCandidatos.InscricaoPropria, contexto.Cronograma, contexto.Etapas, CadastrosDeTeste.Padrao.Corretores)
            .Single(static item => item.Codigo == ItemDoRecursoComBancaQueJulga).Ok.Should().BeFalse();
    }

    [Fact(DisplayName = "A avaliadora de outro item e a banca de 2ª instância deixam o recurso sem banca que o julgue")]
    public void AvaliadoraDeOutroItemEBancaDeSegundaInstancia_RecursoSemBanca()
    {
        TipoDeBancaCopiado deOutroItem = Avaliadora(TipoDeBanca.Heteroidentificacao, false, TipoMotivoDeDecisao.Heteroidentificacao);
        ModeloDeEtapa etapa = EtapaComRecurso("Prova", Fase, DaNota, false, NaEtapa(deOutroItem), DaSegundaInstanciaNaEtapa());

        SemBanca(Montar([FaseSemRecurso()], [etapa])).Should().Equal(("etapas[0].bancas", CodigoDoRecursoSemBanca));
    }

    [Fact(DisplayName = "A classificação com a dedicada e a banca de 2ª instância, sem avaliadora, tem quem a julgue")]
    public void Classificacao_ComDedicadaEBancaDeSegundaInstancia_SemRecusa()
    {
        ModeloDeFase fase = FaseComRecurso(Fase, [Procedimento(TipoMotivoDeDecisao.Classificacao)], true, BancasDeTeste.DeRecursosDaFase(), DaSegundaInstanciaNaFase());

        SemBanca(Montar([fase], [])).Should().BeEmpty();
    }

    [Fact(DisplayName = "O recurso da etapa é julgado pela dedicada da etapa, e a banca de 2ª instância não entra entre as que o julgam em 1ª instância")]
    public void RecursoDaEtapa_ComDedicadasEBancaDeSegundaInstancia_JulgaADedicadaDaEtapa()
    {
        ModeloDeEtapa etapa = EtapaQueAdmite(BancasDeTeste.DeRecursosDaEtapa(), DaSegundaInstanciaNaEtapa());
        Cenario cenario = Montar([FaseSemRecurso(BancasDeTeste.DeRecursosDaFase(), DaSegundaInstanciaNaFase())], [etapa]);

        QuemJulgaORecurso.ItemJulgado item = QuemJulgaORecurso.Itens(cenario.Cronograma, cenario.Etapas).Single();
        IReadOnlyList<BancaDeclarada> dedicadasDaEtapa = QuemJulgaORecurso.DedicadasDaPrimeiraInstancia(cenario.Etapas.Etapas[0].Bancas.Declaradas);
        IReadOnlyList<BancaDeclarada> dedicadasDaFase = QuemJulgaORecurso.DedicadasDaPrimeiraInstancia(cenario.Cronograma.Fases[0].Bancas.Declaradas);

        item.Nivel.Should().Be(Nivel.BancaDeRecursosDaEtapa);
        dedicadasDaEtapa.Should().ContainSingle().Which.Tipo.Codigo.Should().Be(TipoDeBanca.AnaliseDeRecursos);
        dedicadasDaFase.Should().ContainSingle().Which.Tipo.Codigo.Should().Be(TipoDeBanca.AnaliseDeRecursos);
        QuemJulgaORecurso.AvaliadorasQueJulgam([item]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Na homologação das inscrições, a banca de homologação julga a 1ª instância, e a de 2ª instância não forma grupo com ela")]
    public void Homologacao_ComBancaDaCompetenciaEDeSegundaInstancia_AAvaliadoraJulgaEASegundaNaoFormaGrupo()
    {
        ModeloDeFase queHomologa = FaseComRecurso(Fase, [Procedimento(TipoMotivoDeDecisao.HomologacaoDasInscricoes)], true)
            .Homologando(RegraDeHomologacaoDasInscricoes.Declarar("RESULTADO", []));
        ModeloDeFase fase = queHomologa with { Bancas = [BancasDeTeste.DaHomologacaoDasInscricoesDaFase(), DaSegundaInstanciaNaFase()] };
        Cenario cenario = Montar([fase], []);

        QuemJulgaORecurso.ItemJulgado item = QuemJulgaORecurso.Itens(cenario.Cronograma, cenario.Etapas).Single();
        IReadOnlyList<GrupoDeCandidatosAtendidos> grupos = cenario.Cronograma.Fases[0].Bancas.Grupos;

        item.Nivel.Should().Be(Nivel.BancasQueAvaliaramOItem);
        QuemJulgaORecurso.AvaliadorasQueJulgam([item]).Should().ContainSingle().Which.Tipo.Codigo.Should().Be(TipoDeBanca.HomologacaoDasInscricoes);
        grupos.Select(static grupo => grupo.Tipo).Should().BeEquivalentTo([TipoDeBanca.HomologacaoDasInscricoes, TipoDeBanca.AnaliseDeRecursosDeSegundaInstancia]);
        grupos.Should().OnlyContain(static grupo => grupo.Posicoes.Count == 1 && grupo.AtendeTodosOsCandidatos);
        ConferenciaDasBancas.ConferirMinimoDeBancasPorTipo(cenario.Cronograma, cenario.Etapas).Should().BeEmpty();
        SemBanca(cenario).Should().BeEmpty();
    }

    [Fact(DisplayName = "A avaliadora do item continua julgando em 1ª instância quando a fase tem também a banca de 2ª instância")]
    public void Avaliadora_ComBancaDeSegundaInstanciaNaFase_ContinuaJulgando()
    {
        ModeloDeEtapa etapa = EtapaComRecurso("Prova", Fase, DaNota, true, NaEtapa(Entrevista));
        Cenario cenario = Montar([FaseSemRecurso(DaSegundaInstanciaNaFase())], [etapa]);

        QuemJulgaORecurso.Itens(cenario.Cronograma, cenario.Etapas).Single().Nivel.Should().Be(Nivel.BancasQueAvaliaramOItem);
        SemBanca(cenario).Should().BeEmpty();
    }
}
