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

/// <summary>O histórico das manifestações técnicas do item, de todos os ciclos, com a situação de cada uma.</summary>
public sealed class ListarManifestacoesDoItemDaProtecaoQueryHandlerTests
{
    private readonly ITipoDocumentoRepository _tipos = Substitute.For<ITipoDocumentoRepository>();
    private readonly IFatoCandidatoRepository _fatos = Substitute.For<IFatoCandidatoRepository>();
    private readonly IManifestacaoDaProtecaoRepository _manifestacoes = Substitute.For<IManifestacaoDaProtecaoRepository>();

    [Fact(DisplayName = "O histórico mostra a substituída e a vigente, com o nome de quem se manifestou e sem o identificador")]
    public async Task Tipo_HistoricoComASituacao()
    {
        ItemSobAprovacaoBuilder montagem = new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("INADEQUADA").ComManifestacao("RECOMENDA_AJUSTE");
        TipoDocumento tipo = montagem.Tipo();
        _tipos.ObterPorIdParaLeituraAsync(tipo.Id, Arg.Any<CancellationToken>()).Returns(tipo);
        _manifestacoes.ListarDoItemAsync(new ItemDaProtecao(CadastroDoCatalogo.TipoDocumento, tipo.Id), Arg.Any<CancellationToken>()).Returns(montagem.Eventos);

        IReadOnlyList<ManifestacaoDaProtecaoDto>? historico = await ListarAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, tipo.Id);

        historico!.Select(m => (m.Parecer, m.Situacao, m.AutorNome, m.Ciclo)).Should().Equal(
            ("INADEQUADA", "SUBSTITUIDA", "Kevin Anderson Carvalho", 1),
            ("RECOMENDA_AJUSTE", "VIGENTE", "Kevin Anderson Carvalho", 1));
        typeof(ManifestacaoDaProtecaoDto).GetProperties().Select(p => p.Name).Should().NotContain(["AutorSub", "AutorEmissor", "Sub", "Emissor"]);
    }

    [Fact(DisplayName = "A manifestação de ciclo anterior ao corrente sai encerrada")]
    public async Task Fato_CicloAnterior_Encerrada()
    {
        ItemSobAprovacaoBuilder montagem = new ItemSobAprovacaoBuilder().Solicitado().ComManifestacao("ADEQUADA", texto: null);
        FatoCandidato fato = montagem.Fato();
        fato.ReprovarProtecao("Dado desnecessário.", "helena.rezende", DateTimeOffset.UtcNow);
        fato.AlterarDescritivo("Renda familiar", "Renda bruta declarada.", null);
        _fatos.ObterPorIdParaLeituraAsync(fato.Id, Arg.Any<CancellationToken>()).Returns(fato);
        _manifestacoes.ListarDoItemAsync(new ItemDaProtecao(CadastroDoCatalogo.Fato, fato.Id), Arg.Any<CancellationToken>()).Returns(montagem.Eventos);

        IReadOnlyList<ManifestacaoDaProtecaoDto>? historico = await ListarAsync(CadastroDaProtecaoDoCatalogo.Fato, fato.Id);

        historico.Should().ContainSingle().Which.Situacao.Should().Be("ENCERRADA");
    }

    [Fact(DisplayName = "O item que não existe responde nulo, sem ler o registro")]
    public async Task Inexistente_RespondeNulo()
    {
        (await ListarAsync(CadastroDaProtecaoDoCatalogo.TipoDocumento, Guid.NewGuid())).Should().BeNull();
        (await ListarAsync(CadastroDaProtecaoDoCatalogo.Fato, Guid.NewGuid())).Should().BeNull();
        await _manifestacoes.DidNotReceiveWithAnyArgs().ListarDoItemAsync(default, default);
    }

    private Task<IReadOnlyList<ManifestacaoDaProtecaoDto>?> ListarAsync(CadastroDaProtecaoDoCatalogo cadastro, Guid id) =>
        ListarManifestacoesDoItemDaProtecaoQueryHandler.Handle(
            new ListarManifestacoesDoItemDaProtecaoQuery(cadastro, id), _tipos, _fatos, _manifestacoes, CancellationToken.None);
}
