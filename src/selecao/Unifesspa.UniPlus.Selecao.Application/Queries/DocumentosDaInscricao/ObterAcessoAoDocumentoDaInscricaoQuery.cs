namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosDaInscricao;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Pede o acesso de leitura ao arquivo verificado de uma inscrição, por quem opera o processo: a permissão que vale é a da
/// classificação congelada da exigência a que o arquivo responde.
/// </summary>
public sealed record ObterAcessoAoDocumentoDaInscricaoQuery(
    Guid ProcessoSeletivoId, Guid InscricaoId, Guid ArquivoId) : IQuery<Result<AcessoArquivoDoCandidatoDto>>;
