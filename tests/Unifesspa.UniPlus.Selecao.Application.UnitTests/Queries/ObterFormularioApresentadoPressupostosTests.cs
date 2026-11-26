namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDoFormularioApresentado;

/// <summary>O que o <see cref="ObterFormularioApresentadoQueryHandler"/> serve do envelope congelado além do grafo: o código do formulário pedido, a data de referência dos fatos e o fuso do envio.</summary>
public sealed class ObterFormularioApresentadoPressupostosTests
{
    [Fact(DisplayName = "Processo sem o formulário do código pedido não serve formulário")]
    public async Task Handle_SemFormularioDoCodigo_NaoEncontrado()
    {
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioApresentadoDoProcessoDto> resultado = await HandleAsync(
            processoId, """{"documentosExigidos": {"dataDeReferenciaDosDados": null}}""",
            EnvelopeCom(Formulario(FormularioDeTeste.CodigoDaInscricao, null, comComprovacao: false)), "HABILITACAO");

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Theory(DisplayName = "Código de formulário fora do formato não designa formulário: é o mesmo não encontrado do formulário ausente")]
    [InlineData("inscricao")]
    [InlineData("formulário")]
    [InlineData("1INSCRICAO")]
    public async Task Handle_CodigoForaDoFormato_NaoEncontrado(string codigo)
    {
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioApresentadoDoProcessoDto> resultado = await HandleAsync(
            processoId, """{"documentosExigidos": {"dataDeReferenciaDosDados": null}}""",
            EnvelopeCom(Formulario(FormularioDeTeste.CodigoDaInscricao, null, comComprovacao: false)), codigo);

        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Theory(DisplayName = "A data de referência dos fatos sai do envelope congelado; fora da forma de data, a versão não tem apresentação")]
    [InlineData("\"2026-01-31\"", true)]
    [InlineData("null", true)]
    [InlineData("\"31/01/2026\"", false)]
    public async Task Handle_DataReferenciaFatos_SaiDoEnvelope(string data, bool valida)
    {
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioApresentadoDoProcessoDto> resultado = await HandleAsync(
            processoId, $$"""{"documentosExigidos": {"dataDeReferenciaDosDados": {{data}} } }""",
            EnvelopeCom(Formulario(FormularioDeTeste.CodigoDaInscricao, null, comComprovacao: false)), FormularioDeTeste.CodigoDaInscricao);

        if (valida)
        {
            resultado.Value!.Formulario.Pressupostos.DataReferenciaFatos.Should().Be(data == "null" ? null : new DateOnly(2026, 1, 31));
        }
        else
        {
            resultado.Error!.Code.Should().Be("CertamePublicado.EnvelopeInesperado");
        }
    }

    [Fact(DisplayName = "O formulário publica o fuso em que se conta o dia do envio: o do período de inscrição")]
    public async Task Handle_FusoDoEnvio_EODaJanela()
    {
        Guid processoId = Guid.CreateVersion7();

        Result<FormularioApresentadoDoProcessoDto> resultado = await HandleAsync(
            processoId, """{"documentosExigidos": {"dataDeReferenciaDosDados": null}}""",
            EnvelopeCom(Formulario(FormularioDeTeste.CodigoDaInscricao, null, comComprovacao: false)), FormularioDeTeste.CodigoDaInscricao);

        resultado.Value!.Formulario.Pressupostos.FusoDoEnvio.Should().Be("America/Belem");
    }
}
