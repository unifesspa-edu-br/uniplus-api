namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.Rascunhos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.Rascunhos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O que a exclusão do rascunho faz com os documentos a publicar dele: apaga as linhas na mesma gravação e devolve, em
/// cascata, a mensagem que remove os objetos do armazenamento.
/// </summary>
public sealed class ExcluirRascunhoComDocumentosAPublicarTests
{
    private static readonly string[] Chaves = ["selecao/documentos-a-publicar/p/d.pdf", "selecao/documentos-a-publicar/p/d/confirmado.pdf"];

    private readonly CenarioDoRascunho _cenario = new();
    private readonly IModeloDeDocumentoRepository _modelos = Substitute.For<IModeloDeDocumentoRepository>();

    private Task<(Result Resposta, IEnumerable<object> Mensagens)> Excluir(RascunhoDeProcessoSeletivo rascunho, PrecondicaoIfMatch? precondicao = null) =>
        ExcluirRascunhoDeProcessoSeletivoCommandHandler.Handle(
            new ExcluirRascunhoDeProcessoSeletivoCommand(rascunho.Id, precondicao ?? CenarioDoRascunho.Precondicao(rascunho)),
            _cenario.Bloqueio, _cenario.Rascunhos, _cenario.Documentos, _modelos, _cenario.UnitOfWork, CancellationToken.None);

    private RascunhoDeProcessoSeletivo ComDocumentos(params string[] chaves)
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps);
        _cenario.Documentos.RemoverDoProcessoAsync(rascunho.Id, Arg.Any<CancellationToken>()).Returns(chaves);
        return rascunho;
    }

    [Fact(DisplayName = "Apaga os documentos do identificador e o rascunho na mesma gravação")]
    public async Task ApagaOsDocumentosEORascunhoNaMesmaGravacao()
    {
        RascunhoDeProcessoSeletivo rascunho = ComDocumentos(Chaves);

        await Excluir(rascunho);

        Received.InOrder(() =>
        {
            _ = _cenario.Documentos.RemoverDoProcessoAsync(rascunho.Id, Arg.Any<CancellationToken>());
            _cenario.Rascunhos.Remover(rascunho);
            _ = _cenario.UnitOfWork.SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
        });
    }

    [Fact(DisplayName = "Devolve a mensagem de remoção com as chaves exatas dos documentos")]
    public async Task DevolveAMensagemComAsChaves()
    {
        RascunhoDeProcessoSeletivo rascunho = ComDocumentos(Chaves);

        (Result resposta, IEnumerable<object> mensagens) = await Excluir(rascunho);

        resposta.IsSuccess.Should().BeTrue();
        mensagens.Should().ContainSingle().Which.Should().BeOfType<RemoverArquivosDoRascunhoExcluido>()
            .Which.Chaves.Should().Equal(Chaves);
    }

    [Fact]
    public async Task Excluir_ComDocumentosEModelos_ReuneAsChavesNaMesmaMensagem()
    {
        RascunhoDeProcessoSeletivo rascunho = ComDocumentos(Chaves);
        string[] chavesDosModelos = ["modelos/envio", "modelos/copia"];
        _modelos.RemoverDoProcessoAsync(rascunho.Id, Arg.Any<CancellationToken>()).Returns(chavesDosModelos);

        (Result resposta, IEnumerable<object> mensagens) = await Excluir(rascunho);

        resposta.IsSuccess.Should().BeTrue();
        mensagens.Should().ContainSingle().Which.Should().BeOfType<RemoverArquivosDoRascunhoExcluido>()
            .Which.Chaves.Should().Equal([.. Chaves, .. chavesDosModelos]);
        await _modelos.Received(1).RemoverDoProcessoAsync(rascunho.Id, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem documentos não há objeto a remover e a mensagem não sai")]
    public async Task SemDocumentos_SemMensagem()
    {
        RascunhoDeProcessoSeletivo rascunho = ComDocumentos();

        (Result resposta, IEnumerable<object> mensagens) = await Excluir(rascunho);

        resposta.IsSuccess.Should().BeTrue();
        mensagens.Should().BeEmpty();
    }

    [Fact(DisplayName = "A exclusão recusada não apaga documento nem devolve mensagem")]
    public async Task ExclusaoRecusada_NaoApagaNemAgenda()
    {
        RascunhoDeProcessoSeletivo rascunho = ComDocumentos(Chaves);

        (Result resposta, IEnumerable<object> mensagens) = await Excluir(rascunho, PrecondicaoIfMatch.DeTags(["\"4\""]));

        resposta.IsFailure.Should().BeTrue();
        mensagens.Should().BeEmpty();
        await _cenario.Documentos.DidNotReceiveWithAnyArgs().RemoverDoProcessoAsync(default, default);
        await _modelos.DidNotReceiveWithAnyArgs().RemoverDoProcessoAsync(default, default);
    }

    [Fact(DisplayName = "A mensagem espera a URL de envio expirar e a tolerância da rotina dos pendentes vencidos")]
    public void AtrasoDaMensagem_EOPrazoDaUrlMaisATolerancia() =>
        RemoverArquivosDoRascunhoExcluido.Atraso.Should().Be(TimeSpan.FromMinutes(15) + TimeSpan.FromHours(1));
}
