namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>Os textos dos avisos da reclassificação do catálogo e da divergência da governança.</summary>
internal static class TextosDosAvisosDeReclassificacaoEDivergencia
{
    public static IEnumerable<KeyValuePair<TipoDeAviso, TextoDoAviso>> Todos { get; } =
    [
        TextoDoAviso.Para(
            TipoDeAviso.PermissaoReclassificada,
            "Permissão de perfil de acesso reclassificada",
            "A classificação de uma permissão de perfil subiu. A permissão fica suspensa nas concessões do perfil até a liberação."),
        TextoDoAviso.Para(
            TipoDeAviso.EscopoDaPermissaoAlterado,
            "Escopo de permissão de perfil de acesso alterado",
            "O escopo de uma permissão de perfil mudou. A permissão fica suspensa nas concessões do perfil até a liberação."),
        TextoDoAviso.Para(
            TipoDeAviso.DivergenciaDaGovernanca,
            "Divergência em perfil de governança",
            "Um perfil de governança diverge do que o código espera. O perfil não é suspenso; a divergência pede conferência."),
    ];
}
