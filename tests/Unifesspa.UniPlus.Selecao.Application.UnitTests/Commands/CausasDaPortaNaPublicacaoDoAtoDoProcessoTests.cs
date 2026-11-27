namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDaPublicacaoDoProdutoDoEdital;

/// <summary>
/// A porta de escrita do registro central pode recusar o ato por mais de uma causa: a publicação do ato do processo
/// devolve todas, na ordem em que a porta as deu, descarta o que foi rastreado e não grava nada.
/// </summary>
public sealed class CausasDaPortaNaPublicacaoDoAtoDoProcessoTests
{
    private static readonly DomainError PrimeiraCausa = new("AtoNormativo.TextoInvalido", "O órgão é obrigatório e deve ter no máximo 200 caracteres.");
    private static readonly DomainError SegundaCausa = new("AtoNormativo.HashDoDocumentoInvalido", "O hash do documento deve ser um SHA-256 em hexadecimal minúsculo.");

    private readonly CenarioDaPublicacaoDoProdutoDoEdital _cenario = new();

    [Fact(DisplayName = "A porta que recusa com duas causas as devolve na ordem, descarta o rastreado e não grava")]
    public async Task PortaComDuasCausas_DevolveAsDuasNaOrdemENaoGrava()
    {
        (ProdutoDaFase preliminar, _) = Par();
        _cenario.Cronograma(Fase("HOMOLOGACAO_INSCRICOES", 1, preliminar));
        _cenario.Registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>())
            .Returns(Result.ValidationFailure([new FieldError(null, PrimeiraCausa), new FieldError(null, SegundaCausa)]));

        (Result<AtoPublicadoDto> resposta, IEnumerable<object> eventos) = await _cenario.Publicar(_cenario.Comando(preliminar.Id));

        resposta.Errors.Select(recusa => recusa.Error).Should().Equal(PrimeiraCausa, SegundaCausa);
        eventos.Should().BeEmpty();
        _cenario.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await _cenario.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
