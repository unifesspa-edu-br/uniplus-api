namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.AtosDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// A publicação de um item do edital sem a data do ato, sem o documento principal ou com um anexo sem documento: a
/// recusa é 422 com o código de campo obrigatório, no campo do pedido. Faz parte da forma do pedido, que vem primeiro e
/// acumula com a recusa de que o processo ou o item não existem, que não dependem dela; o documento ausente não é lido.
/// </summary>
public sealed class CampoObrigatorioAusenteNoAtoDoProcessoTests
{
    private readonly CenarioDaPublicacaoDoProdutoDoEdital _cenario = new();

    [Fact(DisplayName = "A data do ato ausente é recusada no campo do pedido, e nada é gravado")]
    public async Task DataAusente_Recusa()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> mensagens) =
            await _cenario.Publicar(_cenario.Comando(preliminar.Id) with { DataDoAto = null });

        Recusa(resposta).Should().Equal(("dataDoAto", CodigosDeValidacao.Obrigatorio));
        mensagens.Should().BeEmpty();
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O documento principal ausente é recusado no campo do pedido, e os documentos não são lidos")]
    public async Task DocumentoPrincipalAusente_Recusa()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();

        (Result<AtoPublicadoDto> resposta, _) =
            await _cenario.Publicar(_cenario.Comando(preliminar.Id) with { DocumentoPrincipalId = null });

        Recusa(resposta).Should().Equal(("documentoPrincipalId", CodigosDeValidacao.Obrigatorio));
        await _cenario.Documentos.DidNotReceive().ListarDoProcessoAsync(
            Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Cada anexo sem documento é recusado na posição dele")]
    public async Task AnexoSemDocumento_RecusaNaPosicao()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        AnexoSolicitadoDoAto[] anexos = [new(null, "A"), new(null, "B")];

        (Result<AtoPublicadoDto> resposta, _) =
            await _cenario.Publicar(_cenario.Comando(preliminar.Id) with { Anexos = anexos });

        Recusa(resposta).Should().Equal(
            ("anexos[0].documentoId", CodigosDeValidacao.Obrigatorio), ("anexos[1].documentoId", CodigosDeValidacao.Obrigatorio));
    }

    [Fact(DisplayName = "A data, o documento principal e o anexo ausentes acumulam, a data primeiro")]
    public async Task VariosCamposAusentes_AcumulamNaOrdemDoPedido()
    {
        (ProdutoDaFase preliminar, _) = _cenario.ParNaMesmaFase();
        PublicarProdutoDoEditalCommand comando = _cenario.Comando(preliminar.Id) with
        {
            DataDoAto = null,
            DocumentoPrincipalId = null,
            Anexos = [new(null, "A")],
        };

        (Result<AtoPublicadoDto> resposta, _) = await _cenario.Publicar(comando);

        Recusa(resposta).Select(static r => r.Campo).Should().Equal("dataDoAto", "documentoPrincipalId", "anexos[0].documentoId");
        resposta.Errors.Should().OnlyContain(static e => e.Error.Code == CodigosDeValidacao.Obrigatorio);
    }

    [Fact(DisplayName = "A data ausente acumula com a recusa do item que o edital não declara, e a data vem primeiro")]
    public async Task DataAusenteEItemNaoDeclarado_Acumulam()
    {
        _cenario.ParNaMesmaFase();

        (Result<AtoPublicadoDto> resposta, _) =
            await _cenario.Publicar(_cenario.Comando(Guid.CreateVersion7()) with { DataDoAto = null });

        Recusa(resposta).Should().Equal(
            ("dataDoAto", CodigosDeValidacao.Obrigatorio), (null, AtoDoProcessoErrorCodes.ProdutoNaoDeclarado));
    }

    [Fact(DisplayName = "O processo sem página pública é recusado depois da forma, na mesma resposta, sem campo")]
    public async Task ProcessoSemPaginaPublica_RecusaDepoisDaForma()
    {
        _cenario.Divulgados.ObterParaMutacaoAsync(_cenario.ProcessoId, Arg.Any<CancellationToken>()).Returns((CertameDivulgado?)null);

        (Result<AtoPublicadoDto> resposta, _) =
            await _cenario.Publicar(_cenario.Comando(Guid.CreateVersion7()) with { DataDoAto = null, DocumentoPrincipalId = null });

        Recusa(resposta).Should().Equal(
            ("dataDoAto", CodigosDeValidacao.Obrigatorio),
            ("documentoPrincipalId", CodigosDeValidacao.Obrigatorio),
            (null, AtoDoProcessoErrorCodes.CertameNaoDivulgado));
    }

    private static (string? Campo, string Codigo)[] Recusa(Result<AtoPublicadoDto> resposta) =>
        [.. resposta.Errors.Select(static e => (e.Field, e.Error.Code))];
}
