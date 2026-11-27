namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.CorpoDoPedido;

using System.Text.Json;

/// <summary>Os códigos dos tipos de ato que o seed do repositório (<c>seeds/seed-tipos-ato.json</c>) cadastra.</summary>
internal static class CodigosDosTiposDeAtoSemeados
{
    public static IReadOnlyList<string> Todos { get; } = Ler();

    private static List<string> Ler()
    {
        using JsonDocument seed = JsonDocument.Parse(File.ReadAllText(Path.Join(AppContext.BaseDirectory, "seeds", "seed-tipos-ato.json")));
        return [.. seed.RootElement.EnumerateArray().Select(static linha => linha.GetProperty("codigo").GetString()!)];
    }
}
