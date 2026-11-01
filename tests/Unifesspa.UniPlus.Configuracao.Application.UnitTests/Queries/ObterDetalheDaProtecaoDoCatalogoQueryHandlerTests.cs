namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>O item da fila da proteção de dados visto por inteiro, no cadastro a que pertence (ADR-0153).</summary>
public sealed class ObterDetalheDaProtecaoDoCatalogoQueryHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();

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

    [Fact(DisplayName = "O item que não existe no cadastro responde nulo")]
    public async Task Inexistente_RespondeNulo()
    {
        (await ObterAsync(CadastroDaProtecaoDoCatalogo.Fato, Guid.NewGuid())).Should().BeNull();
        (await ObterAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, Guid.NewGuid())).Should().BeNull();
    }

    private Task<DetalheDaProtecaoDoCatalogoDto?> ObterAsync(CadastroDaProtecaoDoCatalogo cadastro, Guid id) =>
        ObterDetalheDaProtecaoDoCatalogoQueryHandler.Handle(
            new ObterDetalheDaProtecaoDoCatalogoQuery(cadastro, id), _tipos, _fatos, CancellationToken.None);
}
