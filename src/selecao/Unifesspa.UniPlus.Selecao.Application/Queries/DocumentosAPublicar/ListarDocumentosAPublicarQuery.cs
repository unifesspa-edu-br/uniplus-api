namespace Unifesspa.UniPlus.Selecao.Application.Queries.DocumentosAPublicar;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// Lê os documentos oficiais vinculados a um Processo Seletivo. É o que
/// permite ao editor administrativo retomar um rascunho depois de um refresh:
/// sem essa leitura, o vínculo com o documento já enviado vive só no estado da
/// página e um segundo envio desnecessário passa a ser o caminho mais provável.
/// </summary>
/// <remarks>
/// A leitura não elege documento algum. Havendo mais de um confirmado, qual
/// deles acompanha a publicação continua sendo decisão explícita de quem
/// publica — escolher aqui, em silêncio, faria a lista parecer inofensiva
/// enquanto decidia por quem a consulta.
/// </remarks>
/// <param name="ProcessoSeletivoId">O processo.</param>
/// <param name="Finalidade">Só os documentos enviados para esta finalidade; nulo lista todos.</param>
public sealed record ListarDocumentosAPublicarQuery(
    Guid ProcessoSeletivoId,
    FinalidadeDoDocumento? Finalidade = null) : IQuery<Result<ListarDocumentosAPublicarResult>>;
