namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Lê as regras de remanejamento de uma modalidade de concorrência, com o código e o rótulo de cada uma, na ordem em que o enum as declara. Não há estado a consultar: o conjunto
/// muda por versão da API, não por cadastro.
/// </summary>
public sealed record ListarRegrasRemanejamentoQuery : IQuery<IReadOnlyList<CodigoComRotulo<RegraRemanejamento>>>;
