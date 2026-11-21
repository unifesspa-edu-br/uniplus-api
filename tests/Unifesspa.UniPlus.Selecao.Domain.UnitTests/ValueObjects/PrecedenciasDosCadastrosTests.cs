namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ValueObjects;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A precedência lida do cadastro, ou o registro de que a operação não a leu: a lista vazia lida não é a ausência de leitura.</summary>
public sealed class PrecedenciasDosCadastrosTests
{
    [Fact(DisplayName = "A precedência não lida não pede a conferência e não traz relação")]
    public void NaoLidas_NaoPedeAConferencia()
    {
        PrecedenciasDosCadastros.NaoLidas.FoiLida.Should().BeFalse();
        PrecedenciasDosCadastros.NaoLidas.Arestas.Should().BeEmpty();
    }

    [Fact(DisplayName = "A lista vazia lida é leitura: o cadastro sem relação alguma é julgado")]
    public void ListaVaziaLida_FoiLida()
    {
        PrecedenciasDosCadastros lidas = PrecedenciasDosCadastros.Lidas([]);

        lidas.FoiLida.Should().BeTrue();
        lidas.Arestas.Should().BeEmpty();
    }

    [Fact(DisplayName = "As relações lidas ficam como foram lidas, numa cópia que a lista de origem não altera")]
    public void Lidas_GuardamUmaCopia()
    {
        List<ArestaPrecedencia> origem = [new("A", "B", true)];

        PrecedenciasDosCadastros lidas = PrecedenciasDosCadastros.Lidas(origem);
        origem.Clear();

        lidas.Arestas.Should().ContainSingle().Which.Should().Be(new ArestaPrecedencia("A", "B", true));
    }

    [Fact(DisplayName = "Sem a lista de relações, a leitura não se registra")]
    public void SemLista_Lanca()
    {
        Func<PrecedenciasDosCadastros> registrar = () => PrecedenciasDosCadastros.Lidas(null!);

        registrar.Should().Throw<ArgumentNullException>();
    }
}
