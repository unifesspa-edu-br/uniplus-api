namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.AcessoAosDadosDoProcesso;

/// <summary>
/// Os valores de <c>sub</c> das personas fictícias dos cenários de acesso aos dados do processo. Cada persona só tem a
/// concessão que o cenário lhe dá, e o papel <c>plataforma-admin</c> do token não vale nada para elas.
/// </summary>
internal static class PersonasDoAcesso
{
    public const string Leandro = "leandro.viana";
    public const string Betina = "betina.neves";
    public const string Diogo = "diogo.souza";
    public const string Caroline = "caroline.lima";

    public static IReadOnlyList<string> Todas { get; } = [Leandro, Betina, Diogo, Caroline];
}
