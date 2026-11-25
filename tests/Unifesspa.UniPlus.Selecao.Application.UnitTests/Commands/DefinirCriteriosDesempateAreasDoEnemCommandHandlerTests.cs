namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDosCriteriosDeDesempate;

/// <summary>
/// A ordem de áreas do ENEM do critério de desempate: recusa,
/// cada uma no campo do item e sem ecoar o texto recebido, a área que falta, a de forma inválida, a repetida,
/// a acima do teto e a fora do quadro de pesos da classificação.
/// </summary>
public sealed class DefinirCriteriosDesempateAreasDoEnemCommandHandlerTests
{
    private static ProcessoSeletivo ProcessoComClassificacaoImportadaSemQuadro()
    {
        ProcessoSeletivo processo = NovoProcesso("PS ENEM 2027", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        processo.DefinirClassificacao(ConfiguracaoClassificacao.Criar(new DeclaracaoDaClassificacao
        {
            RegraCalculo = ReferenciaRegra.De(RegraDeCalculoDaNota.ClassificacaoImportada.Codigo(), "v1", new string('a', 64)),
            RegraOrdemAlocacao = ReferenciaRegra.De(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria.Codigo(), "v1", new string('c', 64)),
            BaseadoEmEnem = true,
        }).Value!).IsSuccess.Should().BeTrue();
        return processo;
    }

    [Theory(DisplayName = "Área com espaços ou letras minúsculas é recusada junto com outra causa, sem gravar")]
    [InlineData(" REDACAO ")]
    [InlineData("redacao")]
    public async Task Handle_AreaForaDoFormato_AcumulaRecusasSemGravar(string area)
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioDeAreas(1, area, "REDACAO"), CriterioDeAreas(2, "FISICA")));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].parametros.areas[0]", "CriterioDesempate.AreaInvalida"),
            ("criterios[1].parametros.areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        processo.CriteriosDesempate.Should().BeEmpty();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Área em minúsculas é recusada sem repetir o texto recebido na mensagem")]
    public async Task Handle_AreaEmMinusculas_RecusaSemRepetirOTexto()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioDeAreas(1, "redacao")));

        result.Errors.Should().ContainSingle().Subject.Error.Message.Should().NotContain("redacao");
    }

    [Fact(DisplayName = "Área com espaços antes da mesma área válida é recusada pela forma, sem recusa de repetição")]
    public async Task Handle_AreaComEspacosAntesDaMesmaArea_RecusaFormaSemRepeticao()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioDeAreas(1, " REDACAO ", "REDACAO")));

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        (erro.Field, erro.Error.Code).Should().Be(("criterios[0].parametros.areas[0]", "CriterioDesempate.AreaInvalida"));
        processo.CriteriosDesempate.Should().BeEmpty();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com DESEMPATE_MAIOR_NOTA_AREA_ENEM sem áreas recusa no campo do critério")]
    public async Task Handle_MaiorNotaAreaEnemSemAreas_RecusaNoCampo()
    {
        ProcessoSeletivo processo = NovoProcesso("PS ENEM 2027", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioSimples(1, RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo())));

        result.IsFailure.Should().BeTrue();
        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("criterios[0].parametros.areas");
        erro.Error.Code.Should().Be("CriterioDesempate.AreasObrigatorias");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle com área nula na ordem recusa no campo do item, sem estourar")]
    public async Task Handle_MaiorNotaAreaEnemComItemNulo_RecusaNoCampoDoItem()
    {
        ProcessoSeletivo processo = NovoProcesso("PS ENEM 2027", TiposDeProcessoDeTeste.IndigenaEQuilombola);
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioDeAreas(1, "REDACAO", null!)));

        result.IsFailure.Should().BeTrue();
        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        erro.Field.Should().Be("criterios[0].parametros.areas[1]");
        erro.Error.Code.Should().Be("CriterioDesempate.AreaInvalida");
    }

    [Fact(DisplayName = "Handle devolve todas as áreas fora do quadro, cada uma no campo do item, e não persiste")]
    public async Task Handle_AreasForaDoQuadroEmDoisCriterios_DevolveTodasNosCampos()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(
            Comando(processo.Id, CriterioDeAreas(1, "FISICA", "REDACAO"), CriterioDeAreas(2, "MATEMATICA", "QUIMICA")));

        result.IsFailure.Should().BeTrue();
        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].parametros.areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
            ("criterios[1].parametros.areas[1]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Handle acumula a recusa de um critério que nem é criado com a área fora do quadro de outro")]
    public async Task Handle_AreaRepetidaEAreaForaDoQuadroEmCriteriosDiferentes_DevolveAsDuas()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(
            Comando(processo.Id, CriterioDeAreas(1, "REDACAO", "REDACAO"), CriterioDeAreas(2, "FISICA")));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("criterios[0].parametros.areas[1]", "CriterioDesempate.AreaRepetida"),
            ("criterios[1].parametros.areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"));
        processo.CriteriosDesempate.Should().BeEmpty();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Theory(DisplayName = "Área recusada pela forma traz a lista de aceitas, sem ecoar o texto nem somar a recusa fora do quadro")]
    [InlineData("Redação")]
    [InlineData("RED\u202EACAO\n\u2028X")]
    [InlineData(null)]
    public async Task Handle_AreaForaDaForma_SoARecusaDaForma(string? area)
    {
        area ??= new string('A', 200_000);
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioDeAreas(1, area)));

        FieldError erro = result.Errors.Should().ContainSingle().Subject;
        (erro.Field, erro.Error.Code).Should().Be(("criterios[0].parametros.areas[0]", "CriterioDesempate.AreaInvalida"));
        erro.Error.Message.Should().StartWith("Cada área da ordem de desempate é um código")
            .And.Contain("Áreas aceitas: CIENCIAS_DA_NATUREZA (Ciências da Natureza e suas Tecnologias)")
            .And.NotContain("\u202E").And.NotContain("\n").And.NotContain("AAAAAAAAAA");
    }

    [Fact(DisplayName = "Sem quadro de pesos por área, a recusa pela forma sai sem lista de áreas aceitas")]
    public async Task Handle_AreaForaDaFormaSemQuadro_SemLista()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoImportadaSemQuadro();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(Comando(processo.Id, CriterioDeAreas(1, "Redação")));

        FieldError forma = result.Errors.Single(static e => e.Error.Code == "CriterioDesempate.AreaInvalida");
        forma.Error.Message.Should().NotContain("Áreas aceitas");
    }

    [Fact(DisplayName = "A lista de áreas aceitas vai uma vez só, mesmo com recusas de forma e fora do quadro juntas")]
    public async Task Handle_VariasRecusasDeArea_ListaUmaVez()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());

        Result result = await cenario.DefinirAsync(
            Comando(processo.Id, CriterioDeAreas(1, "Redação", "FISICA"), CriterioDeAreas(2, "QUIMICA")));

        result.Errors.Should().HaveCount(3);
        result.Errors.Count(static e => e.Error.Message.Contains("Áreas aceitas", StringComparison.Ordinal)).Should().Be(1);
    }

    [Fact(DisplayName = "Área repetida e lista acima do teto de áreas não geram também a recusa fora do quadro")]
    public async Task Handle_AreaRepetidaEListaAcimaDoTeto_SemRecusaDuplicada()
    {
        ProcessoSeletivo processo = ProcessoComClassificacaoEnem();
        CenarioDosCriteriosDeDesempate cenario = Com(processo).ComRegra(RegraDeDesempate.MaiorNotaPorAreaDoEnem.Codigo());
        string[] areasDemais = [.. Enumerable.Range(0, ParametrosDesempateMaiorNotaAreaEnem.AreasMaximo + 1).Select(static i => $"AREA_{i}")];

        Result result = await cenario.DefinirAsync(
            Comando(processo.Id, CriterioDeAreas(1, "FISICA", "FISICA"), CriterioDeAreas(2, areasDemais)));

        result.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("criterios[0].parametros.areas[1]", "CriterioDesempate.AreaRepetida"),
            ("criterios[1].parametros.areas", "CriterioDesempate.AreasEmExcesso"),
            ("criterios[0].parametros.areas[0]", "ProcessoSeletivo.DesempateAreaEnemForaDoQuadro"),
        ]);
    }
}
