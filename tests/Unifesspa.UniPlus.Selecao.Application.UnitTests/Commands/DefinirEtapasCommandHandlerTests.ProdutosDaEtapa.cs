namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ArranjoDasEtapas;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDasEtapas;

public sealed partial class DefinirEtapasCommandHandlerTests
{
    /// <summary>
    /// A fase confere o ato contra o catálogo de Publicações; a etapa não conferia. O código
    /// declarado aqui vira o nome do produto no cronograma público e a chave pela qual a
    /// restauração da configuração congelada reencontra o produto — um código com erro de
    /// digitação só apareceria depois de o certame estar no Diário Oficial. O catálogo não conhece
    /// código algum: é o erro de digitação do payload chegando sem correspondência, não uma
    /// indisponibilidade do módulo de Publicações. A etapa já foi construída quando a recusa
    /// acontece, e não basta o handler deixar de chamar SalvarAlteracoesAsync: o SaveChanges
    /// automático do Wolverine roda DEPOIS do retorno e persistiria a mutação, e só o descarte do
    /// rastreamento a impede.
    /// </summary>
    [Fact(DisplayName = "Produto de etapa com ato fora do catálogo é recusado, como na fase")]
    public async Task Handle_ProdutoComAtoForaDoCatalogo_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, tiposDeAto: CatalogoSemAtos());
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva() with { Produtos = [new ProdutoDaEtapaInput("RESULTADO_PRELMINAR", null)] });

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// Papel preliminar ou definitivo é o par que a janela recursal ancora, e só resultado o
    /// admite. A fase recusa o mesmo. O ato existe e está vigente — o que ele não é, é resultado:
    /// a recusa tem de vir da segunda pergunta, não da primeira. Sem o descarte, o SaveChanges
    /// automático do Wolverine persistiria a etapa que a recusa acabou de invalidar.
    /// </summary>
    [Fact(DisplayName = "Produto de etapa com papel sobre ato que não é resultado é recusado")]
    public async Task Handle_PapelEmAtoQueNaoEhResultado_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        TipoAtoPublicadoView edital = new(
            "EDITAL_ABERTURA", "Edital de abertura", CongelaConfiguracao: true, UnicoPorObjeto: true, EfeitoIrreversivel: false, EhResultado: false);
        CenarioDasEtapas cenario = Para(processo, tiposDeAto: CatalogoCom(edital));
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva() with { Produtos = [Preliminar("EDITAL_ABERTURA")] });

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProdutoDaEtapa.PapelEmAtoQueNaoEhResultado");
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
    }

    /// <summary>
    /// O mesmo tipo de ato aparece muitas vezes num cronograma real — cada etapa publica
    /// preliminar e definitivo, e etapas diferentes publicam o mesmo gabarito. Reler o catálogo
    /// por item multiplica idas ao banco enquanto o <c>SELECT ... FOR UPDATE</c> do certame está
    /// na mão, serializando quem mais estiver editando o mesmo certame por todo esse tempo. Aqui
    /// são quatro produtos, dois por etapa, todos do MESMO código: uma consulta basta.
    /// </summary>
    [Fact(DisplayName = "O catálogo é consultado uma vez por código, não uma vez por produto declarado")]
    public async Task Handle_MesmoAtoEmVariosProdutos_ConsultaOCatalogoUmaVezPorCodigo()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoAtoPublicadoReader catalogo = CatalogoDoResultado(AtoDaClassificacao);
        CenarioDasEtapas cenario = Para(processo, tiposDeAto: catalogo);
        EtapaProcessoInput redacao = EtapaClassificatoria("Redação", TipoRedacaoOrigemId, 2) with { Produtos = PreliminarEDefinitivo(AtoDaClassificacao) };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, ProvaPublicandoAClassificacao(), redacao));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        await catalogo.Received(1).ObterVigenteAsync(AtoDaClassificacao, Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo classificado como resultado declarado sem papel pela etapa é recusado com ProcessoSeletivo.ResultadoDeclaradoSemPapel")]
    public async Task Handle_ResultadoSemPapel_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, tiposDeAto: CatalogoDoResultado(AtoDaClassificacao));
        DefinirEtapasCommand command = Definindo(processo, ProvaObjetiva() with { Produtos = [new ProdutoDaEtapaInput(AtoDaClassificacao, null)] });

        Result<MutacaoAceita> result = await cenario.DefinirAsync(command);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("ProcessoSeletivo.ResultadoDeclaradoSemPapel");
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <summary>
    /// O endpoint substitui a coleção inteira, e o cliente devolve o que leu. Se o catálogo
    /// aposentar um ato que alguma etapa já declarava, reconferir todo produto a cada gravação
    /// trancaria a edição de qualquer etapa do certame — inclusive de quem só quer corrigir um
    /// nome —, e a única saída seria apagar um produto que o operador não declarou hoje. É a
    /// mesma distinção que o tipo da etapa recebe: confere-se o vínculo novo, não o que já
    /// estava lá. Na segunda gravação o catálogo aposentou o ato: ObterVigenteAsync deixa de
    /// resolvê-lo.
    /// </summary>
    [Fact(DisplayName = "Ato aposentado no catálogo não tranca a gravação da etapa que já o declarava")]
    public async Task Handle_AtoAposentadoJaDeclarado_NaoTrancaAColecao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, tiposDeAto: CatalogoDoResultado(AtoDaClassificacao));
        (await cenario.DefinirAsync(Definindo(processo, ProvaPublicandoAClassificacao()))).IsSuccess.Should().BeTrue();
        Guid etapaId = processo.Etapas.Single().Id;
        cenario.TiposDeAto = CatalogoSemAtos();

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            Redefinindo(processo, ProvaPublicandoAClassificacao("Prova Objetiva — 1ª fase", etapaId)));

        resultado.IsSuccess.Should().BeTrue(
            $"o produto já estava declarado e não é o que esta gravação muda — veio '{resultado.Error?.Code}'");
        processo.Etapas.Single().Nome.Should().Be("Prova Objetiva — 1ª fase");
    }

    [Fact(DisplayName = "Tipo já declarado na versão publicada vale pela classificação congelada: o catálogo que passou a congelar não recusa a etapa")]
    public async Task Handle_TipoJaDeclaradoQueOCatalogoPassouACongelar_AceitaPelaClassificacaoCongelada()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        TipoAtoPublicadoView agoraCongelante = AtoDeResultado(AtoDaClassificacao) with { CongelaConfiguracao = true };
        CenarioDasEtapas cenario = Para(processo, tiposDeAto: CatalogoCom(agoraCongelante));
        VersaoPublicadaDeTeste.Publicar(processo, cenario.Repositorio, cenario.Codecs, ClassificacaoCongelada());

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(ComUmProdutoDefinitivo(processo));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Tipo declarado pela primeira vez continua conferido pelo catálogo, mesmo com versão publicada")]
    public async Task Handle_TipoNovoEmProcessoPublicado_ConfereOCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        TipoAtoPublicadoView congelante = AtoDeResultado(AtoDaClassificacao) with { CongelaConfiguracao = true };
        CenarioDasEtapas cenario = Para(processo, tiposDeAto: CatalogoCom(congelante));
        VersaoPublicadaDeTeste.Publicar(processo, cenario.Repositorio, cenario.Codecs, new TipoDeAtoCongelado("OUTRO_ATO", UnicoPorObjeto: false, EhResultado: false, CongelaConfiguracao: false));

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(ComUmProdutoDefinitivo(processo));

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado");
    }

    [Fact(DisplayName = "Versão publicada que o codec não lê é recusada com a falha do codec, sem cair no catálogo")]
    public async Task Handle_VersaoPublicadaIlegivel_RecusaComAFalhaDoCodec()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, tiposDeAto: CatalogoDoResultado(AtoDaClassificacao));
        DomainError falhaDoCodec = new("Envelope.CodecDesconhecido", "O codec não lê esta versão.");
        VersaoPublicadaDeTeste.PublicarIlegivel(processo, cenario.Repositorio, cenario.Codecs, falhaDoCodec);

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(ComUmProdutoDefinitivo(processo));

        resultado.Error!.Code.Should().Be("Envelope.CodecDesconhecido");
    }

    /// <summary>
    /// O ato é resultado — a conferência do produto passa —, mas carrega a flag que torna a âncora
    /// indevida. As três flags são independentes no catálogo: garantir que o ato é resultado não
    /// garante que ele não congela a configuração nem que é irreversível.
    /// </summary>
    [Theory(DisplayName = "Âncora do recurso da etapa em ato congelante ou irreversível é recusada, como na fase")]
    [InlineData(true, false, "ProcessoSeletivo.TipoQueCongelaDeclaradoComoResultado")]
    [InlineData(false, true, "RecursoDaEtapa.AncoraEmAtoIrreversivel")]
    public async Task Handle_AncoraEmAtoQueNaoCabeRecurso_Recusa(
        bool congelaConfiguracao, bool efeitoIrreversivel, string codigoEsperado)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        TipoAtoPublicadoView ato = AtoDeResultado(AtoDaClassificacao) with { CongelaConfiguracao = congelaConfiguracao, EfeitoIrreversivel = efeitoIrreversivel };
        CenarioDasEtapas cenario = Para(processo, CatalogoCom(ato), CatalogoDoPrazoAncoradoEmAto());
        EtapaProcessoInput etapa = ProvaObjetiva() with { Produtos = [Preliminar(AtoDaClassificacao)], Recursos = [RecursoAncoradoNoAto(AtoDaClassificacao)] };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, etapa));

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be(codigoEsperado);
        cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A declaração de instância superior da janela chega à janela gravada, e não um valor fixo do handler")]
    public async Task Handle_JanelaComInstanciaSuperior_GravaADeclaracao()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, CatalogoDoResultado(AtoDaClassificacao), CatalogoDoPrazoAncoradoEmAto());
        EtapaProcessoInput etapa = ProvaPublicandoAClassificacao(comAncora: true, admiteInstanciaSuperior: true);

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, etapa));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
        processo.Etapas.Single().Recursos.Single().Args.AdmiteInstanciaSuperior.Should().BeTrue();
    }

    private static TipoDeAtoCongelado ClassificacaoCongelada() =>
        new(AtoDaClassificacao, UnicoPorObjeto: false, EhResultado: true, CongelaConfiguracao: false);
}
