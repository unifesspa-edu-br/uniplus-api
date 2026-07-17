namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Cobertura de <see cref="CondicaoGatilho"/> (Story #554, issue #892, PR-b): fábrica —
/// validação de forma delegada a <see cref="CondicaoDnf"/> — e vinculação ao
/// <see cref="DocumentoExigido"/> pai.
/// </summary>
public sealed class CondicaoGatilhoTests
{
    private static JsonElement Json(string valorJson) => JsonDocument.Parse(valorJson).RootElement.Clone();

    [Fact(DisplayName = "Criar aceita cláusula/fato/operador/valor coerentes")]
    public void Criar_DadosCoerentes_Aceita()
    {
        Result<CondicaoGatilho> resultado = CondicaoGatilho.Criar(0, "SEXO", Operador.Igual, Json("\"MASCULINO\""));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Clausula.Should().Be(0);
        resultado.Value.Fato.Should().Be("SEXO");
        resultado.Value.Operador.Should().Be(Operador.Igual);
    }

    [Fact(DisplayName = "Criar recusa cláusula negativa")]
    public void Criar_ClausulaNegativa_Recusa()
    {
        Result<CondicaoGatilho> resultado = CondicaoGatilho.Criar(-1, "SEXO", Operador.Igual, Json("\"MASCULINO\""));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoGatilho.ClausulaInvalida");
    }

    [Fact(DisplayName = "Criar propaga a falha de forma de CondicaoDnf (fato vazio)")]
    public void Criar_FatoVazio_PropagaErroDeCondicaoDnf()
    {
        Result<CondicaoGatilho> resultado = CondicaoGatilho.Criar(0, "  ", Operador.Igual, Json("\"MASCULINO\""));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoDnf.FatoObrigatorio");
    }

    [Fact(DisplayName = "Criar propaga a falha de forma de CondicaoDnf (EM com array vazio)")]
    public void Criar_OperadorEmComArrayVazio_PropagaErroDeCondicaoDnf()
    {
        Result<CondicaoGatilho> resultado = CondicaoGatilho.Criar(0, "MODALIDADE", Operador.Em, Json("[]"));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("CondicaoDnf.FormaIncoerenteComOperador");
    }

    [Fact(DisplayName = "DocumentoExigidoId nasce vazio antes da vinculação ao pai")]
    public void Criar_DocumentoExigidoId_NasceVazio()
    {
        CondicaoGatilho condicao = CondicaoGatilho.Criar(0, "SEXO", Operador.Igual, Json("\"MASCULINO\"")).Value!;

        condicao.DocumentoExigidoId.Should().Be(Guid.Empty, "a vinculação ao pai é responsabilidade de DocumentoExigido.Criar, coberta em DocumentoExigidoTests");
    }
}
