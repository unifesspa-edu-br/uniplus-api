namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using DTOs;

/// <summary>
/// Consulta um Processo Seletivo (com toda a configuração) pelo
/// <see cref="Id"/>. Contrato <c>ProcessoSeletivoDto?</c> — "não encontrado"
/// é estado normal de leitura, mapeado a 404 pelo controller.
/// </summary>
public sealed record ObterProcessoSeletivoQuery(Guid Id) : IQuery<ProcessoSeletivoDto?>;
