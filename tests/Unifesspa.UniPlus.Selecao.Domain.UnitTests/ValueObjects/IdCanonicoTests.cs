namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Xunit;

public sealed class IdCanonicoTests
{
    private static readonly Guid ProcessoA = new("11111111-1111-4111-8111-111111111111");
    private static readonly Guid ProcessoB = new("22222222-2222-4222-8222-222222222222");

    [Fact(DisplayName = "IdCanonico — compõe tipoDeNo/PROCESSO/<id-N>/codigo")]
    public void Compoe_AFormaCanonica()
    {
        IdCanonico.De(ClasseNoGrafo.Fato, ProcessoA, "COR_RACA").Valor
            .Should().Be("FATO/PROCESSO/11111111111141118111111111111111/COR_RACA");
    }

    [Fact(DisplayName = "IdCanonico — campo e fato do mesmo código são identidades distintas")]
    public void CampoEFato_MesmoCodigo_SaoDistintos()
    {
        IdCanonico campo = IdCanonico.De(ClasseNoGrafo.Campo, ProcessoA, "QUILOMBOLA");
        IdCanonico fato = IdCanonico.De(ClasseNoGrafo.Fato, ProcessoA, "QUILOMBOLA");

        campo.Should().NotBe(fato);
        campo.Valor.Should().StartWith("CAMPO/");
        fato.Valor.Should().StartWith("FATO/");
    }

    [Fact(DisplayName = "IdCanonico — o mesmo código em processos distintos são identidades distintas")]
    public void MesmoCodigo_ProcessosDistintos_SaoDistintos()
    {
        IdCanonico.De(ClasseNoGrafo.Fato, ProcessoA, "PCD")
            .Should().NotBe(IdCanonico.De(ClasseNoGrafo.Fato, ProcessoB, "PCD"));
    }

    [Fact(DisplayName = "IdCanonico — duas composições iguais são iguais e têm o mesmo hash")]
    public void MesmaComposicao_Igual()
    {
        IdCanonico a = IdCanonico.De(ClasseNoGrafo.Exigencia, ProcessoA, "0f9a");
        IdCanonico b = IdCanonico.De(ClasseNoGrafo.Exigencia, ProcessoA, "0f9a");

        a.Should().Be(b);
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [Fact(DisplayName = "IdCanonico — a ordenação é por bytes UTF-8 (aqui, ASCII, coincide com ordinal)")]
    public void Ordena_PorBytes()
    {
        IdCanonico a = IdCanonico.De(ClasseNoGrafo.Fato, ProcessoA, "AAA");
        IdCanonico b = IdCanonico.De(ClasseNoGrafo.Fato, ProcessoA, "AAB");

        a.CompareTo(b).Should().BeNegative();
        b.CompareTo(a).Should().BePositive();
        a.CompareTo(a).Should().Be(0);

        Encoding.UTF8.GetBytes(a.Valor).Should().Equal(a.Bytes.ToArray());
    }

    [Theory(DisplayName = "IdCanonico — o delimitador '/' no código é recusado (remontagem ambígua)")]
    [InlineData("COR/RACA")]
    [InlineData("/PCD")]
    [InlineData("PCD/")]
    public void Delimitador_NoCodigo_Recusado(string codigo)
    {
        Action criar = () => IdCanonico.De(ClasseNoGrafo.Fato, ProcessoA, codigo);

        criar.Should().Throw<ArgumentException>();
    }

    [Theory(DisplayName = "IdCanonico — código não-ASCII ou de controle é recusado")]
    [InlineData("CÓDIGO")]
    [InlineData("COR RACA")]
    public void NaoAsciiOuControle_Recusado(string codigo)
    {
        Action criar = () => IdCanonico.De(ClasseNoGrafo.Fato, ProcessoA, codigo);

        criar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "IdCanonico — código vazio é recusado")]
    public void Vazio_Recusado()
    {
        Action criar = () => IdCanonico.De(ClasseNoGrafo.Fato, ProcessoA, "  ");

        criar.Should().Throw<ArgumentException>();
    }
}
