namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;

/// <summary>
/// Todos os eventos de manifestação técnica de um item do catálogo, de todos os ciclos, com a situação de cada um
/// (UNI-REQ-0373); nulo quando o item não existe. A permissão de aprovar ou de consultar a proteção é verificada na borda.
/// </summary>
public sealed record ListarManifestacoesDoItemDaProtecaoQuery(CadastroDaProtecaoDoCatalogo Cadastro, Guid Id)
    : IQuery<IReadOnlyList<ManifestacaoDaProtecaoDto>?>;
