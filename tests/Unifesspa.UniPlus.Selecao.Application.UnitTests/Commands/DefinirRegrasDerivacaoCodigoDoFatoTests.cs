namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

using static ApoioDaDefinicaoDasRegrasDeDerivacao;

/// <summary>
/// O código do fato derivado ausente, nulo, vazio ou em branco é recusado pelo domínio no campo do pedido, junto com as
/// demais recusas de forma, sem consultar o vocabulário do fato ausente.
/// </summary>
public sealed class DefinirRegrasDerivacaoCodigoDoFatoTests
{
    [Theory(DisplayName = "O código do fato ausente, nulo, vazio ou em branco é recusado no campo, com o código próprio")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_CodigoDoFatoAusenteOuEmBranco_RecusaOCampo(string? codigoFato)
    {
        ProcessoSeletivo processo = ProcessoBase();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id, [new ConfiguracaoDerivacaoInput(codigoFato, [new RegraDerivacaoInput(0, "AC", null)])]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(static e => (e.Field, e.Error.Code)).Should().Equal(
            ("configuracoes[0].codigoFato", ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoObrigatorio));
    }

    [Fact(DisplayName = "O código do fato ausente acumula com a ordem inválida da regra e com o código ausente de outra configuração")]
    public async Task Handle_CodigoDoFatoAusenteComOutrasRecusas_Acumula()
    {
        ProcessoSeletivo processo = ProcessoBase();
        Mocks mocks = NovosMocks(processo, processo.Id);
        DefinirRegrasDerivacaoCommand command = new(processo.Id,
        [
            new ConfiguracaoDerivacaoInput(null, [new RegraDerivacaoInput(-1, "AC", null)]),
            new ConfiguracaoDerivacaoInput(null, [new RegraDerivacaoInput(0, "AC", null)]),
        ]);

        Result resultado = await HandleAsync(mocks, command);

        resultado.Errors.Select(static e => e.Field).Should().Equal(
            "configuracoes[0].codigoFato", "configuracoes[0].regras[0].ordem", "configuracoes[1].codigoFato");
    }
}
