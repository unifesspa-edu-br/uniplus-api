namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>
/// O item da fila da proteção de dados visto por inteiro, no cadastro a que pertence (ADR-0153), com o ciclo e as
/// manifestações técnicas vigentes (ADR-0163).
/// </summary>
public sealed class ObterDetalheDaProtecaoDoCatalogoQueryHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos = Substitute.For<IAtoDaProtecaoDoCatalogoRepository>();
    private readonly IManifestacaoDaProtecaoRepository _manifestacoes = Substitute.For<IManifestacaoDaProtecaoRepository>();

    [Fact(DisplayName = "O fato sai com o bloco do fato e sem o do tipo de documento")]
    public async Task Fato_SaiComOBlocoDoFato()
    {
        FatoCandidato fato = NovoFato();
        _fatos.ObterPorIdParaLeituraAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);

        DetalheDaProtecaoDoCatalogoDto? detalhe = await ObterAsync(CadastroDaProtecaoDoCatalogo.Fato, fato.Id);

        detalhe!.Tipo.Should().Be("FATO");
        detalhe.Descricao.Should().Be("Descrição do fato.");
        detalhe.Fato.Should().NotBeNull();
        detalhe.Fato!.Dominio.Should().Be("BOOLEANO");
        detalhe.Fato.Valores.Should().BeEmpty();
        detalhe.TipoDocumento.Should().BeNull();
        await _tipos.DidNotReceive().ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O tipo de documento sai com o bloco do tipo e sem o do fato")]
    public async Task Tipo_SaiComOBlocoDoTipo()
    {
        TipoDocumento tipo = NovoTipo();
        _tipos.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);

        DetalheDaProtecaoDoCatalogoDto? detalhe = await ObterAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, tipo.Id);

        detalhe!.Tipo.Should().Be("TIPO_DOCUMENTO");
        detalhe.TipoDocumento!.Categoria.Should().Be("SAUDE");
        detalhe.Fato.Should().BeNull();
        await _fatos.DidNotReceive().ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O detalhe traz todos os atos do item, sem o identificador de quem agiu")]
    public async Task Detalhe_TrazOsAtosDoItem()
    {
        TipoDocumento tipo = NovoTipo();
        _tipos.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        _atos.ListarDoItemAsync(CadastroDoCatalogo.TipoDocumento, tipo.Id, Arg.Any<CancellationToken>()).Returns(
        [
            AtoDaProtecaoDoCatalogo.Registrar(Retrato(tipo), AtoDaProtecao.Solicitacao, new AtorDoAto(Emissor, Isabella, "Isabella Priscila Figueiredo"), Agora),
            AtoDaProtecaoDoCatalogo.RegistrarDecisao(Retrato(tipo), Ajuste("Traz dado de saúde."), ItemSobAprovacaoBuilder.Lucia, Agora.AddHours(1)),
        ]);

        DetalheDaProtecaoDoCatalogoDto? detalhe = await ObterAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, tipo.Id);

        detalhe!.Atos.Select(a => (a.Ato, a.AtorNome, a.ClassificacaoRecomendada, a.Justificativa, a.Ciclo)).Should().Equal(
            ("SOLICITACAO", "Isabella Priscila Figueiredo", null, null, 1),
            ("AJUSTE_RECOMENDADO", "Lúcia Bruna Maya Almeida", "SENSIVEL", "Traz dado de saúde.", 1));
        typeof(AtoDaProtecaoDto).GetProperties().Select(p => p.Name).Should().NotContain(["AtorSub", "AtorEmissor", "Sub", "Emissor"]);
    }

    [Fact(DisplayName = "O detalhe traz o ciclo, o estado das manifestações e as vigentes do ciclo corrente")]
    public async Task Detalhe_TrazAsManifestacoesVigentes()
    {
        ItemSobAprovacaoBuilder montagem = new ItemSobAprovacaoBuilder().Solicitado()
            .ComManifestacao("INADEQUADA").ComManifestacao("ADEQUADA", texto: null).ComManifestacao("RECOMENDA_AJUSTE", autor: ItemSobAprovacaoBuilder.Lucia);
        FatoCandidato fato = montagem.Fato();
        _fatos.ObterPorIdParaLeituraAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);
        _manifestacoes.ListarDoCicloAsync(new ItemDaProtecao(CadastroDoCatalogo.Fato, fato.Id), fato.Aprovacao.Ciclo, Arg.Any<CancellationToken>())
            .Returns(montagem.Eventos);

        DetalheDaProtecaoDoCatalogoDto? detalhe = await ObterAsync(CadastroDaProtecaoDoCatalogo.Fato, fato.Id);

        (detalhe!.Aprovacao.Ciclo, detalhe.Aprovacao.ManifestacaoDesfavoravel, detalhe.Aprovacao.ManifestadaEm).Should().Be((1, true, montagem.Eventos[^1].Instante));
        detalhe.ManifestacoesVigentes.Select(m => (m.Parecer, m.AutorNome, m.SubstituiId, m.Situacao)).Should().Equal(
            ("ADEQUADA", "Kevin Anderson Carvalho", montagem.Eventos[0].Id, "VIGENTE"),
            ("RECOMENDA_AJUSTE", "Lúcia Bruna Maya Almeida", (Guid?)null, "VIGENTE"));
    }

    [Fact(DisplayName = "O item sem atos sai com a lista vazia")]
    public async Task Detalhe_SemAtos_ListaVazia()
    {
        FatoCandidato fato = NovoFato();
        _fatos.ObterPorIdParaLeituraAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);
        _atos.ListarDoItemAsync(CadastroDoCatalogo.Fato, fato.Id, Arg.Any<CancellationToken>()).Returns([]);

        (await ObterAsync(CadastroDaProtecaoDoCatalogo.Fato, fato.Id))!.Atos.Should().BeEmpty();
    }

    [Fact(DisplayName = "O item que não existe no cadastro responde nulo")]
    public async Task Inexistente_RespondeNulo()
    {
        (await ObterAsync(CadastroDaProtecaoDoCatalogo.Fato, Guid.NewGuid())).Should().BeNull();
        (await ObterAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, Guid.NewGuid())).Should().BeNull();
    }

    private Task<DetalheDaProtecaoDoCatalogoDto?> ObterAsync(CadastroDaProtecaoDoCatalogo cadastro, Guid id) =>
        ObterDetalheDaProtecaoDoCatalogoQueryHandler.Handle(
            new ObterDetalheDaProtecaoDoCatalogoQuery(cadastro, id), _tipos, _fatos, _atos, _manifestacoes, CancellationToken.None);

    private static DecisaoFundamentada Ajuste(string justificativa) =>
        new(AtoDaProtecao.AjusteRecomendado, ClassificacaoDeProtecao.Sensivel, Fundamentacao.Analisar(justificativa));
}
