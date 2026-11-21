namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Lê as opções de efeito suspensivo que o edital escolhe para o recurso da 1ª instância, com o código, o rótulo e o que cada uma significa.
/// </summary>
public sealed record ListarEfeitosSuspensivosQuery : IQuery<IReadOnlyList<CodigoComDescricao<EfeitoSuspensivo>>>;
