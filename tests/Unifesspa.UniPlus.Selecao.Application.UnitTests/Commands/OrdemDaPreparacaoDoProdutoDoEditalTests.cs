namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

public sealed class OrdemDaPreparacaoDoProdutoDoEditalTests
{
    private readonly CenarioDaPublicacaoDoProdutoDoEdital _cenario = new();

    [Fact]
    public async Task Handle_ItemNaoDeclaradoEDocumentoAusente_InterrompeNoItem()
    {
        _cenario.ParNaMesmaFase();
        await _cenario.DeveRecusar(_cenario.Comando(Guid.CreateVersion7()) with { DocumentoPrincipalId = Guid.CreateVersion7() },
            AtoDoProcessoErrorCodes.ProdutoNaoDeclarado);
        await _cenario.Tipos.DidNotReceive().ObterVigenteAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        await NaoLeuDocumentos();
    }

    [Fact]
    public async Task Handle_TipoAusenteEPoliticaNaoConfirmada_InterrompeNoTipo()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Tipos.ObterVigenteAsync(Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>()).Returns((Publicacoes.Contracts.TipoAtoPublicadoView?)null);
        await _cenario.DeveRecusar(_cenario.Comando(preliminar.Id, politica: false), "ProcessoSeletivo.TipoDeAtoSemVersaoVigente");
        await NaoLeuDocumentos();
    }

    [Fact]
    public async Task Handle_ProdutoJaPublicadoEPoliticaNaoConfirmada_InterrompeNaPublicacao()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        _cenario.Publicado(CenarioDaPublicacaoDoProdutoDoEdital.Materia, preliminar.Id);
        await _cenario.DeveRecusar(_cenario.Comando(preliminar.Id, politica: false), AtoDoProcessoErrorCodes.ProdutoJaPublicado);
        await NaoLeuDocumentos();
    }

    [Fact]
    public async Task Handle_PoliticaNaoConfirmadaEDocumentoAusente_InterrompeNaPolitica()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        await _cenario.DeveRecusar(_cenario.Comando(preliminar.Id, politica: false) with { DocumentoPrincipalId = Guid.CreateVersion7() },
            AtoDoProcessoErrorCodes.PoliticaDeDivulgacaoNaoConfirmada);
        await NaoLeuDocumentos();
    }

    [Fact]
    public async Task Handle_ProdutoPublicado_TravaAntesDasLeiturasESalvaUmaVez()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(_cenario.Comando(preliminar.Id));
        resposta.IsSuccess.Should().BeTrue();
        Received.InOrder(() =>
        {
            _ = _cenario.Processos.TravarAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
            _ = _cenario.Processos.ObterSiglaDaUnidadeAdministradoraAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>());
        });
        await _cenario.UnitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_CorrecaoSemMotivo_RecusaAntesDeTravar()
    {
        PublicarProdutoDoEditalCommand comando = _cenario.Comando(Guid.CreateVersion7(), corrige: Guid.CreateVersion7()) with { MotivoDaCorrecao = null };
        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(comando);
        resposta.IsFailure.Should().BeTrue();
        await _cenario.Processos.DidNotReceive().TravarAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _cenario.Processos.DidNotReceive().ObterSiglaDaUnidadeAdministradoraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    private async Task NaoLeuDocumentos() =>
        await _cenario.Documentos.DidNotReceive().ListarDoProcessoAsync(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
}
