namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;

/// <summary>Violações de gravação do perfil que os handlers traduzem em erro de domínio.</summary>
internal static class PerfilViolacoes
{
    private const string NomeConstraint = "ux_perfis_nome";

    /// <summary>Violação do índice único do nome.</summary>
    public static bool EhConflitoDeNome(Exception ex) => ViolacoesDeGravacao.ViolaIndiceUnico(ex, NomeConstraint);

    /// <summary>Outra requisição alterou o perfil entre a leitura e a gravação.</summary>
    public static bool EhConflitoDeConcorrencia(Exception ex) => ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex);
}
