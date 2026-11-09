namespace Unifesspa.UniPlus.Autorizacao.Application.Avisos;

/// <summary>Os avisos da governança de perfis que a aplicação emite.</summary>
public enum TipoDeAviso
{
    /// <summary>Permissões foram removidas de um perfil e a remoção já vale.</summary>
    PermissoesRemovidas = 1,

    /// <summary>A versão pendente foi cancelada pela contenção de uma remoção.</summary>
    VersaoCanceladaPelaRemocao = 2,

    /// <summary>As aprovações dadas a uma versão foram descartadas pela mudança de classe.</summary>
    AprovacoesDescartadas = 3,
}

/// <summary>O texto estável de cada tipo: a finalidade do envio e o que o registro de avisos guarda.</summary>
public static class TipoDeAvisoCodigo
{
    public static string ToCodigo(this TipoDeAviso tipo) => tipo switch
    {
        TipoDeAviso.PermissoesRemovidas => "permissoes_removidas",
        TipoDeAviso.VersaoCanceladaPelaRemocao => "versao_cancelada_pela_remocao",
        TipoDeAviso.AprovacoesDescartadas => "aprovacoes_descartadas",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, null),
    };

    /// <summary>A finalidade que <c>EnviarEmail</c> leva aos logs: <c>perfil.{tipo}</c>.</summary>
    public static string Finalidade(this TipoDeAviso tipo) => $"perfil.{tipo.ToCodigo()}";
}
