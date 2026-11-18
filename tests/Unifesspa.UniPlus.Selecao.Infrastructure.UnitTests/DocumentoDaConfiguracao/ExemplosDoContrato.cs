namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.DocumentoDaConfiguracao;

/// <summary>Os exemplos publicados do documento de configuração, copiados para a saída do teste.</summary>
internal static class ExemplosDoContrato
{
    private static readonly string Pasta = Path.Join(AppContext.BaseDirectory, "Contratos", "selecao-configuracao", "exemplos");

    public static IEnumerable<object[]> Arquivos => Directory.GetFiles(Pasta, "*.json").Select(static caminho => new object[] { Path.GetFileName(caminho) });

    public static string Ler(string arquivo) => File.ReadAllText(Path.Join(Pasta, arquivo));
}
