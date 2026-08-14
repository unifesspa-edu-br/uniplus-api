namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A localidade rege quais feriados incidem na contagem dos prazos (UNI-REQ-0111). O que
/// se cobre aqui é a fronteira entre o que é normativo — o código IBGE — e o que é cache
/// de exibição, porque é dela que depende a promessa de que um nome divergente produz
/// rótulo errado e nunca prazo errado.
/// </summary>
public sealed class LocalidadeRegenteTests
{
    [Fact(DisplayName = "Trio coerente é aceito e os valores ficam recuperáveis")]
    public void Criar_TrioCoerente_Aceita()
    {
        Result<LocalidadeRegente> resultado = LocalidadeRegente.Criar("1504208", "Marabá", "PA");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CodigoIbge.Should().Be("1504208");
        resultado.Value.Nome.Should().Be("Marabá");
        resultado.Value.Uf.Should().Be("PA");
    }

    [Theory(DisplayName = "Código fora de sete dígitos é recusado")]
    [InlineData("150420")]
    [InlineData("15042088")]
    [InlineData("15042O8")]
    public void Criar_CodigoForaDeSeteDigitos_Recusa(string codigo)
    {
        Result<LocalidadeRegente> resultado = LocalidadeRegente.Criar(codigo, "Marabá", "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.CodigoIbgeFormatoInvalido);
    }

    [Fact(DisplayName = "Prefixo do código incompatível com a UF informada é recusado")]
    public void Criar_PrefixoIncompativelComUf_Recusa()
    {
        // 15 é o prefixo do Pará; declarar São Paulo com ele é o erro que a coerência pega.
        Result<LocalidadeRegente> resultado = LocalidadeRegente.Criar("1504208", "Marabá", "SP");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.UfIncoerente);
    }

    [Theory(DisplayName = "Nome vazio ou só espaços é recusado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_NomeVazio_Recusa(string nome)
    {
        Result<LocalidadeRegente> resultado = LocalidadeRegente.Criar("1504208", nome, "PA");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(CidadeReferenciaErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Espaço em volta é aparado e a UF é normalizada em maiúsculas")]
    public void Criar_Normaliza()
    {
        Result<LocalidadeRegente> resultado = LocalidadeRegente.Criar("  1504208 ", "  Marabá  ", " pa ");

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.CodigoIbge.Should().Be("1504208");
        resultado.Value.Nome.Should().Be("Marabá");
        resultado.Value.Uf.Should().Be("PA");
    }

    /// <summary>
    /// A contraprova que sustenta o desenho: o código é o único valor normativo, então
    /// duas declarações do mesmo município regem-se pelo mesmo calendário ainda que uma
    /// delas exiba nome divergente. Se esta igualdade quebrar, o cache de exibição passou
    /// a influenciar a identidade da localidade — e o prazo deixou de depender só do código.
    /// </summary>
    [Fact(DisplayName = "Mesmo código com nomes divergentes produz a mesma localidade normativa")]
    public void Igualdade_MesmoCodigoNomesDivergentes_SaoIguais()
    {
        LocalidadeRegente correta = LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!;
        LocalidadeRegente comNomeErrado = LocalidadeRegente.Criar("1504208", "Maraba do Norte", "PA").Value!;

        correta.Should().Be(comNomeErrado);
        correta.GetHashCode().Should().Be(comNomeErrado.GetHashCode());
        correta.Nome.Should().NotBe(comNomeErrado.Nome, "o cache de exibição continua distinto, e é só ele que difere");
    }

    [Fact(DisplayName = "Códigos diferentes produzem localidades diferentes")]
    public void Igualdade_CodigosDiferentes_SaoDiferentes()
    {
        LocalidadeRegente maraba = LocalidadeRegente.Criar("1504208", "Marabá", "PA").Value!;
        LocalidadeRegente belem = LocalidadeRegente.Criar("1501402", "Belém", "PA").Value!;

        maraba.Should().NotBe(belem);
    }
}
