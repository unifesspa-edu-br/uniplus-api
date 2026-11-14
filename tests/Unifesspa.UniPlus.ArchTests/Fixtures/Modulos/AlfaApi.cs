namespace Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Alfa.API;

using Unifesspa.UniPlus.ArchTests.Fixtures.Modulos.Beta.Infrastructure;

/// <summary>Camada de entrada do módulo sintético Alfa que alcança a infraestrutura de Beta, o que a fronteira proíbe.</summary>
internal sealed class UsaAInfraestruturaDeBeta
{
    public static string Descrever() => new InfraestruturaDeBeta().Descricao;
}
