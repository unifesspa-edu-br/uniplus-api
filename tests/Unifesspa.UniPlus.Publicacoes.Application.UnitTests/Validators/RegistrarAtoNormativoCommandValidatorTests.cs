namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Validators;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Kernel.Results;
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

    [Theory(DisplayName = "Código do tipo de ato com espaços, minúsculas ou número é recusado no campo tipoCodigo")]
    [InlineData(" EDITAL_ABERTURA ")]
    [InlineData("edital_abertura")]
    [InlineData("5")]
    public void TipoCodigoForaDoFormato_Recusado(string tipoCodigo)
    {
        ValidationResult resultado = _validator.Validate(Comando() with { TipoCodigo = tipoCodigo });

        ValidationFailure falha = resultado.Errors.Should().ContainSingle().Subject;
        falha.PropertyName.Should().Be("TipoCodigo");
        falha.ErrorCode.Should().Be(CodigosDeValidacao.Formato);
        falha.ErrorMessage.Should().NotContain(tipoCodigo);
    }

    [Fact(DisplayName = "Código do tipo de ato em branco é recusado só como obrigatório")]
    public void TipoCodigoEmBranco_RecusadoSoComoObrigatorio()
    {
        ValidationResult resultado = _validator.Validate(Comando() with { TipoCodigo = "   " });

        resultado.Errors.Should().ContainSingle().Which.ErrorMessage.Should().Be(AtoNormativoRegras.TipoCodigoObrigatorio);
    }

    [Fact(DisplayName = "Código do tipo de ato fora do formato acumula com as demais causas")]
    public void TipoCodigoForaDoFormato_AcumulaComAsDemaisCausas()
    {
        ValidationResult resultado = _validator.Validate(Comando() with { TipoCodigo = "edital_abertura", Ano = 0 });

        resultado.Errors.Select(falha => falha.PropertyName).Should().BeEquivalentTo("TipoCodigo", "Ano");
    }

    [Theory(DisplayName = "Tipo da entidade vinculada com espaços, minúsculas, hífen ou número é recusado no campo entidadeTipo")]
    [InlineData(" OUTRO_TIPO ")]
    [InlineData("outro_tipo")]
    [InlineData("processo-seletivo")]
    [InlineData("3")]
    public void EntidadeTipoForaDoFormato_Recusado(string entidadeTipo)
    {
        ValidationResult resultado = _validator.Validate(
            Comando() with { Vinculos = [new VinculoEntidadeInput(entidadeTipo, Guid.CreateVersion7())] });

        ValidationFailure falha = resultado.Errors.Should().ContainSingle().Subject;
        falha.PropertyName.Should().EndWith("EntidadeTipo");
        falha.ErrorCode.Should().Be(CodigosDeValidacao.Formato);
        falha.ErrorMessage.Should().NotContain(entidadeTipo);
    }

    [Fact(DisplayName = "Códigos exatos do tipo de ato e da entidade vinculada passam")]
    public void CodigosExatos_Passam() =>
        _validator.Validate(
            Comando() with { TipoCodigo = "EDITAL_ABERTURA", Vinculos = [new VinculoEntidadeInput("OUTRO_TIPO", Guid.CreateVersion7())] })
            .IsValid.Should().BeTrue();

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
    public void VersaoDoObjetoSoId()
    {
        ValidationResult resultado = _validator.Validate(
            Comando() with { VersaoDoObjetoId = Guid.CreateVersion7(), VersaoDoObjetoHash = null });

        resultado.IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Par de versão incompleto (só hash) é recusado")]
    public void VersaoDoObjetoSoHash()
    {
        _validator.Validate(
            Comando() with { VersaoDoObjetoId = null, VersaoDoObjetoHash = HashValido })
            .IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Par de versão completo e válido passa")]
    public void VersaoDoObjetoCompleta()
    {
        _validator.Validate(
            Comando() with { VersaoDoObjetoId = Guid.CreateVersion7(), VersaoDoObjetoHash = HashValido })
            .IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Par de versão com id vazio (Guid.Empty) é recusado")]
    public void VersaoDoObjetoIdVazio()
    {
        _validator.Validate(
            Comando() with { VersaoDoObjetoId = Guid.Empty, VersaoDoObjetoHash = HashValido })
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
            DataDoAto: new DateOnly(2026, 3, 13),
            DocumentoHash: HashValido,
            Assinante: "Jairo Belchior");
}
