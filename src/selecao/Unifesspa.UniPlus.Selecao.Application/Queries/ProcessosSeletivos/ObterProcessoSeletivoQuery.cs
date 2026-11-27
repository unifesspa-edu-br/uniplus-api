namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;

/// <summary>
/// Consulta um Processo Seletivo (com toda a configuração) pelo
/// <see cref="Id"/>. Contrato <c>ProcessoSeletivoDto?</c> — "não encontrado"
/// é estado normal de leitura; quem expõe o processo na API é <see cref="LerProcessoSeletivoQuery"/>.
/// </summary>
public sealed record ObterProcessoSeletivoQuery(Guid Id) : IQuery<ProcessoSeletivoDto?>;
