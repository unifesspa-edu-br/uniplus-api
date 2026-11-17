namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConferenciaDaConfiguracao;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>Um assunto de regras da conferência: o nome dela e a função que devolve todas as causas que ela encontra.</summary>
internal sealed record AssuntoDaConferencia(string Nome, Func<ContextoDaConferencia, IEnumerable<FieldError>> Causas);
