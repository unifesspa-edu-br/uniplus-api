namespace Unifesspa.UniPlus.Autorizacao.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O código textual do tipo de governança: o papel de Representante do controlador tem código próprio e o código antigo
/// da proteção de dados deixa de existir, sem alias (ADR-0162).
/// </summary>
public sealed class TipoDeGovernancaCodigoTests
{
    private const string CodigoAntigoDaProtecaoDeDados = "protecao-de-dados";
    private const int ValorGravadoDoPerfil2 = 2;

    [Theory(DisplayName = "Cada tipo de governança vai ao código e volta ao mesmo tipo")]
    [InlineData(TipoDeGovernanca.GestorDeAcessos, "GESTOR_DE_ACESSOS")]
    [InlineData(TipoDeGovernanca.RepresentanteDoControlador, "REPRESENTANTE_DO_CONTROLADOR")]
    [InlineData(TipoDeGovernanca.GestorDeProtecaoDeDados, "GESTOR_DE_PROTECAO_DE_DADOS")]
    public void Codigo_IdaEVolta(TipoDeGovernanca tipo, string codigo)
    {
        tipo.Codigo().Should().Be(codigo);
        CatalogoDe<TipoDeGovernanca>.Obter(codigo).Should().Be(tipo);
    }

    [Fact(DisplayName = "O código antigo da proteção de dados não é mais reconhecido")]
    public void Codigo_ProtecaoDeDados_NaoExisteMais() =>
        FluentActions.Invoking(static () => CatalogoDe<TipoDeGovernanca>.Obter(CodigoAntigoDaProtecaoDeDados))
            .Should().Throw<ArgumentException>();

    [Fact(DisplayName = "O Representante do controlador mantém o valor do antigo tipo da proteção de dados")]
    public void Representante_MantemOValor() =>
        ((int)TipoDeGovernanca.RepresentanteDoControlador).Should().Be(ValorGravadoDoPerfil2);

    [Fact(DisplayName = "O Gestor de proteção de dados é o terceiro tipo de governança")]
    public void GestorDeProtecaoDeDados_ValorTres() =>
        ((int)TipoDeGovernanca.GestorDeProtecaoDeDados).Should().Be(3);
}
