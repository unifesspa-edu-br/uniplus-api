namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>Os textos dos avisos da contenção do perfil.</summary>
internal static class TextosDosAvisosDaContencao
{
    public static IEnumerable<KeyValuePair<TipoDeAviso, TextoDoAviso>> Todos { get; } =
    [
        TextoDoAviso.Para(
            TipoDeAviso.PerfilSuspenso,
            "Perfil de acesso suspenso",
            "Um perfil de acesso foi suspenso: nenhuma concessão dele vale até a retomada."),
    ];
}
