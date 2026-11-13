namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;

using static Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport.CatalogoDeProtecaoDeTeste;

/// <summary>O histórico da proteção de um item, lido por quem mantém o cadastro dele (ADR-0153).</summary>
public sealed class ListarAtosDoItemDaProtecaoQueryHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IAtoDaProtecaoDoCatalogoRepository _atos = Substitute.For<IAtoDaProtecaoDoCatalogoRepository>();

    private Task<IReadOnlyList<AtoDaProtecaoDto>?> ListarAsync(CadastroDaProtecaoDoCatalogo cadastro, Guid id) =>
        ListarAtosDoItemDaProtecaoQueryHandler.Handle(
            new ListarAtosDoItemDaProtecaoQuery(cadastro, id), _tipos, _fatos, _atos, CancellationToken.None);

    [Fact(DisplayName = "O fato lê só os atos do próprio cadastro")]
    public async Task Fato_LeOsAtosDoFato()
    {
        FatoCandidato fato = NovoFato();
        _fatos.ObterPorIdParaLeituraAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);
        _atos.ListarDoItemAsync(CadastroDoCatalogo.Fato, fato.Id, Arg.Any<CancellationToken>()).Returns(
        [
            AtoDaProtecaoDoCatalogo.Registrar(Retrato(fato), AtoDaProtecao.Solicitacao, new AtorDoAto(Emissor, Isabella, "Isabella Priscila Figueiredo"), Agora),
        ]);

        IReadOnlyList<AtoDaProtecaoDto>? atos = await ListarAsync(CadastroDaProtecaoDoCatalogo.Fato, fato.Id);

        atos.Should().ContainSingle().Which.Ato.Should().Be("SOLICITACAO");
        await _tipos.DidNotReceive().ObterPorIdParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O item que não existe responde nulo, sem ler o registro")]
    public async Task Inexistente_RespondeNulo()
    {
        (await ListarAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, Guid.NewGuid())).Should().BeNull();
        (await ListarAsync(CadastroDaProtecaoDoCatalogo.Fato, Guid.NewGuid())).Should().BeNull();
        await _atos.DidNotReceiveWithAnyArgs().ListarDoItemAsync(default, default, default);
    }
}
