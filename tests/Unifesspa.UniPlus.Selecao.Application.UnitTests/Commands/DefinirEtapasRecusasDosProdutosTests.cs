namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.FasesDoCronogramaDeTeste;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

/// <summary>
/// Os produtos de uma etapa mal declarados erram em vários lugares ao mesmo tempo: cada recusa sai no produto que a
/// causou, na ordem em que os produtos chegaram, antes das recusas do período e dos tipos de decisão.
/// </summary>
public sealed class DefinirEtapasRecusasDosProdutosTests
{
    private const string AtoForaDoCatalogo = "ATO_FORA_DO_CATALOGO";
    private const string AtoDoResultadoFinal = "RESULTADO_FINAL";
    private const string AtoQueNaoEResultado = "AVISO_GERAL";
    private const string PapelForaDoVocabulario = "PAPEL_INEXISTENTE";
    private const string TipoForaDoVocabulario = "RECURSO_DA_BANCA";
    private static readonly DateTimeOffset Inicio = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset FimAntesDoInicio = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static ProdutoDaEtapaInput Produto(string ato, string? papel) => new(ato, papel);

    private static EtapaProcessoInput EtapaCom(params ProdutoDaEtapaInput[] produtos) => new(
        "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
        Produtos: produtos, Bancas: [], Recursos: []);

    private static ProdutoDaEtapaInput[] ProdutosComPapelDesconhecidoNoPrimeiroEAtoForaDoCatalogoNoTerceiro() =>
    [
        Produto(AtoDaClassificacao, PapelForaDoVocabulario),
        Produto(AtoDaClassificacao, PapelProdutoFaseCodigo.Preliminar),
        Produto(AtoForaDoCatalogo, null),
    ];

    // A segunda etapa também tem o período invertido, para provar que nada dela além dos produtos é julgado.
    private static EtapaProcessoInput[] TresEtapasComPapelDesconhecidoECatalogoIlegivelNaSegunda() =>
    [
        EtapaCom(Produto(AtoDaClassificacao, PapelForaDoVocabulario)),
        EtapaCom(ProdutosComPapelDesconhecidoNoPrimeiroEAtoForaDoCatalogoNoTerceiro()) with
        {
            Nome = "Redação",
            Ordem = 2,
            Inicio = Inicio,
            Fim = FimAntesDoInicio,
        },
        EtapaCom(Produto(AtoDaClassificacao, PapelForaDoVocabulario)) with { Nome = "Entrevista", Ordem = 3 },
    ];

    private static IProcessoSeletivoRepository RepositorioDe(ProcessoSeletivo processo)
    {
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        return repository;
    }

