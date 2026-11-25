namespace Unifesspa.UniPlus.Publicacoes.IntegrationTests.TiposAtoPublicado;

using System.Globalization;
using System.Text;
using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Infrastructure.HealthChecks;

using static Unifesspa.UniPlus.Publicacoes.Infrastructure.HealthChecks.CatalogoDeTiposAtoDeclarado;

public sealed class CatalogoDeTiposAtoDeclaradoTests
{
    private const string Catalogo = """
        [
          { "codigo": "AVISO", "nome": "Aviso", "congelaConfiguracao": true,
            "unicoPorObjeto": false, "efeitoIrreversivel": true, "ehResultado": false,
            "publicavelComoAvulso": true, "exigeAtoCorrigido": false,
            "vigenciaInicio": "2026-01-01", "vigenciaFim": null, "baseLegal": null },
          { "codigo": "ERRATA", "nome": "Errata", "congelaConfiguracao": false,
            "unicoPorObjeto": true, "efeitoIrreversivel": false, "ehResultado": true,
            "publicavelComoAvulso": false, "exigeAtoCorrigido": true,
            "vigenciaInicio": "2026-02-01", "vigenciaFim": "2027-01-01", "baseLegal": "Lei de exemplo" }
        ]
        """;

    [Theory]
    [InlineData("\"codigo\"", "\"Codigo\"")]
    [InlineData("\"vigenciaInicio\"", "\"VigenciaInicio\"")]
    public void Ler_PropriedadeComOutraCaixa_DevolveVazio(string original, string alterada)
    {
        LerTexto(Catalogo.Replace(original, alterada, StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public void Ler_PropriedadeDesconhecida_DevolveVazio()
    {
        LerTexto(Catalogo.Replace("\"codigo\":", "\"extra\": 1, \"codigo\":", StringComparison.Ordinal))
            .Should().BeEmpty();
    }

    [Theory]
    [InlineData("\"codigo\": \"AVISO\", ")]
    [InlineData("\"vigenciaFim\": null, ")]
    [InlineData(", \"baseLegal\": null")]
    public void Ler_PropriedadeAusente_DevolveVazio(string propriedade)
    {
        LerTexto(Catalogo.Replace(propriedade, "", StringComparison.Ordinal)).Should().BeEmpty();
    }

    [Fact]
    public void Ler_CatalogoNoFormatoDeclarado_PreservaTodasAsLinhasEPropriedades()
    {
        LinhaDeclarada[] esperadas =
        [
            new() { Codigo = "AVISO", Nome = "Aviso", CongelaConfiguracao = true, UnicoPorObjeto = false,
                EfeitoIrreversivel = true, EhResultado = false, PublicavelComoAvulso = true,
                ExigeAtoCorrigido = false, VigenciaInicio = new(2026, 1, 1), VigenciaFim = null, BaseLegal = null },
            new() { Codigo = "ERRATA", Nome = "Errata", CongelaConfiguracao = false, UnicoPorObjeto = true,
                EfeitoIrreversivel = false, EhResultado = true, PublicavelComoAvulso = false,
                ExigeAtoCorrigido = true, VigenciaInicio = new(2026, 2, 1), VigenciaFim = new(2027, 1, 1),
                BaseLegal = "Lei de exemplo" }
        ];

        LerTexto(Catalogo).Should().Equal(esperadas);
    }

    [Fact]
    public void Ler_ArquivoEmbutido_LeTodasAsLinhasComOsMesmosValores()
    {
        using MemoryStream copia = CopiaDoArquivoEmbutido();
        using JsonDocument arquivo = JsonDocument.Parse(copia);
        copia.Position = 0;
        LinhaDeclarada[] lidas = Ler(copia);
        JsonElement[] itens = [.. arquivo.RootElement.EnumerateArray()];

        IsAvailable.Should().BeTrue();
        lidas.Select(static linha => linha.Codigo).Should().Equal(itens.Select(static item => item.GetProperty("codigo").GetString()));
        lidas.Select(static linha => linha.VigenciaInicio.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .Should().Equal(itens.Select(static item => item.GetProperty("vigenciaInicio").GetString()));
    }

    private static MemoryStream CopiaDoArquivoEmbutido()
    {
        using Stream embutido = typeof(CatalogoDeTiposAtoDeclarado).Assembly.GetManifestResourceStream(
            "Unifesspa.UniPlus.Publicacoes.Infrastructure.seed-tipos-ato.json")!;
        MemoryStream copia = new();
        embutido.CopyTo(copia);
        copia.Position = 0;
        return copia;
    }

    private static LinhaDeclarada[] LerTexto(string json)
    {
        using MemoryStream stream = new(Encoding.UTF8.GetBytes(json));
        return Ler(stream);
    }
}
