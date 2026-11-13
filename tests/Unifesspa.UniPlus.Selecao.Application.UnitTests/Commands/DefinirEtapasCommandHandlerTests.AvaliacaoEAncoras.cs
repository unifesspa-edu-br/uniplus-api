namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ArranjoDasEtapas;
using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDasEtapas;

public sealed partial class DefinirEtapasCommandHandlerTests
{
    private static EtapaProcessoInput ObjetivaComPasso(string nome, int ordem) =>
        ProvaObjetiva(nome, ordem) with
        {
            NotaMaxima = 10m,
            PassoDoLancamento = 0.5m,
            FormaDeAvaliacao = FormaDeAvaliacaoCodigo.Objetiva,
            RegraConsolidacaoCodigo = RegraConsolidacaoCodigo.AvaliacaoUnica,
            RegraConsolidacaoVersao = "v1",
        };

    /// <summary>Declara a prova que publica a classificação e devolve o id que o agregado deu à etapa.</summary>
    private static async Task<Guid> DeclararAsync(CenarioDasEtapas cenario, ProcessoSeletivo processo, bool comAncora)
    {
        Result<MutacaoAceita> declaracao = await cenario.DefinirAsync(
            Definindo(processo, ProvaPublicandoAClassificacao(comAncora: comAncora)));
        declaracao.IsSuccess.Should().BeTrue(declaracao.Error?.Message);
        return processo.Etapas.Single().Id;
    }

    /// <summary>
    /// O grafo publicado vem de uma instância à parte, com a identidade da etapa viva, porque a gravação
    /// seguinte edita a etapa no lugar.
    /// </summary>
    private static async Task<ProcessoSeletivo> ProcessoPublicadoComAncoraAsync(Guid etapaId)
    {
        ProcessoSeletivo publicado = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(publicado, CatalogoDoResultado(AtoDaClassificacao), CatalogoDoPrazoAncoradoEmAto());
        (await cenario.DefinirAsync(Definindo(publicado, ProvaPublicandoAClassificacao(comAncora: true)))).IsSuccess.Should().BeTrue();
        typeof(EntityBase).GetProperty("Id")!.SetValue(publicado.Etapas.Single(), etapaId);
        publicado.Etapas.Single().Id.Should().Be(etapaId);
        publicado.Etapas.Single().Recursos.Should().ContainSingle();
        return publicado;
    }

    private static void DeveSerAceitoOuRecusadoCom(Result<MutacaoAceita> resultado, string? codigoEsperado)
    {
        if (codigoEsperado is null)
        {
            resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        }
        else
        {
            resultado.Error!.Code.Should().Be(codigoEsperado);
        }
    }

