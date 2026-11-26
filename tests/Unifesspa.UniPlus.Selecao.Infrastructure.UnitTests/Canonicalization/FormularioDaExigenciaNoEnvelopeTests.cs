namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>A exigência de documento congela o código do formulário em que é coletada, e todo documento exigido tem um.</summary>
public sealed class FormularioDaExigenciaNoEnvelopeTests
{
    [Fact(DisplayName = "A exigência lida guarda o código do formulário congelado")]
    public void Ler_CodigoDoFormulario_ChegaAExigencia()
    {
        JsonObject exigencia = ChavesDasExigenciasNoEnvelopeTests.Exigencia();
        exigencia["formularioCodigo"] = "HABILITACAO";
        LeitorEnvelope leitor = new();

        IReadOnlyList<DocumentoExigido> lidas = ChavesDasExigenciasNoEnvelopeTests.Ler(leitor, exigencia);

        leitor.Falhou.Should().BeFalse();
        lidas.Should().ContainSingle().Which.FormularioCodigo!.Valor.Should().Be("HABILITACAO");
    }

    [Fact(DisplayName = "A exigência sem a chave do formulário é recusada")]
    public void Ler_ChaveDoFormularioAusente_Recusa()
    {
        JsonObject exigencia = ChavesDasExigenciasNoEnvelopeTests.Exigencia();
        exigencia.Remove("formularioCodigo");
        LeitorEnvelope leitor = new();

        ChavesDasExigenciasNoEnvelopeTests.Ler(leitor, exigencia);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("formularioCodigo");
    }

    [Fact(DisplayName = "A exigência sem formulário (valor nulo) é recusada")]
    public void Ler_FormularioNulo_Recusa()
    {
        JsonObject exigencia = ChavesDasExigenciasNoEnvelopeTests.Exigencia();
        exigencia["formularioCodigo"] = null;
        LeitorEnvelope leitor = new();

        ChavesDasExigenciasNoEnvelopeTests.Ler(leitor, exigencia);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("formularioCodigo");
    }

    [Theory(DisplayName = "A exigência com código de formulário fora da forma do código é recusada")]
    [InlineData("inscricao")]
    [InlineData("INSCRICAO__DADOS")]
    [InlineData("1INSCRICAO")]
    public void Ler_FormularioForaDoFormato_Recusa(string codigo)
    {
        JsonObject exigencia = ChavesDasExigenciasNoEnvelopeTests.Exigencia();
        exigencia["formularioCodigo"] = codigo;
        LeitorEnvelope leitor = new();

        ChavesDasExigenciasNoEnvelopeTests.Ler(leitor, exigencia);

        leitor.Erro.Message.Should().Be("'documentosExigidos.exigencias[0].formularioCodigo' fora do formato do código de formulário.");
    }

    [Fact(DisplayName = "A exigência com código de formulário acima de 60 caracteres é recusada")]
    public void Ler_FormularioAcimaDoTamanho_Recusa()
    {
        JsonObject exigencia = ChavesDasExigenciasNoEnvelopeTests.Exigencia();
        exigencia["formularioCodigo"] = new string('A', 61);
        LeitorEnvelope leitor = new();

        ChavesDasExigenciasNoEnvelopeTests.Ler(leitor, exigencia);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Message.Should().Contain("formularioCodigo");
    }
}
