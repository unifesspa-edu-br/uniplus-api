namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Validators;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using FluentValidation.Results;

using Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

/// <summary>
/// O agregado é a autoridade sobre as invariantes; o validator apenas antecipa a
/// recusa, devolvendo 400 antes de o handler tocar o repositório. Estes testes
/// garantem que os dois não divergem.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class TipoAtoPublicadoValidatorsTests
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    private readonly CriarTipoAtoPublicadoCommandValidator _criar = new();
    private readonly AtualizarTipoAtoPublicadoCommandValidator _atualizar = new();
    private readonly RemoverTipoAtoPublicadoCommandValidator _remover = new();

    [Fact(DisplayName = "Criar: comando válido passa")]
    public void Criar_Valido()
    {
        _criar.Validate(Criar()).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Criar: código fora do formato UPPER_SNAKE é recusado")]
    [InlineData("convocacao")]
    [InlineData("Convocacao")]
    [InlineData("_EDITAL")]
    [InlineData("EDITAL_")]
    [InlineData("EDITAL__ABERTURA")]
    [InlineData("EDITAL-ABERTURA")]
    [InlineData("EDITAL 2026")]
    [InlineData("RETIFICAÇÃO")]
    public void Criar_CodigoInvalido(string codigo)
    {
        ValidationResult resultado = _criar.Validate(Criar() with { Codigo = codigo });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoAtoPublicadoCommand.Codigo));
    }

    [Fact(DisplayName = "Criar: janela vazia é recusada — o fim é exclusivo")]
    public void Criar_JanelaVazia()
    {
        _criar.Validate(Criar() with { VigenciaFim = Inicio }).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar: janela invertida é recusada")]
    public void Criar_JanelaInvertida()
    {
        _criar.Validate(Criar() with { VigenciaFim = Inicio.AddDays(-1) }).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar: janela de um único dia é aceita")]
    public void Criar_JanelaDeUmDia()
    {
        _criar.Validate(Criar() with { VigenciaFim = Inicio.AddDays(1) }).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar: vigência aberta é aceita")]
    public void Criar_VigenciaAberta()
    {
        _criar.Validate(Criar() with { VigenciaFim = null }).IsValid.Should().BeTrue();
    }

    [Theory(DisplayName = "Criar: nome fora do intervalo de tamanho é recusado")]
    [InlineData("")]
    [InlineData("E")]
    public void Criar_NomeInvalido(string nome)
    {
        _criar.Validate(Criar() with { Nome = nome }).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar: base legal acima do limite é recusada")]
    public void Criar_BaseLegalLonga()
    {
        _criar.Validate(Criar() with { BaseLegal = new string('x', 501) }).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Criar: código com espaços ao redor é aceito — o domínio normaliza")]
    public void Criar_CodigoComEspacos_Aceito()
    {
        // O agregado faz Trim antes de validar o formato. Se o validator olhasse o
        // texto cru, recusaria com 400 um payload que o domínio aceita.
        _criar.Validate(Criar() with { Codigo = "  EDITAL_ABERTURA  " }).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar: nome de um caractere entre espaços é recusado aqui, não adiante")]
    public void Criar_NomeCurtoEntreEspacos_Recusado()
    {
        // " E" tem dois caracteres crus, mas um só depois do Trim. Sem normalizar, o
        // validator deixaria passar e o agregado recusaria com outro status.
        ValidationResult resultado = _criar.Validate(Criar() with { Nome = " E" });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoAtoPublicadoCommand.Nome));
    }

    [Fact(DisplayName = "Criar: base legal no limite com espaços ao redor é aceita")]
    public void Criar_BaseLegalNoLimiteComEspacos_Aceita()
    {
        string baseLegal = "  " + new string('x', 500) + "  ";

        _criar.Validate(Criar() with { BaseLegal = baseLegal }).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Criar: código só com espaços é recusado como obrigatório")]
    public void Criar_CodigoSoEspacos_Recusado()
    {
        ValidationResult resultado = _criar.Validate(Criar() with { Codigo = "    " });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(CriarTipoAtoPublicadoCommand.Codigo));
    }

    [Fact(DisplayName = "Atualizar: código com espaços ao redor é aceito")]
    public void Atualizar_CodigoComEspacos_Aceito()
    {
        _atualizar.Validate(Atualizar() with { Codigo = " AVISO " }).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Atualizar: identificador vazio é recusado")]
    public void Atualizar_IdVazio()
    {
        ValidationResult resultado = _atualizar.Validate(Atualizar() with { Id = Guid.Empty });

        resultado.IsValid.Should().BeFalse();
        resultado.Errors.Should().Contain(e => e.PropertyName == nameof(AtualizarTipoAtoPublicadoCommand.Id));
    }

    [Fact(DisplayName = "Atualizar: comando válido passa")]
    public void Atualizar_Valido()
    {
        _atualizar.Validate(Atualizar()).IsValid.Should().BeTrue();
    }

    [Fact(DisplayName = "Atualizar: código minúsculo é recusado")]
    public void Atualizar_CodigoMinusculo()
    {
        _atualizar.Validate(Atualizar() with { Codigo = "aviso" }).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Remover: identificador vazio é recusado")]
    public void Remover_IdVazio()
    {
        _remover.Validate(new RemoverTipoAtoPublicadoCommand(Guid.Empty)).IsValid.Should().BeFalse();
    }

    [Fact(DisplayName = "Remover: identificador informado passa")]
    public void Remover_Valido()
    {
        _remover.Validate(new RemoverTipoAtoPublicadoCommand(Guid.NewGuid())).IsValid.Should().BeTrue();
    }

    private static CriarTipoAtoPublicadoCommand Criar() =>
        new("EDITAL_ABERTURA", "Edital de abertura", true, true, false, Inicio);

    private static AtualizarTipoAtoPublicadoCommand Atualizar() =>
        new(Guid.NewGuid(), "EDITAL_ABERTURA", "Edital de abertura", true, true, false, Inicio);
}
