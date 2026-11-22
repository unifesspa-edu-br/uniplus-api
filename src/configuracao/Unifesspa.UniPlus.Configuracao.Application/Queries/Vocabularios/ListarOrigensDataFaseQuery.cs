namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Lê as origens da data de uma fase canônica, com o código e o rótulo de cada um, na ordem em que o enum os declara. Não há estado a consultar: o conjunto
/// muda por versão da API, não por cadastro.
/// </summary>
public sealed record ListarOrigensDataFaseQuery : IQuery<IReadOnlyList<CodigoComRotulo<OrigemDataFase>>>;
