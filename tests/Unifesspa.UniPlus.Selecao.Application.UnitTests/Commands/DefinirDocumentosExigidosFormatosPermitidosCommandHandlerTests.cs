namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands.ApoioDosDocumentosExigidos;

/// <summary>
/// Os formatos permitidos do documento exigido: a forma do valor, o token QUALQUER, a lista de formatos, o teto por formato e a substituição integral no reenvio.
/// </summary>
public sealed class DefinirDocumentosExigidosFormatosPermitidosCommandHandlerTests
{
    [Fact(DisplayName = "Story #918: reenviar o PUT com FormatosPermitidos/TamanhoMaximoBytes diferentes substitui integralmente (não faz merge)")]
    public async Task Handle_ReenviarComFormatoETamanhoDiferentes_SubstituiIntegralmente()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        (await cenario.DefinirAsync(cenario.ItemGeral(ListaDeFormatos("PDF"), 5_000_000))).IsSuccess.Should().BeTrue();
        DocumentoExigido primeira = cenario.Processo.DocumentosExigidos.Single();
        primeira.FormatosPermitidos.Qualquer.Should().BeFalse();
        primeira.FormatosPermitidos.Lista!.Single().Formato.Should().Be(FormatoPermitido.Pdf);
        primeira.TamanhoMaximoBytes.Should().Be(5_000_000);

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral(ListaDeFormatos("JPEG"), 2_000_000));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        DocumentoExigido exigencia = cenario.Processo.DocumentosExigidos.Should().ContainSingle().Which;
        exigencia.FormatosPermitidos.Lista!.Single().Formato.Should().Be(
            FormatoPermitido.Jpeg, "substituição integral, sem vestígio do valor anterior");
        exigencia.TamanhoMaximoBytes.Should().Be(2_000_000);
    }

    [Fact(DisplayName = "FormatosPermitidos ausente (JsonElement? null) retorna FormatosPermitidos.Obrigatorio")]
    public async Task Handle_FormatosPermitidosAusente_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral() with { FormatosPermitidos = null });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.Obrigatorio");
    }

    [Theory(DisplayName = "FormatosPermitidos em forma inválida (ValueKind inesperado ou string diferente de QUALQUER) retorna FormatosPermitidos.FormaInvalida")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("\"PDF\"")]
    [InlineData("{}")]
    public async Task Handle_FormatosPermitidosFormaInvalida_RetornaErroNomeado(string valorJson)
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral(JsonDocument.Parse(valorJson).RootElement));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormaInvalida");
    }

    [Fact(DisplayName = "FormatosPermitidos = QUALQUER é aceito")]
    public async Task Handle_FormatosPermitidosQualquer_Aceita()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral(Qualquer));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        cenario.Processo.DocumentosExigidos.Single().FormatosPermitidos.Qualquer.Should().BeTrue();
    }

    [Fact(DisplayName = "FormatosPermitidos com lista {PDF,JPEG,PNG} (itens-texto simples) congela os 3 formatos, sem teto por formato")]
    public async Task Handle_FormatosPermitidosListaDeStrings_Aceita()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral(ListaDeFormatos("PDF", "JPEG", "PNG")));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FormatosPermitidos formatosPermitidos = cenario.Processo.DocumentosExigidos.Single().FormatosPermitidos;
        formatosPermitidos.Qualquer.Should().BeFalse();
        formatosPermitidos.Lista!.Select(e => e.Formato).Should().BeEquivalentTo(
            [FormatoPermitido.Pdf, FormatoPermitido.Jpeg, FormatoPermitido.Png]);
        formatosPermitidos.Lista!.Should().AllSatisfy(e => e.TamanhoMaximoBytesMax.Should().BeNull());
    }

    [Fact(DisplayName = "FormatosPermitidos com item {formato, tamanhoMaximoBytesMax} congela o teto por formato")]
    public async Task Handle_FormatosPermitidosComTetoPorFormato_Aceita()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        JsonElement lista = JsonSerializer.SerializeToElement(new[] { new { formato = "PDF", tamanhoMaximoBytesMax = 5_000_000 } });

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral(lista));

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        FormatoPermitidoEntry entrada = cenario.Processo.DocumentosExigidos.Single().FormatosPermitidos.Lista!.Single();
        entrada.Formato.Should().Be(FormatoPermitido.Pdf);
        entrada.TamanhoMaximoBytesMax.Should().Be(5_000_000);
    }

    [Theory(DisplayName = "tamanhoMaximoBytesMax presente e malformado é recusado com erro nomeado, nunca 500")]
    [InlineData("\"5MB\"")]
    [InlineData("true")]
    [InlineData("5000000.5")]
    [InlineData("99999999999999999999")]
    public async Task Handle_FormatosPermitidosTamanhoMaximoBytesMaxMalformado_RetornaErroNomeado(string tamanhoJson)
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();
        JsonElement lista = JsonDocument.Parse($$"""[{"formato": "PDF", "tamanhoMaximoBytesMax": {{tamanhoJson}}}]""").RootElement;

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral(lista));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormaInvalida");
    }

    /// <summary>
    /// "QUALQUER" só é reconhecido como o token especial quando a forma é texto: um array contendo
    /// a string "QUALQUER" é só mais um item de lista (um formato desconhecido). A exclusividade
    /// mútua (FormatosPermitidos.QualquerComFormatosEspecificos) é coberta no VO (FormatosPermitidosTests).
    /// </summary>
    [Fact(DisplayName = "Um array contendo a string \"QUALQUER\" NÃO é o token especial — é lista com um formato desconhecido")]
    public async Task Handle_FormatosPermitidosQualquerComLista_RetornaErroNomeado()
    {
        CenarioDaExigencia cenario = ProcessoComUmaFase();

        Result resultado = await cenario.DefinirAsync(cenario.ItemGeral(ListaDeFormatos("QUALQUER")));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("FormatosPermitidos.FormatoInvalido");
    }

    private static JsonElement ListaDeFormatos(params string[] formatos) => JsonSerializer.SerializeToElement(formatos);
}
