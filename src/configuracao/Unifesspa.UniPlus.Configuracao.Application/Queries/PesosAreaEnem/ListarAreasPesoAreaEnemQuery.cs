namespace Unifesspa.UniPlus.Configuracao.Application.Queries.PesosAreaEnem;

using System.Collections.Generic;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Lê as cinco áreas do cadastro de Pesos por Área, com código e rótulo, na ordem
/// canônica. Não há estado a consultar: as áreas são definidas pelo domínio de Pesos por
/// Área, e o formulário de cadastro monta as colunas a partir delas mesmo com o cadastro
/// vazio.
/// </summary>
public sealed record ListarAreasPesoAreaEnemQuery : IQuery<IReadOnlyList<AreaPesoAreaEnemDto>>;