    private static ITipoAtoPublicadoReader CatalogoDeResultados()
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicadoView?)null);
        catalogo.ObterVigenteAsync(AtoDaClassificacao, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado(AtoDaClassificacao));
        catalogo.ObterVigenteAsync(AtoDoResultadoFinal, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado(AtoDoResultadoFinal));
        catalogo.ObterVigenteAsync(AtoQueNaoEResultado, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(new TipoAtoPublicadoView(
                AtoQueNaoEResultado, "Aviso geral", CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, EhResultado: false));
        return catalogo;
    }

    private static Task<Result> Definir(EtapaProcessoInput etapa) => Definir(etapa, out _);

    private static Task<Result> Definir(EtapaProcessoInput etapa, out ProcessoSeletivo processo)
    {
        processo = ProcessoSemEtapas();
        return Executar(
            new DefinirEtapasCommand(processo.Id, [etapa]), RepositorioDe(processo), Substitute.For<IUnitOfWork>(),
            cadastros: new(Atos: CatalogoDeResultados()));
    }

    private static (string? Campo, string Codigo)[] Recusas(Result resultado) =>
        [.. resultado.Errors.Select(static e => (e.Field, e.Error.Code))];

    [Fact(DisplayName = "Papel desconhecido no primeiro produto e ato inexistente no terceiro saem os dois, antes do período e dos tipos de decisão")]
    public async Task Handle_DoisProdutosRecusados_AcumulaNaOrdemDosProdutosAntesDoPeriodoEDosTipos()
    {
        EtapaProcessoInput etapa = EtapaCom(ProdutosComPapelDesconhecidoNoPrimeiroEAtoForaDoCatalogoNoTerceiro()) with
        {
            Inicio = Inicio,
            Fim = FimAntesDoInicio,
            TiposDeDecisao = [TipoForaDoVocabulario],
        };

        Result resultado = await Definir(etapa);

        Recusas(resultado).Should().Equal(
            ("etapas[0].produtos[0].papel", "ProdutoDaEtapa.PapelDesconhecido"),
            ("etapas[0].produtos[2].atoCodigo", "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo"),
            ("etapas[0].fim", "EtapaProcesso.JanelaInvertida"),
            ("etapas[0].tiposDeDecisao[0]", "MotivoDeDecisao.TipoInvalido"));
    }

    [Fact(DisplayName = "Ato repetido na etapa recusa só o produto que repete: A, B, A com o mesmo papel recusa o terceiro")]
    public async Task Handle_AtoRepetidoEmAba_RecusaOTerceiroProduto()
    {
        EtapaProcessoInput etapa = EtapaCom(
            Produto(AtoDaClassificacao, PapelProdutoFaseCodigo.Preliminar),
            Produto(AtoDoResultadoFinal, PapelProdutoFaseCodigo.Preliminar),
            Produto(AtoDaClassificacao, PapelProdutoFaseCodigo.Preliminar));

        Result resultado = await Definir(etapa);

        Recusas(resultado).Should().Equal(("etapas[0].produtos[2]", "EtapaProcesso.AtoDuplicadoNaEtapa"));
    }

    [Fact(DisplayName = "Produto recusado e ato repetido na mesma etapa devolvem só a recusa do produto, e a etapa não muda")]
    public async Task Handle_ProdutoRecusadoEAtoRepetido_DevolveSoARecusaDoProduto()
    {
        EtapaProcessoInput etapa = EtapaCom(
            Produto(AtoDaClassificacao, PapelProdutoFaseCodigo.Preliminar),
            Produto(AtoDaClassificacao, PapelProdutoFaseCodigo.Preliminar),
            Produto(AtoDaClassificacao, PapelForaDoVocabulario));

        Result resultado = await Definir(etapa, out ProcessoSeletivo processo);

        Recusas(resultado).Should().Equal(("etapas[0].produtos[2].papel", "ProdutoDaEtapa.PapelDesconhecido"));
        processo.Etapas.Should().BeEmpty();
    }

    [Fact(DisplayName = "Produto recusado deixa o recurso da etapa sem julgamento, mesmo ancorado num ato que a etapa não publica")]
    public async Task Handle_ProdutoRecusado_NaoJulgaORecurso()
    {
        RecursoDaEtapaInput recursoComEventoInicialNaoPublicado = new(
            AncoraDoRecurso.AtoPublicado, RegraPrazoRecursoCodigo.AncoradoEmAto, "v1", 2m, "diasUteis",
            AtoDoResultadoFinal, false, null, null, "semEfeitoSuspensivo", null);
        EtapaProcessoInput etapa = EtapaCom(Produto(AtoDaClassificacao, PapelForaDoVocabulario)) with
        {
            Recursos = [recursoComEventoInicialNaoPublicado],
        };

        Result resultado = await Definir(etapa);

        Recusas(resultado).Should().Equal(("etapas[0].produtos[0].papel", "ProdutoDaEtapa.PapelDesconhecido"));
    }

    [Fact(DisplayName = "Versão publicada ilegível interrompe na etapa em que ocorre: guarda as recusas das etapas e dos produtos anteriores, e não julga o resto")]
    public async Task Handle_VersaoPublicadaIlegivelNaSegundaEtapa_GuardaAsRecusasAnterioresECortaAsSeguintes()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = RepositorioDe(processo);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        VersaoPublicadaDeTeste.PublicarIlegivel(
            processo, repository, codecs, new DomainError("Envelope.CodecDesconhecido", "O codec não lê esta versão."));
        Result resultado = await Executar(
            new DefinirEtapasCommand(processo.Id, TresEtapasComPapelDesconhecidoECatalogoIlegivelNaSegunda()), repository, Substitute.For<IUnitOfWork>(),
            cadastros: new(Atos: CatalogoDeResultados(), Codecs: codecs));

        Recusas(resultado).Should().Equal(
            ("etapas[0].produtos[0].papel", "ProdutoDaEtapa.PapelDesconhecido"),
            ("etapas[1].produtos[0].papel", "ProdutoDaEtapa.PapelDesconhecido"),
            ("etapas[1].produtos[1].atoCodigo", "Envelope.CodecDesconhecido"));
    }

    [Fact(DisplayName = "A mensagem da recusa do papel e do ato não repete o que a pessoa digitou, que pode ser dado pessoal")]
    public async Task Handle_PapelEAtoRecusados_MensagemNaoRepeteOValorRecebido()
    {
        Result resultado = await Definir(EtapaCom(ProdutosComPapelDesconhecidoNoPrimeiroEAtoForaDoCatalogoNoTerceiro()));

        string[] mensagens = [.. resultado.Errors.Select(static e => e.Error.Message)];

        mensagens.Should().HaveCount(2);
        mensagens.Should().AllSatisfy(mensagem => mensagem.Should().NotContain(PapelForaDoVocabulario).And.NotContain(AtoForaDoCatalogo));
    }

    [Fact(DisplayName = "Papel de resultado em ato que não é resultado aponta o campo do papel, e ato fora do catálogo aponta o campo do ato")]
    public async Task Handle_PapelIncompativelComOAto_ApontaOPapel()
    {
        Result resultado = await Definir(EtapaCom(
            Produto(AtoQueNaoEResultado, PapelProdutoFaseCodigo.Preliminar),
            Produto(AtoForaDoCatalogo, null)));

        Recusas(resultado).Select(static r => r.Campo).Should().Equal("etapas[0].produtos[0].papel", "etapas[0].produtos[1].atoCodigo");
    }
}
