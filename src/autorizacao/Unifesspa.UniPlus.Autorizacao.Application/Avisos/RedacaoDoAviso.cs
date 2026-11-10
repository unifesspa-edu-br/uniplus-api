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
        TipoDeAviso.JanelaDeMigracaoAberta => "Janela de migração aberta para um perfil de acesso",
        TipoDeAviso.JanelaDeMigracaoFaltam48Horas => "Faltam 48 horas para o fim da migração de um perfil de acesso",
        TipoDeAviso.JanelaDeMigracaoFaltam24Horas => "Faltam 24 horas para o fim da migração de um perfil de acesso",
        TipoDeAviso.ConcessaoRetidaNaMigracao => "Concessão retida na migração de um perfil de acesso",
        TipoDeAviso.PerfilSuspenso => "Perfil de acesso suspenso",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    public static string CorpoEmTexto(ModeloDeAviso modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        string versao = modelo.VersaoNumero is { } numero ? $"\nVersão: {numero}" : string.Empty;
        string permissoes = modelo.Permissoes.Count > 0 ? $"\nPermissões: {string.Join(", ", modelo.Permissoes)}" : string.Empty;

        return $"{Abertura(modelo.Tipo)}\n\nPerfil: {modelo.PerfilNome}{versao}{permissoes}{Janela(modelo)}\n\nAbra em: {modelo.Link}";
    }

    public static string CorpoEmHtml(ModeloDeAviso modelo)
    {
        ArgumentNullException.ThrowIfNull(modelo);

        string versao = modelo.VersaoNumero is { } numero ? $"<br>Versão: <b>{numero}</b>" : string.Empty;
        string permissoes = modelo.Permissoes.Count > 0
            ? $"<br>Permissões: <b>{WebUtility.HtmlEncode(string.Join(", ", modelo.Permissoes))}</b>"
            : string.Empty;

        return $"<p>{Abertura(modelo.Tipo)}</p>"
            + $"<p>Perfil: <b>{WebUtility.HtmlEncode(modelo.PerfilNome)}</b>{versao}{permissoes}{Janela(modelo).Replace("\n", "<br>", StringComparison.Ordinal)}</p>"
            + $"<p><a href=\"{WebUtility.HtmlEncode(modelo.Link)}\">Abrir</a></p>";
    }

    // A quantidade e o prazo da janela de migração, quando o aviso é dela; o prazo vai em horário de Brasília (UTC-3).
    private static string Janela(ModeloDeAviso modelo)
    {
        string quantidade = modelo.Quantidade is { } n ? $"\nConcessões: {n}" : string.Empty;
        string prazo = modelo.Prazo is { } fim
            ? $"\nPrazo: {fim.ToOffset(TimeSpan.FromHours(-3)).ToString("dd/MM/yyyy HH:mm", System.Globalization.CultureInfo.InvariantCulture)} (Brasília)"
            : string.Empty;

        return quantidade + prazo;
    }

    private static string Abertura(TipoDeAviso tipo) =>
        tipo is >= TipoDeAviso.JanelaDeMigracaoAberta and <= TipoDeAviso.ConcessaoRetidaNaMigracao ? AberturaDaMigracao(tipo) : AberturaDaVersao(tipo);

    private static string AberturaDaVersao(TipoDeAviso tipo) => tipo switch
    {
        TipoDeAviso.PermissoesRemovidas => "Permissões foram removidas de um perfil de acesso, e a remoção já vale.",
        TipoDeAviso.VersaoCanceladaPelaRemocao =>
            "A versão que você criou foi cancelada porque o perfil teve permissões removidas. Se ela ainda for necessária, submeta-a de novo.",
        TipoDeAviso.AprovacoesDescartadas =>
            "As aprovações que você deu a uma versão de perfil foram descartadas porque mudou o que a versão exige. Decida de novo.",
        TipoDeAviso.PerfilSuspenso =>
            "Um perfil de acesso foi suspenso: nenhuma concessão dele vale até a retomada.",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    private static string AberturaDaMigracao(TipoDeAviso tipo) => tipo switch
    {
        TipoDeAviso.JanelaDeMigracaoAberta =>
            "Uma versão que acrescenta permissão entrou em vigor. As concessões da versão anterior têm 72 horas para ser migradas; quem não migrar perde o acesso.",
        TipoDeAviso.JanelaDeMigracaoFaltam48Horas =>
            "Faltam 48 horas para o fim da migração. As concessões ainda pendentes serão revogadas no prazo.",
        TipoDeAviso.JanelaDeMigracaoFaltam24Horas =>
            "Faltam 24 horas para o fim da migração. As concessões ainda pendentes serão revogadas no prazo.",
        TipoDeAviso.ConcessaoRetidaNaMigracao =>
            "Há concessões retidas na migração que só outra pessoa pode decidir, e o prazo corre.",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };
}
