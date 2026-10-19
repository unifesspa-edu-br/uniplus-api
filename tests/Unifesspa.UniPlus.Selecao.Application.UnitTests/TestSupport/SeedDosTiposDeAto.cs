namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Os tipos de ato como o seed do repositório (<c>seeds/seed-tipos-ato.json</c>) os declara: o teste que
/// usa o valor real não passa por construção quando alguém muda o arquivo.
/// </summary>
internal static class SeedDosTiposDeAto
{
    public static TipoAtoPublicadoView Tipo(string codigo)
    {
        string? pasta = AppContext.BaseDirectory;
        while (pasta is not null && !File.Exists(Path.Join(pasta, "seeds", "seed-tipos-ato.json")))
        {
            pasta = Path.GetDirectoryName(pasta);
        }

        pasta.Should().NotBeNull("o seed dos tipos de ato fica na raiz do repositório");

        using JsonDocument seed = JsonDocument.Parse(File.ReadAllText(Path.Join(pasta!, "seeds", "seed-tipos-ato.json")));
        JsonElement linha = seed.RootElement.EnumerateArray().Single(l => l.GetProperty("codigo").GetString() == codigo);

        return new TipoAtoPublicadoView(
            codigo,
            linha.GetProperty("nome").GetString()!,
            linha.GetProperty("congelaConfiguracao").GetBoolean(),
            linha.GetProperty("unicoPorObjeto").GetBoolean(),
            linha.GetProperty("efeitoIrreversivel").GetBoolean(),
            linha.GetProperty("ehResultado").GetBoolean(),
            linha.GetProperty("publicavelComoAvulso").GetBoolean(),
            linha.GetProperty("exigeAtoCorrigido").GetBoolean());
    }
}
