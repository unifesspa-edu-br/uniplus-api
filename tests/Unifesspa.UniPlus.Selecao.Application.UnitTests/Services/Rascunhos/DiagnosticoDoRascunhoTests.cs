namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services.Rascunhos;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.CadastroDeReserva;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Manifestacao;

/// <summary>
/// O diagnóstico do rascunho junta as recusas do cadastro de reserva e da manifestação de interesse, pelas regras de domínio, às
/// citações de modelos não resolvidas, numa ordem só, a do documento.
/// </summary>
public sealed class DiagnosticoDoRascunhoTests
{
    private readonly CenarioDoRascunho _cenario = new();

    private async Task<IReadOnlyList<FieldError>> DiagnosticarAsync(string documento)
    {
        RascunhoDeProcessoSeletivo rascunho = _cenario.ExisteUmRascunhoNaRevisao1(CenarioDoRascunho.Ceps, documento);
        return await _cenario.Diagnostico.DiagnosticarAsync(rascunho, CancellationToken.None);
    }

    private static (string? Ponteiro, string Codigo)[] Pares(IReadOnlyList<FieldError> causas) =>
        [.. causas.Select(static causa => (causa.Field, causa.Error.Code))];

    [Fact(DisplayName = "O rascunho sem os blocos e sem citações não tem causa e não consulta modelos")]
    public async Task RascunhoSemBlocos_NaoTemCausa()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarAsync(CenarioDoRascunho.DocumentoValido());

        causas.Should().BeEmpty();
        await _cenario.Modelos.DidNotReceiveWithAnyArgs().ListarDoProcessoAsync(default, default!, default);
    }

    [Fact(DisplayName = "O cadastro de reserva e a manifestação conformes não têm causa")]
    public async Task BlocosConformes_NaoTemCausa()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarAsync("""
            { "cadastroDeReserva": [ { "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 2, "participacaoDaReserva": "ANTECIPA_A_HABILITACAO" } ],
              "manifestacaoDeInteresse": "UMA_VEZ" }
            """);

        causas.Should().BeEmpty();
    }

    // O campo ausente do item vem depois dos campos que o item traz.
    [Fact(DisplayName = "Cada recusa do cadastro de reserva aponta o item e o campo no documento, todas de uma vez e na ordem do documento")]
    public async Task CadastroDeReservaRecusado_ApontaCadaItemECampo()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarAsync("""
            { "cadastroDeReserva": [
              { "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 2 },
              { "numeroDaChamada": 1, "pessoasDaReservaPorVaga": 0, "participacaoDaReserva": "ASSUME_NA_MESMA_CHAMADA" },
              { "pessoasDaReservaPorVaga": 1, "participacaoDaReserva": "CODIGO_QUE_NAO_EXISTE" } ] }
            """);

        Pares(causas).Should().Equal(
            ("/cadastroDeReserva/0/participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoAusente),
            ("/cadastroDeReserva/1/numeroDaChamada", CadastroDeReservaErrorCodes.ChamadaRepetida),
            ("/cadastroDeReserva/1/participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoSemReserva),
            ("/cadastroDeReserva/2/participacaoDaReserva", CadastroDeReservaErrorCodes.ParticipacaoDesconhecida),
            ("/cadastroDeReserva/2/numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido));
    }

    [Fact(DisplayName = "A manifestação de interesse desconhecida aponta o campo da manifestação, sem repetir o valor")]
    public async Task ManifestacaoDesconhecida_ApontaOCampo()
    {
        IReadOnlyList<FieldError> causas = await DiagnosticarAsync("""{ "manifestacaoDeInteresse": "VALOR-DIGITADO" }""");

        Pares(causas).Should().Equal(("/manifestacaoDeInteresse", ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido));
        causas.Single().Error.Message.Should().NotContain("VALOR-DIGITADO");
    }

    [Fact(DisplayName = "As causas do cadastro de reserva, da manifestação e dos modelos saem numa ordem só, a da posição no documento")]
    public async Task CausasDosTresBlocos_SaemNaOrdemDoDocumento()
    {
        Guid modeloAusente = Guid.CreateVersion7();
        _cenario.Modelos.ListarDoProcessoAsync(Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
        IReadOnlyList<FieldError> causas = await DiagnosticarAsync($$"""
            { "manifestacaoDeInteresse": "VALOR-DIGITADO",
              "documentosExigidos": { "exigencias": [ { "modeloId": "{{modeloAusente}}" } ] },
              "cadastroDeReserva": [ { "numeroDaChamada": 0, "pessoasDaReservaPorVaga": 0 } ] }
            """);

        Pares(causas).Should().Equal(
            ("/manifestacaoDeInteresse", ManifestacaoDeInteresseErrorCodes.CodigoDesconhecido),
            ("/documentosExigidos/exigencias/0/modeloId", ModeloDaExigenciaErrorCodes.ModeloNaoEncontrado),
            ("/cadastroDeReserva/0/numeroDaChamada", CadastroDeReservaErrorCodes.NumeroDaChamadaInvalido));
        await _cenario.Modelos.Received(1).ListarDoProcessoAsync(
            Arg.Any<Guid>(), Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O rascunho nulo é defeito de quem chama")]
    public async Task RascunhoNulo_Lanca()
    {
        Func<Task> diagnosticar = () => _cenario.Diagnostico.DiagnosticarAsync(null!, CancellationToken.None);

        await diagnosticar.Should().ThrowAsync<ArgumentNullException>();
    }
}
