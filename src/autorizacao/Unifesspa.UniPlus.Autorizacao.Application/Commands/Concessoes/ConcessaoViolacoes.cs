namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;

using Unifesspa.UniPlus.Autorizacao.Application.Commands;

/// <summary>Violações de gravação da concessão que os handlers traduzem em erro de domínio.</summary>
public static class ConcessaoViolacoes
{
    private const string AtivaUnicaConstraint = "ux_concessoes_ativa_unica";
    private const string OrigemConstraint = "ux_concessoes_origem";

    /// <summary>
    /// Violação de um dos índices únicos da concessão: já existe uma concessão igual não
    /// revogada, vigente ou vencida, ou a origem já foi processada. É o que quem chama
    /// <c>Concessao.Conceder</c> traduz em <c>409</c>; a concessão vencida igual precisa ser
    /// revogada por ele antes de conceder de novo.
    /// </summary>
    public static bool EhConcessaoDuplicada(Exception ex) =>
        ViolacoesDeGravacao.ViolaIndiceUnico(ex, AtivaUnicaConstraint, OrigemConstraint);

    /// <summary>Outra requisição alterou a concessão entre a leitura e a gravação.</summary>
    public static bool EhConflitoDeConcorrencia(Exception ex) => ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex);
}
