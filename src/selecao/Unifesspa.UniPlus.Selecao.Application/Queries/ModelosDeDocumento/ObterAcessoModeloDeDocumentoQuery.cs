namespace Unifesspa.UniPlus.Selecao.Application.Queries.ModelosDeDocumento;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;

/// <summary>
/// Pede o acesso de leitura a um modelo confirmado, para o administrador conferir o arquivo. Não
/// altera nada: produz uma assinatura de curta duração, calculada no instante do pedido.
/// </summary>
public sealed record ObterAcessoModeloDeDocumentoQuery(
    Guid ProcessoSeletivoId, Guid ModeloDeDocumentoId) : IQuery<Result<AcessoModeloDeDocumentoDto>>;
