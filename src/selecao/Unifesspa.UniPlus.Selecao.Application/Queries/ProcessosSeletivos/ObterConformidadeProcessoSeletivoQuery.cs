namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Consulta a conformidade estrutural do Processo Seletivo (CA-07 da Story
/// #758): checklist com cada item obrigatório marcado ok/pendente, sem
/// alterar o processo.
/// </summary>
public sealed record ObterConformidadeProcessoSeletivoQuery(Guid ProcessoSeletivoId) : IQuery<ConformidadeProcessoSeletivoDto?>;
