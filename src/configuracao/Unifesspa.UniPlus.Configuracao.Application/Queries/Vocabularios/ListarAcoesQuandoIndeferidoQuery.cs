namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;

/// <summary>
/// Lê as ações aplicadas ao candidato quando a comprovação da reserva é indeferida, com o código e o rótulo de cada uma, na ordem em que o enum as declara. Não há estado a consultar: o conjunto
/// muda por versão da API, não por cadastro.
/// </summary>
public sealed record ListarAcoesQuandoIndeferidoQuery : IQuery<IReadOnlyList<CodigoComRotulo<AcaoQuandoIndeferido>>>;
