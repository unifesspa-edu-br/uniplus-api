namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class TitularDaInscricaoTests
{
    private static TitularDaInscricao Titular => TitularDaInscricao.Identificar(PersonasDeTeste.FernandaDrumond, "conta", "emissor").Value!;

    [Fact]
    public void Identificar_MesmosValores_PossuiIgualdadePorValor()
    {
        TitularDaInscricao outro = TitularDaInscricao.Identificar(PersonasDeTeste.FernandaDrumond, "conta", "emissor").Value!;
        Titular.Should().Be(outro);
        Titular.GetHashCode().Should().Be(outro.GetHashCode());
        typeof(TitularDaInscricao).GetConstructors().Should().BeEmpty();
        typeof(TitularDaInscricao).GetProperties().Should().OnlyContain(propriedade => !propriedade.CanWrite);
    }

    [Fact]
    public void Identificar_CpfNulo_LancaExcecao()
    {
        Action identificar = () => TitularDaInscricao.Identificar(null!, "conta", "emissor");
        identificar.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Reidratar_CpfNulo_LancaExcecao()
    {
        Action reidratar = () => TitularDaInscricao.Reidratar(null!, "conta", "emissor");
        reidratar.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Identificar_SemConta_RecusaAntesDoEmissor()
    {
        Result<TitularDaInscricao> resultado = TitularDaInscricao.Identificar(PersonasDeTeste.FernandaDrumond, "", "");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.ContaIdObrigatorio);
    }

    [Fact]
    public void Identificar_SemEmissor_Recusa()
    {
        Result<TitularDaInscricao> resultado = TitularDaInscricao.Identificar(PersonasDeTeste.FernandaDrumond, "conta", "");
        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.EmissorObrigatorio);
    }

    [Fact]
    public void ConferirDono_ContaDiferente_Recusa()
    {
        Titular.ConferirDono("outra")!.Code.Should().Be(InscricaoErrorCodes.DonoDivergente);
        Titular.ConferirDono("conta").Should().BeNull();
    }

    [Fact]
    public void RecusasDoCpf_ValorNulo_RecusaRemocao()
    {
        Dictionary<string, JsonElement> respostas = new Dictionary<string, JsonElement> { ["CPF"] = JsonSerializer.SerializeToElement<string?>(null) };
        Titular.RecusasDoCpf(respostas).Should().ContainSingle().Which.Error.Code.Should().Be(InscricaoErrorCodes.CpfNaoPodeSerRemovido);
    }

    [Fact]
    public void RecusasDoCpf_ValorInvalido_RecusaDivergencia()
    {
        Dictionary<string, JsonElement> respostas = new Dictionary<string, JsonElement> { ["CPF"] = JsonSerializer.SerializeToElement("invalido") };
        Titular.RecusasDoCpf(respostas).Should().ContainSingle().Which.Error.Code.Should().Be(InscricaoErrorCodes.CpfDiferenteDoDono);
    }
}
