namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Pede o acesso de leitura ao arquivo verificado do próprio candidato.
/// </summary>
public sealed record ObterAcessoDoArquivoDoCandidatoQuery(
    Guid ProcessoSeletivoId, Guid ArquivoId) : IQuery<Result<AcessoArquivoDoCandidatoDto>>;
