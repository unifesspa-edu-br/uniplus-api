namespace Unifesspa.UniPlus.Selecao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Lê as opções de guarda da vaga enquanto o recurso corre, com o código, o rótulo e o que cada uma significa.
/// </summary>
public sealed record ListarGuardasDaVagaQuery : IQuery<IReadOnlyList<CodigoComDescricao<GuardaDaVaga>>>;