    /// <summary>O catálogo deixa de ter versão vigente do tipo: a irreversibilidade dele passa a ser desconhecida.</summary>
    [Theory(DisplayName = "Com o tipo fora do catálogo hoje, a âncora nova da etapa é recusada e a que o agregado já tinha segue aceita")]
    [InlineData(false, "ProdutoDaEtapa.AtoNaoEncontradoNoCatalogo")]
    [InlineData(true, null)]
    public async Task Handle_AncoraEmTipoSemVersaoVigenteNoCatalogo_RecusaSoANovaEmRelacaoAoAgregado(bool ancoraJaNoAgregado, string? codigoEsperado)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, CatalogoDoResultado(AtoDaClassificacao), CatalogoDoPrazoAncoradoEmAto());
        Guid etapaId = await DeclararAsync(cenario, processo, ancoraJaNoAgregado);
        VersaoPublicadaDeTeste.Publicar(processo, cenario.Repositorio, cenario.Codecs, ClassificacaoCongelada());
        cenario.TiposDeAto = CatalogoSemAtos();

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            Redefinindo(processo, ProvaPublicandoAClassificacao(id: etapaId, comAncora: true)));

        DeveSerAceitoOuRecusadoCom(resultado, codigoEsperado);
    }

    /// <summary>
    /// A versão publicada tem a âncora. A sessão grava o agregado sem a âncora, o catálogo perde o tipo e o
    /// operador a reenvia.
    /// </summary>
    [Fact(DisplayName = "A âncora que só a versão publicada tem segue aceita quando o catálogo perde o tipo e o agregado não a tem mais")]
    public async Task Handle_AncoraSoNaVersaoPublicada_CatalogoSemOTipo_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, CatalogoDoResultado(AtoDaClassificacao), CatalogoDoPrazoAncoradoEmAto());
        Guid etapaId = await DeclararAsync(cenario, processo, comAncora: true);
        ProcessoSeletivo publicado = await ProcessoPublicadoComAncoraAsync(etapaId);
        VersaoPublicadaDeTeste.Publicar(processo, publicado, cenario.Repositorio, cenario.Codecs, ClassificacaoCongelada());
        (await cenario.DefinirAsync(Redefinindo(processo, ProvaPublicandoAClassificacao(id: etapaId)))).IsSuccess.Should().BeTrue();
        processo.Etapas.Single().Recursos.Should().BeEmpty();
        publicado.Etapas.Single().Recursos.Should().ContainSingle();
        cenario.TiposDeAto = CatalogoSemAtos();

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            Redefinindo(processo, ProvaPublicandoAClassificacao(id: etapaId, comAncora: true)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "Em rascunho, a âncora que a etapa já tinha segue aceita quando o catálogo perde o tipo")]
    public async Task Handle_RascunhoComAncoraJaDeclarada_CatalogoSemOTipo_Aceita()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, CatalogoDoResultado(AtoDaClassificacao), CatalogoDoPrazoAncoradoEmAto());
        Guid etapaId = await DeclararAsync(cenario, processo, comAncora: true);
        cenario.TiposDeAto = CatalogoSemAtos();

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            Redefinindo(processo, ProvaPublicandoAClassificacao("Prova Objetiva — 1ª fase", etapaId, comAncora: true)));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A etapa de avaliação humana congela a regra de consolidação com o hash do catálogo, nunca o do payload")]
    public async Task Handle_FormaHumanaComRegraDeConsolidacao_GravaAReferenciaDoCatalogo()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        RegraCatalogo regra = RegraDoCatalogo(RegraConsolidacaoCodigo.CorrecaoIndependente, TipoRegra.RegraConsolidacao);
        CenarioDasEtapas cenario = Para(processo, regras: CatalogoDeRegras(regra));

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            RedacaoHumana(processo, FormaDeAvaliacaoCodigo.Humana, RegraConsolidacaoCodigo.CorrecaoIndependente));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        EtapaProcesso etapa = processo.Etapas.Single();
        etapa.FormaDeAvaliacao.Should().Be(FormaDeAvaliacao.Humana);
        etapa.RegraConsolidacao.Should().Be(
            ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash).Value);
    }

    [Fact(DisplayName = "A regra de consolidação só aceita regra do tipo consolidação")]
    public async Task Handle_RegraDeOutroTipoComoConsolidacao_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        RegraCatalogo regraDeArredondamento = RegraDoCatalogo("PRECISAO-TRUNCAR", TipoRegra.RegraArredondamento);
        CenarioDasEtapas cenario = Para(processo, regras: CatalogoDeRegras(regraDeArredondamento));

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            RedacaoHumana(processo, FormaDeAvaliacaoCodigo.Humana, "PRECISAO-TRUNCAR"));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].regraConsolidacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcesso.RegraDeConsolidacaoInvalida);
        await cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A coerência da avaliação acumula entre as etapas, sem ler o catálogo: cada etapa devolve o seu erro")]
    public async Task Handle_DuasEtapasObjetivasComPasso_AcumulaUmErroPorEtapa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            Definindo(processo, ObjetivaComPasso("Prova 1", 1), ObjetivaComPasso("Prova 2", 2)));

        resultado.Errors.Select(e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("etapas[0].regraConsolidacao", EtapaProcesso.RegraDeConsolidacaoForaDaFormaHumana),
            ("etapas[0].passoDoLancamento", EtapaProcesso.PassoNaFormaObjetiva),
            ("etapas[1].regraConsolidacao", EtapaProcesso.RegraDeConsolidacaoForaDaFormaHumana),
            ("etapas[1].passoDoLancamento", EtapaProcesso.PassoNaFormaObjetiva),
        ]);
        await cenario.Regras.DidNotReceiveWithAnyArgs().ObterAsync(default!, default!, default);
    }

    [Theory(DisplayName = "Código sem versão, ou versão sem código, é recusado dizendo qual metade falta")]
    [InlineData("CONSOLIDACAO-AVALIACAO-UNICA", null, "a versão")]
    [InlineData(null, "v1", "o código")]
    public async Task Handle_RegraDeConsolidacaoPelaMetade_Recusa(string? codigo, string? versao, string falta)
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            RedacaoHumana(processo, FormaDeAvaliacaoCodigo.Humana, codigo, versao));

        resultado.Errors.Should().ContainSingle().Which.Error.Message.Should().Contain(falta);
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcesso.RegraDeConsolidacaoInvalida);
    }

    [Fact(DisplayName = "Token de forma de avaliação desconhecido é recusado, não lido como ausente")]
    public async Task Handle_FormaDeAvaliacaoDesconhecida_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(
            RedacaoHumana(processo, "HUMANO", regraCodigo: null, regraVersao: null));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].formaDeAvaliacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcesso.FormaDeAvaliacaoDesconhecida);
    }

    [Fact(DisplayName = "O item reenviado com o id é a mesma linha: o rótulo muda e o id continua; o item sem id é novo")]
    public async Task Handle_ItemComIdExistente_PreservaOIdQuandoORotuloMuda()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);
        EtapaProcessoInput semIds = EntrevistaComItens(null, [new(null, "Clareza", 4m), new(null, "Domínio", 6m)]);

        (await cenario.DefinirAsync(Definindo(processo, semIds))).IsSuccess.Should().BeTrue();
        EtapaProcesso etapa = processo.Etapas.Single();
        Guid idDaClareza = etapa.ItensDeAvaliacao.Single(static i => i.Rotulo == "Clareza").Id;
        EtapaProcessoInput reenvio = EntrevistaComItens(etapa.Id, [new(idDaClareza, "Objetividade", 4m), new(null, "Postura", 6m)]);
        (await cenario.DefinirAsync(Definindo(processo, reenvio))).IsSuccess.Should().BeTrue();

        processo.Etapas.Single().ItensDeAvaliacao.Should().HaveCount(2)
            .And.Contain(i => i.Id == idDaClareza && i.Rotulo == "Objetividade");
    }

    [Fact(DisplayName = "Itens e escala inválidos acumulam entre as etapas, antes de qualquer mutação")]
    public async Task Handle_ItensInvalidosEmDuasEtapas_AcumulaUmErroPorEtapa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo);
        EtapaProcessoInput semRotulo = EntrevistaComItens(null, [new(null, " ", 10m)]);
        EtapaProcessoInput semNivelCompleto = EntrevistaComItens(null, [new(null, "Clareza", 10m)], [new("Atendeu parcialmente", 0.5m)]);

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(Definindo(processo, semRotulo, semNivelCompleto));

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().BeEquivalentTo(
        [
            ("etapas[0].itensDeAvaliacao[0].rotulo", ItemDeAvaliacao.RotuloInvalido),
            ("etapas[1].escalaDeAtendimento", NiveisDeAtendimento.SemNivelCompleto),
        ]);
        processo.Etapas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A etapa de nota do ENEM recusa itens e escala na gravação, lidos do tipo congelado")]
    public async Task Handle_ItensNaEtapaDoEnem_Recusa()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        ITipoEtapaReader cadastro = CadastroDeTipos(TipoDoCadastro(TipoRedacaoOrigemId, "NOTA_ENEM", "Nota do ENEM", notaDeOrigemNoEnem: true));
        CenarioDasEtapas cenario = Para(processo, tiposDeEtapa: cadastro);
        EtapaProcessoInput etapa = EntrevistaComItens(null, [new(null, "Clareza", 10m)]) with { FormaDeAvaliacao = null };

        Result<MutacaoAceita> resultado = await cenario.DefinirAsync(Definindo(processo, etapa));

        resultado.Errors.Should().ContainSingle().Which.Field.Should().Be("etapas[0].itensDeAvaliacao");
        resultado.Errors[0].Error.Code.Should().Be(EtapaProcesso.ItensNaEtapaDoEnem);
    }

    /// <summary>
    /// A etapa publica o mesmo ato duas vezes — preliminar e definitivo —, e é da publicação
    /// preliminar que a janela recursal corre. Resolver a âncora só pelo código do ato devolvia
    /// o definitivo sempre que ele viesse primeiro na coleção, e a etapa era recusada por
    /// ancorar onde não se pode — sem que nada no payload estivesse errado. O definitivo vem
    /// primeiro de propósito: é a ordem que o servidor devolve depois de reler a etapa.
    /// </summary>
    [Fact(DisplayName = "A âncora do recurso resolve no produto PRELIMINAR, mesmo com o definitivo antes na coleção")]
    public async Task Handle_AncoraComDefinitivoAntesDoPreliminar_ResolveNoPreliminar()
    {
        ProcessoSeletivo processo = ProcessoSemEtapas();
        CenarioDasEtapas cenario = Para(processo, CatalogoDoResultado(AtoDaClassificacao), CatalogoDoPrazoAncoradoEmAto());
        EtapaProcessoInput etapa = ProvaPublicandoAClassificacao(comAncora: true) with { Produtos = [Definitivo(AtoDaClassificacao), Preliminar(AtoDaClassificacao)] };

        Result<MutacaoAceita> result = await cenario.DefinirAsync(Definindo(processo, etapa));

        result.IsSuccess.Should().BeTrue(result.Error?.Message);
    }
}
