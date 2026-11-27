namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.CenarioDeRecusaNoCongelamento;

/// <summary>
/// A porta de escrita do registro central pode recusar o ato da versão por mais de uma causa: o congelamento devolve
/// todas, na ordem em que a porta as deu, descarta o que foi rastreado e não grava nem deixa evento.
/// </summary>
public sealed class CausasDaPortaNoCongelamentoDaVersaoTests
{
    private static readonly DomainError PrimeiraCausa = new("AtoNormativo.TextoInvalido", "O órgão é obrigatório e deve ter no máximo 200 caracteres.");
    private static readonly DomainError SegundaCausa = new("AtoNormativo.HashDoDocumentoInvalido", "O hash do documento deve ser um SHA-256 em hexadecimal minúsculo.");

    public static TheoryData<OperacaoQueCongela> Atos() =>
        [OperacaoQueCongela.Publicar, OperacaoQueCongela.Retificar];

    [Theory(DisplayName = "A porta que recusa com duas causas as devolve na ordem, descarta o rastreado e não deixa evento nem gravação")]
    [MemberData(nameof(Atos))]
    public async Task PortaComDuasCausas_DevolveAsDuasNaOrdemESemEvento(OperacaoQueCongela operacao)
    {
        CenarioDeRecusaNoCongelamento cenario = Para(operacao);
        cenario.Servicos.Registro.RegistrarAsync(Arg.Any<AtoNormativoPublicado>(), Arg.Any<CancellationToken>())
            .Returns(Result.ValidationFailure([new FieldError(null, PrimeiraCausa), new FieldError(null, SegundaCausa)]));

        (Result resposta, IEnumerable<object> eventos) = await cenario.ExecutarAsync();

        resposta.Errors.Select(recusa => recusa.Error).Should().Equal(PrimeiraCausa, SegundaCausa);
        eventos.Should().BeEmpty();
        cenario.Servicos.UnitOfWork.Received(1).DescartarAlteracoesNaoSalvas();
        await cenario.Servicos.UnitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }
}
