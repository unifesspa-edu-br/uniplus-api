namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

using System.Net;

/// <summary>
/// O texto de cada aviso: assunto e abertura fixos por tipo, e os campos do modelo no corpo, em texto e em HTML. Os
/// valores do cadastro vão escapados no HTML. O tipo sem texto é erro de programação.
/// </summary>
internal static class RedacaoDoAviso
{
    public static string Assunto(TipoDeAviso tipo) => tipo switch
    {
        TipoDeAviso.PermissoesRemovidas => "Permissões removidas de um perfil de acesso",
        TipoDeAviso.VersaoCanceladaPelaRemocao => "Versão de perfil de acesso cancelada pela remoção de permissões",
        TipoDeAviso.AprovacoesDescartadas => "Aprovações de uma versão de perfil de acesso descartadas",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static string CorpoEmTexto(ModeloDeAviso modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        string versao = modelo.VersaoNumero is { } numero ? $"\nVersão: {numero}" : string.Empty;
        string permissoes = modelo.Permissoes.Count > 0 ? $"\nPermissões: {string.Join(", ", modelo.Permissoes)}" : string.Empty;

        return $"{Abertura(modelo.Tipo)}\n\nPerfil: {modelo.PerfilNome}{versao}{permissoes}\n\nAbra em: {modelo.Link}";
    }

    public static string CorpoEmHtml(ModeloDeAviso modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        string versao = modelo.VersaoNumero is { } numero ? $"<br>Versão: <b>{numero}</b>" : string.Empty;
        string permissoes = modelo.Permissoes.Count > 0
            ? $"<br>Permissões: <b>{WebUtility.HtmlEncode(string.Join(", ", modelo.Permissoes))}</b>"
            : string.Empty;

        return $"<p>{Abertura(modelo.Tipo)}</p>"
            + $"<p>Perfil: <b>{WebUtility.HtmlEncode(modelo.PerfilNome)}</b>{versao}{permissoes}</p>"
            + $"<p><a href=\"{WebUtility.HtmlEncode(modelo.Link)}\">Abrir</a></p>";
    }

    private static string Abertura(TipoDeAviso tipo) => tipo switch
    {
        TipoDeAviso.PermissoesRemovidas => "Permissões foram removidas de um perfil de acesso, e a remoção já vale.",
        TipoDeAviso.VersaoCanceladaPelaRemocao =>
            "A versão que você criou foi cancelada porque o perfil teve permissões removidas. Se ela ainda for necessária, submeta-a de novo.",
        TipoDeAviso.AprovacoesDescartadas =>
            "As aprovações que você deu a uma versão de perfil foram descartadas porque mudou o que a versão exige. Decida de novo.",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };
}
