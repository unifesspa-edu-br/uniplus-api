namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.Sementes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;

/// <summary>
/// O SQL que cada semente grava é o que ficou registrado: um comando por linha de cada arquivo de
/// <c>Sementes/Fixtures</c>, na ordem das chaves estrangeiras. Mudar a semente muda o dado semeado em
/// todo ambiente novo, então a mudança do comando tem de ser deliberada.
/// </summary>
public sealed class SqlDasSementesTests
{
    private static string[] Registrado(string arquivo) =>
        File.ReadAllLines(Path.Combine(AppContext.BaseDirectory, "Sementes", "Fixtures", arquivo));

    [Fact(DisplayName = "A semente do PSR Medicina 2027 grava o tipo de processo, os termos com a versão promovida e os fatos com os valores, como registrado")]
    public void TipoTermosEFatos_GravamOQueEstaRegistrado()
    {
        SementePsrMedicina2027.ComandosDoTipoTermosEFatos().Select(static c => c.Texto)
            .Should().Equal(Registrado("psr-medicina-2027-tipo-termos-e-fatos.sql"));
    }

    [Fact(DisplayName = "A semente do PSR Medicina 2027 grava os modelos de inscrição e de habilitação, ativos e com o conteúdo serializado, como registrado")]
    public void Modelos_GravamOQueEstaRegistrado()
    {
        SementePsrMedicina2027.ComandosDosModelos().Select(static c => c.Texto)
            .Should().Equal(Registrado("psr-medicina-2027-modelos.sql"));
    }

    [Fact(DisplayName = "A semente da isenção da taxa grava os termos e o modelo de isenção, ativo e sem tipo de processo, como registrado")]
    public void Isencao_GravaOQueEstaRegistrado()
    {
        SementeIsencaoDaTaxa.Comandos().Select(static c => c.Texto)
            .Should().Equal(Registrado("isencao-da-taxa.sql"));
    }
}
