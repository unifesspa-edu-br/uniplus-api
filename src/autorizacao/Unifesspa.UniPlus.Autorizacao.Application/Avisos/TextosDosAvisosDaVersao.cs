namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>Os textos dos avisos da governança das versões do perfil.</summary>
internal static class TextosDosAvisosDaVersao
{
    public static IEnumerable<KeyValuePair<TipoDeAviso, TextoDoAviso>> Todos { get; } =
    [
        TextoDoAviso.Para(
            TipoDeAviso.PermissoesRemovidas,
            "Permissões removidas de um perfil de acesso",
            "Permissões foram removidas de um perfil de acesso, e a remoção já vale."),
        TextoDoAviso.Para(
            TipoDeAviso.VersaoCanceladaPelaRemocao,
            "Versão de perfil de acesso cancelada pela remoção de permissões",
            "A versão que você criou foi cancelada porque o perfil teve permissões removidas. Se ela ainda for necessária, submeta-a de novo."),
        TextoDoAviso.Para(
            TipoDeAviso.AprovacoesDescartadas,
            "Aprovações de uma versão de perfil de acesso descartadas",
            "As aprovações que você deu a uma versão de perfil foram descartadas porque mudou o que a versão exige. Decida de novo."),
        TextoDoAviso.Para(
            TipoDeAviso.VersaoPendenteApta,
            "Versão de perfil de acesso aguarda a sua aprovação",
            "Uma versão de perfil de acesso está pronta para a etapa indicada e aguarda a sua decisão. Ela expira no prazo abaixo se ninguém decidir."),
    ];
}
