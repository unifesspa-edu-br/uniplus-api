namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O código textual do tipo de governança: o papel de Representante do controlador tem código próprio e o código antigo
/// da proteção de dados deixa de existir, sem alias (ADR-0162).
/// </summary>
public sealed class TipoDeGovernancaCodigoTests
{
    private const string CodigoAntigoDaProtecaoDeDados = "protecao-de-dados";
    private const int ValorGravadoDoPerfil2 = 2;

    [Theory(DisplayName = "Cada tipo de governança vai ao código e volta ao mesmo tipo")]
    [InlineData(TipoDeGovernanca.GestorDeAcessos, TipoDeGovernancaCodigo.GestorDeAcessos)]
    [InlineData(TipoDeGovernanca.RepresentanteDoControlador, TipoDeGovernancaCodigo.RepresentanteDoControlador)]
    [InlineData(TipoDeGovernanca.EncarregadoDeDados, "encarregado-de-dados")]
    public void Codigo_IdaEVolta(TipoDeGovernanca tipo, string codigo)
    {
        tipo.ToCodigo().Should().Be(codigo);
        TipoDeGovernancaCodigo.FromCodigo(codigo).Should().Be(tipo);
    }

    [Fact(DisplayName = "O código antigo da proteção de dados não é mais reconhecido")]
    public void Codigo_ProtecaoDeDados_NaoExisteMais() =>
        FluentActions.Invoking(static () => TipoDeGovernancaCodigo.FromCodigo(CodigoAntigoDaProtecaoDeDados))
            .Should().Throw<ArgumentOutOfRangeException>();

    [Fact(DisplayName = "O Representante do controlador mantém o valor do antigo tipo da proteção de dados")]
    public void Representante_MantemOValor() =>
        ((int)TipoDeGovernanca.RepresentanteDoControlador).Should().Be(ValorGravadoDoPerfil2);

    [Fact(DisplayName = "O Encarregado de dados é o terceiro tipo de governança")]
    public void Encarregado_ValorTres() =>
        ((int)TipoDeGovernanca.EncarregadoDeDados).Should().Be(3);
}
