namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// Um processo sem etapas, com a regra de prazo de recurso no catálogo, em que o teste declara uma etapa com recurso e confere o que
/// o catálogo de atos publicados permite.
/// </summary>
internal sealed class CenarioDoRecursoDaEtapa
{
    internal const string TipoDoAto = "RESULTADO_CLASSIFICACAO";

    private static readonly ProdutoDaEtapaInput Preliminar = new(TipoDoAto, "PRELIMINAR");
    private static readonly ProdutoDaEtapaInput Definitivo = new(TipoDoAto, "DEFINITIVO");

    internal CenarioDoRecursoDaEtapa()
    {
        Repositorio.ObterParaMutacaoAsync(Processo.Id, Arg.Any<CancellationToken>()).Returns(Processo);
        Regras.ObterAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(RegraDePrazoDoRecurso());
    }

    internal ProcessoSeletivo Processo { get; } = ProcessoSemEtapas();

    internal IProcessoSeletivoRepository Repositorio { get; } = Substitute.For<IProcessoSeletivoRepository>();

    internal IUnitOfWork UnitOfWork { get; } = Substitute.For<IUnitOfWork>();

    private IRegistroCodecsEnvelope Codecs { get; } = Substitute.For<IRegistroCodecsEnvelope>();

    private IRegraCatalogoReader Regras { get; } = Substitute.For<IRegraCatalogoReader>();

    /// <summary>
    /// O ato é resultado, e por isso a conferência do produto passa, mas pode carregar a marca que torna indevido iniciar o prazo
    /// nele. As três marcas são independentes no catálogo: garantir que o ato é resultado não garante que ele não congela a
    /// configuração nem que não é irreversível.
    /// </summary>
    internal static ITipoAtoPublicadoReader CatalogoComOAto(bool congelaConfiguracao = false, bool efeitoIrreversivel = false)
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(TipoDoAto, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                TipoDoAto, $"Ato de {TipoDoAto}", CongelaConfiguracao: congelaConfiguracao, UnicoPorObjeto: false,
                EfeitoIrreversivel: efeitoIrreversivel, EhResultado: true));
        return catalogo;
    }

    /// <summary>O catálogo deixa de ter versão vigente do tipo: a irreversibilidade dele passa a ser desconhecida.</summary>
    internal static ITipoAtoPublicadoReader CatalogoSemOAto()
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        return catalogo;
    }

    /// <summary>A etapa do payload, com os produtos preliminar e definitivo e, por padrão, um recurso iniciado pela publicação do ato.</summary>
    internal static EtapaProcessoInput Etapa(
        Guid? id = null,
        string nome = "Prova Objetiva",
        bool comRecurso = true,
        bool admiteInstanciaSuperior = false,
        IReadOnlyList<ProdutoDaEtapaInput>? produtos = null) => new(
        nome, CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1, id,
        Produtos: produtos ?? [Preliminar, Definitivo],
        Bancas: [],
        Recursos: comRecurso ? [Recurso(admiteInstanciaSuperior)] : []);

    internal static IReadOnlyList<ProdutoDaEtapaInput> ProdutosComODefinitivoPrimeiro() => [Definitivo, Preliminar];

    internal static IReadOnlyList<ProdutoDaEtapaInput> SoOPreliminar() => [Preliminar];

    internal Task<Result> DefinirAsync(EtapaProcessoInput etapa, ITipoAtoPublicadoReader? catalogo = null) =>
        ExecutarComOCatalogo(etapa, catalogo ?? CatalogoComOAto());

    /// <summary>Declara a etapa, exige sucesso e devolve o identificador da única etapa do processo.</summary>
    internal async Task<Guid> DefinirComSucessoAsync(EtapaProcessoInput etapa, ITipoAtoPublicadoReader? catalogo = null)
    {
        Result resultado = await DefinirAsync(etapa, catalogo).ConfigureAwait(true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        return Processo.Etapas.Single().Id;
    }

    /// <summary>Publica a versão do processo, congelando o tipo de ato como resultado que não congela a configuração.</summary>
    internal void Publicar(CenarioDoRecursoDaEtapa? origemDoGrafo = null) =>
        VersaoPublicadaDeTeste.Publicar(
            Processo, (origemDoGrafo ?? this).Processo, Repositorio, Codecs,
            new TipoDeAtoCongelado(TipoDoAto, Nome: "Nome do ato", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));

    /// <summary>Dá à única etapa do processo a identidade de outra, como a etapa viva que a gravação seguinte edita no lugar.</summary>
    internal void IdentificarEtapaComo(Guid id)
    {
        typeof(EntityBase).GetProperty("Id")!.SetValue(Processo.Etapas.Single(), id);
        Processo.Etapas.Single().Id.Should().Be(id);
    }

    private static RecursoDaEtapaInput Recurso(bool admiteInstanciaSuperior) => new(
        AncoraDoRecurso.AtoPublicado, "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", 2m, "diasUteis", TipoDoAto,
        admiteInstanciaSuperior, admiteInstanciaSuperior ? 3m : null, admiteInstanciaSuperior ? "diasUteis" : null, "semEfeitoSuspensivo", null);

    private static RegraCatalogo RegraDePrazoDoRecurso() => RegraCatalogo.Criar(
        "RECURSO-PRAZO-ANCORADO-EM-ATO", "v1", TipoRegra.RegraPrazoRecurso,
        JsonDocument.Parse("{}").RootElement, JsonDocument.Parse("[]").RootElement, "Lei 9.784/1999, art. 59");

    private Task<Result> ExecutarComOCatalogo(EtapaProcessoInput etapa, ITipoAtoPublicadoReader catalogo) =>
        Executar(
            new DefinirEtapasCommand(Processo.Id, [etapa]), Repositorio, UnitOfWork,
            cadastros: new(Regras: Regras, Atos: catalogo, Codecs: Codecs));
}
