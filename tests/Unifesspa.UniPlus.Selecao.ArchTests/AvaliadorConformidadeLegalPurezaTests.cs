namespace Unifesspa.UniPlus.Selecao.ArchTests;

using System.Reflection;
using System.Runtime.CompilerServices;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Services;

/// <summary>
/// Fitness test da Story #853 (CA-01): <see cref="AvaliadorConformidadeLegal"/>
/// avalia <c>ProcessoSeletivo</c> × <c>ObrigatoriedadeLegal</c> como função pura
/// — sem repositório, DbContext ou relógio próprios. Toda entrada (inclusive a
/// data de referência, via <c>ConferenciaDeConformidadeLegal</c> na Application)
/// chega já resolvida pelo chamador.
/// </summary>
public sealed class AvaliadorConformidadeLegalPurezaTests
{
    private static readonly string[] TokensProibidos =
    [
        "TimeProvider",
        "DateTimeOffset.UtcNow",
        "DateTimeOffset.Now",
        "DateTime.UtcNow",
        "DateTime.Now",
        "DbContext",
        "Repository",
        "IQueryable",
        "IServiceProvider",
    ];

    [Fact(DisplayName = "AvaliadorConformidadeLegal é static, sem campos e sem construtor — não guarda estado nem dependências")]
    public void ClasseEstaticaSemEstado()
    {
        Type tipo = typeof(AvaliadorConformidadeLegal);

        tipo.IsAbstract.Should().BeTrue("classes static do C# são compiladas como abstract sealed");
        tipo.IsSealed.Should().BeTrue();
        tipo.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Should().BeEmpty("uma função pura não guarda estado entre chamadas");
        tipo.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Should().BeEmpty();
    }

    [Fact(DisplayName = "AvaliadorConformidadeLegal não referencia repositório, DbContext, relógio ou DI no código-fonte")]
    public void FonteNaoReferenciaIoOuRelogio()
    {
        string fonte = File.ReadAllText(CaminhoAvaliador());

        foreach (string token in TokensProibidos)
        {
            fonte.Should().NotContain(token,
                "o avaliador recebe processo/regras já resolvidos pelo chamador — " + token + " indicaria I/O ou leitura de relógio embutida");
        }
    }

    private static string CaminhoAvaliador([CallerFilePath] string origem = "") =>
        Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!,
            "..",
            "..",
            "src",
            "selecao",
            "Unifesspa.UniPlus.Selecao.Domain",
            "Services",
            "AvaliadorConformidadeLegal.cs"));
}
