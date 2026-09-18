namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

/// <summary>
/// A configuração congelada não é legível pelo codec deste processo.
/// </summary>
/// <remarks>
/// Distingue-se da falha transiente por não melhorar em segundos. A causa esperada é a janela de
/// um deploy em fases: um processo já atualizado congela numa versão de envelope que um processo
/// ainda antigo não conhece, e este só passará a conhecê-la quando for substituído. Insistir
/// resolve — mas na escala do deploy, não na de uma reentrega imediata.
/// </remarks>
public sealed class EnvelopeAindaNaoLegivelException(string message) : Exception(message);
