namespace Unifesspa.UniPlus.Configuracao.Application.Queries.ProtecaoDoCatalogo;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Configuracao.Application.DTOs;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Todos os eventos de manifestação técnica de um tipo de documento ou fato do candidato, de todos os ciclos, com a
/// situação de cada um (UNI-REQ-0373); recusa com o erro de tipo de documento ou de dado do candidato não encontrado quando o item não existe. A permissão de aprovar ou de consultar a proteção é verificada na borda.
/// </summary>
public sealed record ListarManifestacoesDoItemDaProtecaoQuery(CadastroDaProtecaoDoCatalogo Cadastro, Guid Id)
    : IQuery<Result<IReadOnlyList<ManifestacaoDaProtecaoDto>>>;
