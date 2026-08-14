namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Runtime.CompilerServices;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;

/// <summary>
/// Ancora o detector de referência congelada no envelope canônico REAL, gerado
/// pelo canonicalizador e congelado como fixture dourada.
/// </summary>
/// <remarks>
/// Sem esta âncora a prova seria circular: a amostra sintética dos canários e o
/// predicado que a procura nascem no mesmo apoio e foram escritos juntos, de
/// modo que um erro compartilhado — trocar <c>versao</c> por <c>version</c>,
/// por exemplo — deixaria os dois de acordo entre si e em desacordo com o que
/// o sistema realmente grava, sem nenhum teste ficar vermelho. Procurar no
/// envelope dourado prova que o detector reconhece a forma que o
/// canonicalizador produz de fato.
/// </remarks>
public sealed class DetectorDeReferenciaAncoradoNoEnvelopeTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public DetectorDeReferenciaAncoradoNoEnvelopeTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Regras que o envelope dourado congela, com a versão em que as congela.
    /// Se o canonicalizador mudar a forma da referência, a fixture muda e estes
    /// casos passam a não ser encontrados.
    /// </summary>
    public static TheoryData<string, string> ReferenciasDoEnvelopeDourado() => new()
    {
        { "CLASSIFICACAO-IMPORTADA", "v1" },
        { "ALOCACAO-OPCOES-RN04", "v1" },
        { "DISTRIB-VAGAS-INSTITUCIONAL", "v1" },
    };

    [Theory(DisplayName = "O detector encontra, no envelope canônico real, a referência que o canonicalizador gravou")]
    [MemberData(nameof(ReferenciasDoEnvelopeDourado))]
    public async Task Detector_EncontraReferenciaNoEnvelopeReal(string codigo, string versao)
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        long encontradas = await FronteiraAppendOnlyDoRol.ContarAsync(
            context,
            FronteiraAppendOnlyDoRol.DetectaEmAmostra,
            FronteiraAppendOnlyDoRol.PredicadoDeReferencia(codigo, versao),
            amostra: EnvelopeDourado());

        encontradas.Should().Be(
            1,
            $"o detector precisa reconhecer {codigo} na forma que o canonicalizador grava, não só na amostra sintética");
    }

    [Fact(DisplayName = "Uma regra ausente do envelope real não é encontrada nele")]
    public async Task Detector_NaoEncontraRegraAusente()
    {
        await using SelecaoDbContext context = _fixture.CreateDbContext();

        long encontradas = await FronteiraAppendOnlyDoRol.ContarAsync(
            context,
            FronteiraAppendOnlyDoRol.DetectaEmAmostra,
            FronteiraAppendOnlyDoRol.PredicadoDeReferencia("RECURSO-MULTI-INSTANCIA", "v1"),
            amostra: EnvelopeDourado());

        encontradas.Should().Be(
            0, "sem esta contraprova, o teste acima passaria mesmo com um predicado que casasse com tudo");
    }

    private static string EnvelopeDourado([CallerFilePath] string origem = "") =>
        File.ReadAllText(Path.GetFullPath(Path.Join(
            Path.GetDirectoryName(origem)!,
            "..",
            "ProcessosSeletivos",
            "Fixtures",
            "envelope-0.0.11.json")));
}
