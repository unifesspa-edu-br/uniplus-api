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

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport.CenariosDeDefinirEtapas;

public sealed class DefinirEtapasVersaoPublicadaTests
{
    [Fact(DisplayName = "Tipo já declarado na versão publicada vale pela classificação congelada: o catálogo que passou a congelar não recusa a etapa")]
    public async Task Handle_TipoJaDeclaradoQueOCatalogoPassouACongelar_AceitaPelaClassificacaoCongelada()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        VersaoPublicadaDeTeste.Publicar(
            processo, repository, codecs,
            new TipoDeAtoCongelado("RESULTADO_CLASSIFICACAO", UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO") with { CongelaConfiguracao = true });

        Result resultado = await Executar(
            ComUmProdutoDefinitivo(processo), repository, Substitute.For<IUnitOfWork>(),
            tipoAtoPublicadoReader: catalogo, registroCodecs: codecs);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Tipo declarado pela primeira vez continua conferido pelo catálogo, mesmo com versão publicada")]
    public async Task Handle_TipoNovoEmProcessoPublicado_ConfereOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        VersaoPublicadaDeTeste.Publicar(
            processo, repository, codecs,
            new TipoDeAtoCongelado("OUTRO_ATO", UnicoPorObjeto: false, EhResultado: false, CongelaConfiguracao: false));
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO") with { CongelaConfiguracao = true });

        Result resultado = await Executar(
            ComUmProdutoDefinitivo(processo), repository, Substitute.For<IUnitOfWork>(),
            tipoAtoPublicadoReader: catalogo, registroCodecs: codecs);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado");
    }

    [Fact(DisplayName = "Versão publicada que o codec não lê é recusada com a falha do codec, sem cair no catálogo")]
    public async Task Handle_VersaoPublicadaIlegivel_RecusaComAFalhaDoCodec()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        VersaoPublicadaDeTeste.PublicarIlegivel(
            processo, repository, codecs, new DomainError("Envelope.CodecDesconhecido", "O codec não lê esta versão."));
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync("RESULTADO_CLASSIFICACAO", Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado("RESULTADO_CLASSIFICACAO"));

        Result resultado = await Executar(
            ComUmProdutoDefinitivo(processo), repository, Substitute.For<IUnitOfWork>(),
            tipoAtoPublicadoReader: catalogo, registroCodecs: codecs);

        resultado.Error!.Code.Should().Be("Envelope.CodecDesconhecido");
    }

    private static DefinirEtapasCommand ComUmProdutoDefinitivo(ProcessoSeletivo processo) => new(
        processo.Id,
        [
            new EtapaProcessoInput(
                "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                Produtos: [new ProdutoDaEtapaInput("RESULTADO_CLASSIFICACAO", "DEFINITIVO")],
                Bancas: [], Recursos: []),
        ]);
}
