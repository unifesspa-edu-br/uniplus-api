namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Resolve o certame publicado para leitura anônima, projetado da versão de configuração vigente
/// do processo.
/// </summary>
/// <remarks>
/// Duas coisas distinguem esta consulta das leituras internas do módulo.
/// <para>
/// <b>A visibilidade é conjunta</b>: não basta existir versão vigente, o ato normativo que a criou
/// precisa estar registrado em Publicações. A versão carrega o identificador desse ato congelado,
/// por valor, mas carregar a referência não é o mesmo que o ato existir — o registro acontece
/// depois da publicação, por mensagem durável, e uma recusa de mérito só aflora no consumo da
/// fila. Divulgar sem essa conferência publicaria certame sem ato normativo correspondente
/// (ADR-0131).
/// </para>
/// <para>
/// <b>As recusas colapsam numa só</b>: processo inexistente, processo em rascunho, processo sem
/// versão vigente e processo cujo ato não está registrado devolvem o mesmo <c>NaoEncontrado</c>.
/// Distinguir responderia "esse identificador é um rascunho?" a um chamador anônimo, que é
/// exatamente o que a restrição de leitura administrativa existe para impedir.
/// </para>
/// </remarks>
public sealed record ObterCertamePublicadoQuery(
    Guid ProcessoSeletivoId) : IQuery<Result<CertamePublicadoDto>>;
