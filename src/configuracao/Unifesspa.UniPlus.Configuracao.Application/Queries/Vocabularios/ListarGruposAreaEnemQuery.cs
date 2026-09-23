namespace Unifesspa.UniPlus.Configuracao.Application.Queries.Vocabularios;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Lê os quatro grupos de área do ENEM (Anexo I da Resolução nº 805/2024/Consepe), com
/// código e rótulo, na ordem de exibição. Não há estado a consultar: o conjunto é
/// governado por código e muda por versão da API, não por cadastro.
/// </summary>
public sealed record ListarGruposAreaEnemQuery : IQuery<IReadOnlyList<GrupoAreaEnemDto>>;
