namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;

/// <summary>Todas as recusas dos anexos de um ato, por posição, sem repetir nome nem quantidade recebidos.</summary>
public sealed class RecusasDosAnexosTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OutroHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";

    private static IReadOnlyList<FieldError> Recusas(params (string?, string?, string?)?[] anexos) =>
        AnexoDoAto.Recusas(anexos);

    [Fact(DisplayName = "Anexos aceitáveis não têm recusa")]
    public void Recusas_Aceitaveis_SemRecusa()
    {
        Recusas(("Anexo I.pdf", "application/pdf", HashValido), ("Anexo II.pdf", "Application/PDF", OutroHash))
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "Anexo nulo é recusado na posição, sem recusar nome, tipo de mídia nem hash")]
    public void Recusas_AnexoNulo_RecusaNaPosicao()
    {
        ResumoDasRecusas.De(Recusas(("A.pdf", "application/pdf", HashValido), null)).Should().Equal(
            ("anexos[1]", AnexoDoAtoErrorCodes.Nulo));
    }

    [Fact(DisplayName = "Nome, tipo de mídia e hash inválidos do mesmo anexo são recusados juntos, nessa ordem")]
    public void Recusas_TudoInvalidoNoAnexo_AcumulaNaOrdem()
    {
        ResumoDasRecusas.De(Recusas(("  ", "pdf", "abc"))).Should().Equal(
            ("anexos[0].nome", AnexoDoAtoErrorCodes.NomeInvalido),
            ("anexos[0].tipoMidia", AnexoDoAtoErrorCodes.TipoMidiaInvalido),
            ("anexos[0].hash", AnexoDoAtoErrorCodes.HashInvalido));
    }

    [Fact(DisplayName = "Campos nulos do anexo são recusados como vazios")]
    public void Recusas_CamposNulos_RecusaComoVazios()
    {
        ResumoDasRecusas.De(Recusas((null, null, null))).Should().Equal(
            ("anexos[0].nome", AnexoDoAtoErrorCodes.NomeInvalido),
            ("anexos[0].tipoMidia", AnexoDoAtoErrorCodes.TipoMidiaInvalido),
            ("anexos[0].hash", AnexoDoAtoErrorCodes.HashInvalido));
    }

    [Fact(DisplayName = "Nome acima de 255 caracteres é recusado")]
    public void Recusas_NomeLongo_Recusa()
    {
        ResumoDasRecusas.De(Recusas((new string('n', AnexoDoAto.NomeMaxLength + 1), "application/pdf", HashValido)))
            .Should().Equal(("anexos[0].nome", AnexoDoAtoErrorCodes.NomeInvalido));
    }

    [Fact(DisplayName = "Hash inválido do primeiro anexo e nome repetido do segundo são recusados, cada um na sua posição")]
    public void Recusas_HashInvalidoENomeRepetido_AcumulaPorPosicao()
    {
        ResumoDasRecusas.De(Recusas(
            ("A.pdf", "application/pdf", "abc"),
            ("A.pdf", "application/pdf", OutroHash),
            ("B.pdf", "application/pdf", HashValido))).Should().Equal(
            ("anexos[0].hash", AnexoDoAtoErrorCodes.HashInvalido),
            ("anexos[1].nome", AnexoDoAtoErrorCodes.NomeDuplicado));
    }

    [Fact(DisplayName = "Nome repetido depois de aparado e o nome vazio repetido: só o primeiro é repetição")]
    public void Recusas_NomeRepetidoAparado_RecusaSoOQueRepete()
    {
        ResumoDasRecusas.De(Recusas(
            ("A.pdf", "application/pdf", HashValido),
            (" A.pdf ", "application/pdf", OutroHash),
            ("", "application/pdf", HashValido),
            ("", "application/pdf", HashValido))).Should().Equal(
            ("anexos[1].nome", AnexoDoAtoErrorCodes.NomeDuplicado),
            ("anexos[2].nome", AnexoDoAtoErrorCodes.NomeInvalido),
            ("anexos[3].nome", AnexoDoAtoErrorCodes.NomeInvalido));
    }

    [Fact(DisplayName = "Quantidade excedida é recusada na lista e cada anexo ainda é conferido")]
    public void Recusas_QuantidadeExcedida_RecusaAListaEOsItens()
    {
        (string?, string?, string?)?[] anexos =
        [
            ("Anexo 0.pdf", "application/pdf", "abc"),
            .. Enumerable.Range(1, AnexoDoAto.QuantidadeMaxima).Select(i => ((string?, string?, string?)?)($"Anexo {i}.pdf", "application/pdf", HashValido)),
        ];

        ResumoDasRecusas.De(AnexoDoAto.Recusas(anexos)).Should().Equal(
            ("anexos", AnexoDoAtoErrorCodes.QuantidadeExcedida),
            ("anexos[0].hash", AnexoDoAtoErrorCodes.HashInvalido));
    }

    [Fact(DisplayName = "Cinquenta anexos aceitáveis não excedem a quantidade")]
    public void Recusas_NoLimite_SemRecusa()
    {
        (string?, string?, string?)?[] anexos =
            [.. Enumerable.Range(0, AnexoDoAto.QuantidadeMaxima).Select(i => ((string?, string?, string?)?)($"Anexo {i}.pdf", "application/pdf", HashValido))];

        AnexoDoAto.Recusas(anexos).Should().BeEmpty();
    }

    [Fact(DisplayName = "As mensagens não escrevem o nome do anexo nem a quantidade recebida")]
    public void Recusas_Mensagens_NaoRepetemValorRecebido()
    {
        string nome = "Anexo sigiloso.pdf";
        FieldError[] recusas =
        [
            .. Recusas((nome, "pdf", "abc"), (nome, "application/pdf", HashValido)),
            .. AnexoDoAto.Recusas([.. Enumerable.Repeat<(string?, string?, string?)?>(null, 51)]),
        ];

        recusas.Select(r => r.Error.Message).Should().NotContain(m => m.Contains(nome, StringComparison.Ordinal) || m.Contains("51", StringComparison.Ordinal));
        recusas.Should().Contain(r => r.Error.Code == AnexoDoAtoErrorCodes.NomeDuplicado);
    }
}
