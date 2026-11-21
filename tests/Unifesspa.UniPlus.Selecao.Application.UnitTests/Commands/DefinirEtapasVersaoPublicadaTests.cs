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
    private const string Resultado = "RESULTADO_CLASSIFICACAO";

    [Fact(DisplayName = "Tipo já declarado na versão publicada vale pela classificação congelada: o catálogo que passou a congelar não recusa a etapa")]
    public async Task Handle_TipoJaDeclaradoQueOCatalogoPassouACongelar_AceitaPelaClassificacaoCongelada()
    {
        CenarioDaVersaoPublicada cenario = CenarioDaVersaoPublicada.Publicando(
            new TipoDeAtoCongelado(Resultado, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false));

        Result resultado = await cenario.DefinirUmProdutoDefinitivoAsync(CatalogoQueCongelaOResultado());

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Tipo declarado pela primeira vez continua conferido pelo catálogo, mesmo com versão publicada")]
    public async Task Handle_TipoNovoEmProcessoPublicado_ConfereOCatalogo()
    {
        CenarioDaVersaoPublicada cenario = CenarioDaVersaoPublicada.Publicando(
            new TipoDeAtoCongelado("OUTRO_ATO", UnicoPorObjeto: false, EhResultado: false, CongelaConfiguracao: false));

        Result resultado = await cenario.DefinirUmProdutoDefinitivoAsync(CatalogoQueCongelaOResultado());

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado");
    }

    [Fact(DisplayName = "Versão publicada que o codec não lê é recusada com a falha do codec, sem cair no catálogo")]
    public async Task Handle_VersaoPublicadaIlegivel_RecusaComAFalhaDoCodec()
    {
        CenarioDaVersaoPublicada cenario = CenarioDaVersaoPublicada.PublicandoIlegivel(
            new DomainError("Envelope.CodecDesconhecido", "O codec não lê esta versão."));

        Result resultado = await cenario.DefinirUmProdutoDefinitivoAsync(CatalogoComOResultado());

        resultado.Error!.Code.Should().Be("Envelope.CodecDesconhecido");
    }

    private static ITipoAtoPublicadoReader CatalogoComOResultado(bool congelaConfiguracao = false)
    {
        ITipoAtoPublicadoReader catalogo = Substitute.For<ITipoAtoPublicadoReader>();
        catalogo.ObterVigenteAsync(Resultado, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns(AtoDeResultado(Resultado) with { CongelaConfiguracao = congelaConfiguracao });
        return catalogo;
    }

    private static ITipoAtoPublicadoReader CatalogoQueCongelaOResultado() => CatalogoComOResultado(congelaConfiguracao: true);

    /// <summary>O certame sem etapas, com a versão publicada e o registro de codecs que a lê.</summary>
    private sealed record CenarioDaVersaoPublicada(ProcessoSeletivo Processo, IProcessoSeletivoRepository Repositorio, IRegistroCodecsEnvelope Codecs)
    {
        internal static CenarioDaVersaoPublicada Publicando(TipoDeAtoCongelado tipoDeAto)
        {
            CenarioDaVersaoPublicada cenario = Novo();
            VersaoPublicadaDeTeste.Publicar(cenario.Processo, cenario.Repositorio, cenario.Codecs, tipoDeAto);
            return cenario;
        }

        internal static CenarioDaVersaoPublicada PublicandoIlegivel(DomainError falhaDoCodec)
        {
            CenarioDaVersaoPublicada cenario = Novo();
            VersaoPublicadaDeTeste.PublicarIlegivel(cenario.Processo, cenario.Repositorio, cenario.Codecs, falhaDoCodec);
            return cenario;
        }

        internal Task<Result> DefinirUmProdutoDefinitivoAsync(ITipoAtoPublicadoReader catalogo) => Executar(
            ComUmProdutoDefinitivo(Processo), Repositorio, Substitute.For<IUnitOfWork>(), cadastros: new(Atos: catalogo, Codecs: Codecs));

        private static CenarioDaVersaoPublicada Novo()
        {
            ProcessoSeletivo processo = ProcessoSemEtapas();
            IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
            repositorio.ObterParaMutacaoAsync(processo.Id, Arg.Any<CancellationToken>()).Returns(processo);
            return new(processo, repositorio, Substitute.For<IRegistroCodecsEnvelope>());
        }

        private static DefinirEtapasCommand ComUmProdutoDefinitivo(ProcessoSeletivo processo) => new(
            processo.Id,
            [
                new EtapaProcessoInput(
                    "Prova Objetiva", CaraterEtapa.Classificatoria, TipoProvaObjetivaOrigemId, 1m, null, 1,
                    Produtos: [new ProdutoDaEtapaInput(Resultado, "DEFINITIVO")],
                    Bancas: [], Recursos: []),
            ]);
    }
}
