namespace Unifesspa.UniPlus.Configuracao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;
using Unifesspa.UniPlus.Configuracao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Interfaces;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>O histórico das manifestações técnicas do item, de todos os ciclos, com a situação de cada uma.</summary>
public sealed class ListarManifestacoesDoItemDaProtecaoQueryHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IManifestacaoDaProtecaoRepository _manifestacoes = Substitute.For<IManifestacaoDaProtecaoRepository>();

    [Fact(DisplayName = "O histórico mostra a substituída e a vigente, com o nome de quem se manifestou e sem o identificador")]
    public async Task Tipo_HistoricoComASituacao()
    {
        ItemSobAprovacaoBuilder montagem = new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("RECOMENDA_NAO_USO").ComManifestacao("RECOMENDA_AJUSTE");
        TipoDocumento tipo = montagem.Tipo();
        _tipos.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        _manifestacoes.ListarDoItemAsync(new ItemDaProtecao(CadastroDoCatalogo.TipoDocumento, tipo.Id), Arg.Any<CancellationToken>()).Returns(montagem.Eventos);

        Result<IReadOnlyList<ManifestacaoDaProtecaoDto>> resultado = await ListarAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, tipo.Id);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Select(m => (m.Parecer, m.Situacao, m.AutorNome, m.Ciclo)).Should().Equal(
            ("RECOMENDA_NAO_USO", "SUBSTITUIDA", "Kevin Anderson Carvalho", 1),
            ("RECOMENDA_AJUSTE", "VIGENTE", "Kevin Anderson Carvalho", 1));
        typeof(ManifestacaoDaProtecaoDto).GetProperties().Select(p => p.Name).Should().NotContain(["AutorContaId", "AutorEmissor", "ContaId", "Emissor"]);
    }

    [Fact(DisplayName = "A manifestação de ciclo anterior ao corrente sai encerrada")]
    public async Task Fato_CicloAnterior_Encerrada()
    {
        ItemSobAprovacaoBuilder montagem = new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("RECOMENDA_APROVACAO", texto: null);
        FatoCandidato fato = montagem.Fato();
        fato.DevolverProtecao("Dado desnecessário.", "helena.rezende", DateTimeOffset.UtcNow);
        fato.AlterarDescritivo("Renda familiar", "Renda bruta declarada.", null);
        _fatos.ObterPorIdParaLeituraAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);
        _manifestacoes.ListarDoItemAsync(new ItemDaProtecao(CadastroDoCatalogo.Fato, fato.Id), Arg.Any<CancellationToken>()).Returns(montagem.Eventos);

        Result<IReadOnlyList<ManifestacaoDaProtecaoDto>> resultado = await ListarAsync(CadastroDaProtecaoDoCatalogo.Fato, fato.Id);

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value.Should().ContainSingle().Which.Situacao.Should().Be("ENCERRADA");
    }

    [Fact(DisplayName = "O fato que não existe recusa com o erro de dado do candidato não encontrado, sem ler o registro")]
    public async Task FatoInexistente_RecusaComDadoDoCandidatoNaoEncontrado()
    {
        Result<IReadOnlyList<ManifestacaoDaProtecaoDto>> resultado = await ListarAsync(CadastroDaProtecaoDoCatalogo.Fato, Guid.NewGuid());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.NaoEncontrado);
        await _manifestacoes.DidNotReceiveWithAnyArgs().ListarDoItemAsync(default, default);
    }

    [Fact(DisplayName = "O tipo de documento que não existe recusa com o erro de tipo de documento não encontrado, sem ler o registro")]
    public async Task TipoInexistente_RecusaComTipoDeDocumentoNaoEncontrado()
    {
        Result<IReadOnlyList<ManifestacaoDaProtecaoDto>> resultado = await ListarAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, Guid.NewGuid());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(TipoDocumentoErrorCodes.NaoEncontrado);
        await _manifestacoes.DidNotReceiveWithAnyArgs().ListarDoItemAsync(default, default);
    }

    private Task<Result<IReadOnlyList<ManifestacaoDaProtecaoDto>>> ListarAsync(CadastroDaProtecaoDoCatalogo cadastro, Guid id) =>
        ListarManifestacoesDoItemDaProtecaoQueryHandler.Handle(
            new ListarManifestacoesDoItemDaProtecaoQuery(cadastro, id), _tipos, _fatos, _manifestacoes, CancellationToken.None);
}
