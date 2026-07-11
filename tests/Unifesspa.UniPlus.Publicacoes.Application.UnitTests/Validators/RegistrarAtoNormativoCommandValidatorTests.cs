namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Validators;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

/// <summary>
/// O agregado é a autoridade sobre as invariantes de shape; o validator antecipa a
/// recusa em 400 antes de o handler tocar o repositório. Estes testes garantem que
/// os dois não divergem.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RegistrarAtoNormativoCommandValidatorTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    private readonly RegistrarAtoNormativoCommandValidator _validator = new();

    [Fact(DisplayName = "Comando válido passa")]
    public void Valido()
    {
        _validator.Validate(Comando()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Ato sem número é válido (número é opcional)")]
    public void SemNumero_Valido()
    {
        _validator.Validate(Comando() with { Numero = null }).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Órgão em branco é recusado")]
    [InlineData("")]
    [InlineData("   ")]
    public void OrgaoEmBranco(string orgao)
    {
        _validator.Validate(Comando() with { Orgao = orgao }).IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "Ano não-positivo é recusado")]
    [InlineData(0)]
    [InlineData(-5)]
    public void AnoNaoPositivo(int ano)
    {
        _validator.Validate(Comando() with { Ano = ano }).IsValid.Should().BeFalse();
    }

    [Theory(DisplayName = "Hash do documento fora do formato SHA-256 é recusado")]
    [InlineData("abc")]
    [InlineData("0123456789ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void DocumentoHashInvalido(string hash)
    {
        _validator.Validate(Comando() with { DocumentoHash = hash }).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Par de versão incompleto (só id) é recusado")]
    public void VersaoInvocadaSoId()
    {
        ValidationResult resultado = _validator.Validate(
            Comando() with { VersaoInvocadaId = Guid.CreateVersion7(), VersaoInvocadaHash = null });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Par de versão incompleto (só hash) é recusado")]
    public void VersaoInvocadaSoHash()
    {
        _validator.Validate(
            Comando() with { VersaoInvocadaId = null, VersaoInvocadaHash = HashValido })
            .IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Par de versão completo e válido passa")]
    public void VersaoInvocadaCompleta()
    {
        _validator.Validate(
            Comando() with { VersaoInvocadaId = Guid.CreateVersion7(), VersaoInvocadaHash = HashValido })
            .IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Par de versão com id vazio (Guid.Empty) é recusado")]
    public void VersaoInvocadaIdVazio()
    {
        _validator.Validate(
            Comando() with { VersaoInvocadaId = Guid.Empty, VersaoInvocadaHash = HashValido })
            .IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Retificação só com ato retificado (sem motivo) é recusada")]
    public void RetificacaoSoAto()
    {
        _validator.Validate(
            Comando() with { AtoRetificadoId = Guid.CreateVersion7(), MotivoRetificacao = null })
            .IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Retificação só com motivo (sem ato retificado) é recusada")]
    public void RetificacaoSoMotivo()
    {
        _validator.Validate(
            Comando() with { AtoRetificadoId = null, MotivoRetificacao = "corrige o anexo II" })
            .IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Retificação com o par (ato retificado, motivo) completo passa")]
    public void RetificacaoParCompleto()
    {
        _validator.Validate(
            Comando() with { AtoRetificadoId = Guid.CreateVersion7(), MotivoRetificacao = "corrige o anexo II" })
            .IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Retificação com ato retificado vazio (Guid.Empty) é recusada")]
    public void RetificacaoAtoVazio()
    {
        _validator.Validate(
            Comando() with { AtoRetificadoId = Guid.Empty, MotivoRetificacao = "corrige o anexo II" })
            .IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Motivo de retificação acima do limite (1000) é recusado")]
    public void RetificacaoMotivoLongo()
    {
        _validator.Validate(
            Comando() with { AtoRetificadoId = Guid.CreateVersion7(), MotivoRetificacao = new string('x', 1001) })
            .IsValid.Should().BeFalse();
    }

    private static RegistrarAtoNormativoCommand Comando() =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: "EDITAL_ABERTURA",
            DataPublicacao: new DateOnly(2026, 3, 13),
            DocumentoHash: HashValido,
            Assinante: "Jairo Belchior");
}
