namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Pede o acesso de leitura a um documento a publicar já confirmado, para
/// quem administra conferir o arquivo que ficou anexado ao processo.
/// <para>
/// A consulta não altera nada: nada é persistido e o objeto no storage não é
/// tocado. O que ela produz é uma assinatura de curta duração, calculada no
/// instante do pedido — de modo que a autorização vale para aquele instante,
/// e não para quando a listagem foi consultada.
/// </para>
/// </summary>
public sealed record ObterAcessoDocumentoAPublicarQuery(
    Guid ProcessoSeletivoId,
    Guid DocumentoAPublicarId) : IQuery<Result<AcessoDocumentoAPublicarDto>>;
