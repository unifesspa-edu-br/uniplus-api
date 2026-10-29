namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

/// <summary>Violações de gravação da instalação que os handlers traduzem em erro de domínio.</summary>
internal static class InstalacaoViolacoes
{
    private const string PendenciaUnicaConstraint = "ux_instalacoes_executor_pendente";

    /// <summary>O executor já tem outra instalação pendente: duas iniciações correram juntas.</summary>
    public static bool EhPendenciaDuplicada(Exception ex) =>
        ViolacoesDeGravacao.ViolaIndiceUnico(ex, PendenciaUnicaConstraint);
}
