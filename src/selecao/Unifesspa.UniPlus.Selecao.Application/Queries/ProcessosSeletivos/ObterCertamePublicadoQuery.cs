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
/// <b>A visibilidade já está resolvida</b>: a divulgação é materializada quando o ato normativo se
/// confirma no registro central, e a existência dessa linha É a publicidade do certame (ADR-0133).
/// A consulta não confere ato nenhum, nem pergunta a Publicações no caminho da requisição — ela lê
/// a projeção que já está pronta.
/// </para>
/// <para>
/// <b>As recusas colapsam numa só</b>: processo inexistente, processo em rascunho, processo sem
/// versão vigente e processo cujo ato não está registrado devolvem o mesmo <c>NaoEncontrado</c>,
/// porque nenhum deles tem linha de divulgação.
/// Distinguir responderia "esse identificador é um rascunho?" a um chamador anônimo, que é
/// exatamente o que a restrição de leitura administrativa existe para impedir.
/// </para>
/// </remarks>
public sealed record ObterCertamePublicadoQuery(
    Guid ProcessoSeletivoId) : IQuery<Result<CertamePublicadoDto>>;
