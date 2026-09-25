namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Infrastructure.Core.Pagination;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Leitura pública do certame publicado — o contrato que o portal do candidato e qualquer outro
/// consumidor leem para montar a página de um processo seletivo.
/// </summary>
/// <remarks>
/// Sem <c>[Authorize]</c> de classe e sem escrita: este controlador é só leitura anônima. A
/// configuração do processo continua sendo escrita pelas rotas administrativas, e a leitura
/// administrativa do processo em rascunho continua restrita — esta rota não a afrouxa, serve outro
/// recurso, projetado da versão congelada.
/// </remarks>
[ApiController]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class CertamePublicadoController : ControllerBase
{
    private const string RecursoDaVitrine = "certames";

    /// <summary>
    /// Descrição do parâmetro de ordenação, com os campos aceitos escritos por extenso.
    /// </summary>
    /// <remarks>
    /// Atributo exige constante, então a lista não pode ser montada a partir do catálogo. Um teste
    /// confere que as duas coincidem: acrescentar campo ao catálogo sem citá-lo aqui quebra a
    /// suíte, em vez de deixar o contrato anunciar menos do que a rota aceita.
    /// </remarks>
    internal const string DescricaoDoSort =
        "Campos de ordenação separados por vírgula, na ordem de prioridade; '-' prefixa o campo "
        + "decrescente. Exemplo: sort=inscricoesAte,-nome. Campos aceitos: inscricoesAte, "
        + "inscricoesDe, nome, divulgadoEm. Sem o parâmetro, vale a ordem por urgência: quem ainda "
        + "não encerrou primeiro, do prazo mais próximo ao mais distante, e os encerrados depois.";

    private const string ContagemEmBreve =
        "Quantos certames divulgados ainda não abriram a janela de inscrição, no instante da "
        + "consulta. Presente só quando incluir_contadores=true.";

    private const string ContagemAbertas =
        "Quantos certames divulgados ainda recebem inscrição sem estar no limiar final, no instante "
        + "da consulta. Presente só quando incluir_contadores=true.";

    private const string ContagemUltimosDias =
        "Quantos certames divulgados encerram dentro do limiar final, no instante da consulta. "
        + "Presente só quando incluir_contadores=true.";

    private const string ContagemEncerrados =
        "Quantos certames divulgados já encerraram, no instante da consulta. Presente só quando "
        + "incluir_contadores=true.";

    private const string DescricaoDoSeloDoCertame =
        "Selo da representação servida, no formato \"{versaoDaProjecao}:{hashDaConfiguracao}\". "
        + "Devolva-o no If-None-Match da próxima leitura: a resposta é de revalidação obrigatória "
        + "(Cache-Control: no-cache), e o selo é o que permite receber 304 em vez do documento "
        + "inteiro. Esta rota é somente leitura e não aceita If-Match.";

    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    private readonly TimeProvider _relogio;

    public CertamePublicadoController(IQueryBus queryBus, IDomainErrorMapper mapper, TimeProvider relogio)
    {
        _queryBus = queryBus;
        _mapper = mapper;
        _relogio = relogio;
    }

    /// <summary>
    /// Vitrine pública: os certames publicados, ordenados por urgência — quem ainda não encerrou
    /// primeiro, do prazo mais próximo ao mais distante, e os encerrados depois.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Só existe linha para certame publicamente divulgável, então a página sai do banco já com o
    /// tamanho pedido: não há descarte de visibilidade depois de formada, e não há pergunta a outro
    /// módulo no caminho da requisição. A continuação segue pela âncora do header <c>Link</c>, e o
    /// cursor só vale para o mesmo recorte por situação que o emitiu.
    /// </para>
    /// <para>
    /// Sem <c>situacao</c> vem a vitrine inteira. Com ela, vêm exatamente os itens que trazem
    /// aquela situação — e o contador homônimo diz quantos são: as quatro situações particionam o
    /// conjunto divulgado, e os quatro contadores somam o total.
    /// </para>
    /// </remarks>
    [HttpGet("certames")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "certame", Versions = [1])]
    [ProducesResponseType(typeof(IEnumerable<CertameNaVitrineDto>), StatusCodes.Status200OK)]
    // Presentes só sob `incluir_contadores`. Declarados porque cliente gerado só enxerga header
    // declarado.
    [EmiteHeader("X-Certames-Em-Breve", ContagemEmBreve, Inteiro = true)]
    [EmiteHeader("X-Certames-Inscricoes-Abertas", ContagemAbertas, Inteiro = true)]
    [EmiteHeader("X-Certames-Ultimos-Dias", ContagemUltimosDias, Inteiro = true)]
    [EmiteHeader("X-Certames-Encerrados", ContagemEncerrados, Inteiro = true)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ListarVitrine(
        // RequireSortKey: a âncora é o par (SortKey, Id). Sem a exigência, cursor sem a chave
        // degrada em silêncio para primeira página em vez de ser recusado.
        [FromCursor(RecursoDaVitrine, RequireSortKey = true)] PageRequest page,
        [FromQuery(Name = "situacao")] SituacaoDoCertame? situacao,
        [FromQuery(Name = "modalidade")]
        [Description("Código da modalidade que o certame precisa ofertar. Sem o parâmetro, não recorta.")]
        string? modalidade,
        [FromQuery(Name = "q")]
        [Description("Texto pesquisado no título do certame e no número do edital. Insensível a caixa e a acentuação.")]
        string? q,
        [FromQuery(Name = "sort")][Description(DescricaoDoSort)] string? sort,
        [FromQuery(Name = "incluir_contadores")] bool incluirContadores,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(page);

        if (!SortExpressionParser.TentarLer(sort, out IReadOnlyList<SortField> ordenacao, out SortExpressionError erro))
        {
            return erro.ParaResposta(_mapper);
        }

        Result<ListarCertamesPublicadosResult> saida = await _queryBus
            .Send(
                new ListarCertamesPublicadosQuery(
                    _relogio.GetUtcNow(), new RecorteDaVitrine(situacao, modalidade, q), ordenacao,
                    page.AfterSortKey, page.AfterId, page.Limit, page.Direction, incluirContadores),
                cancellationToken)
            .ConfigureAwait(false);

        if (saida.IsFailure)
        {
            return saida.ToActionResult(_mapper);
        }

        ListarCertamesPublicadosResult resultado = saida.Value!;

        // Revalidação obrigatória: o endereço da página não muda quando um certame entra na
        // vitrine ou se reposiciona nela.
        Response.Headers.CacheControl = "no-cache";

        // Metadado de coleção em header, nunca no corpo (ADR-0025). Opt-in: o link de continuação
        // preserva o parâmetro, então quem pede uma vez paga a contagem em toda página — pedir só
        // na primeira requisição é o uso pretendido.
        if (resultado.Contadores is { } contadores)
        {
            Response.Headers["X-Certames-Em-Breve"] = Numero(contadores.EmBreve);
            Response.Headers["X-Certames-Inscricoes-Abertas"] = Numero(contadores.InscricoesAbertas);
            Response.Headers["X-Certames-Ultimos-Dias"] = Numero(contadores.UltimosDias);
            Response.Headers["X-Certames-Encerrados"] = Numero(contadores.Encerrados);
        }

        return await this.OkPaginatedOrdenadoAsync(
            resultado.Items, resultado.Anterior, resultado.Proximo, page, RecursoDaVitrine,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static string Numero(int valor) => valor.ToString(CultureInfo.InvariantCulture);


    /// <summary>
    /// Certame publicado, projetado da versão de configuração vigente e visível apenas quando o ato
    /// normativo que a criou está registrado — localizado pelo Guid do processo ou pelo
    /// identificador legível congelado na publicação.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uma rota só para as duas chaves: o identificador legível nunca tem a forma de um Guid (o
    /// cadastro a recusa), então o próprio valor diz qual é. Mesmo conteúdo, mesmo selo e mesmas
    /// recusas pelas duas.
    /// </para>
    /// <para>
    /// <c>404</c> cobre, com a mesma resposta, o processo inexistente, o processo em rascunho, o
    /// processo sem versão vigente, aquele cujo ato ainda não foi registrado — inclusive quando o
    /// registro foi recusado — e o valor que não é Guid nem identificador que algum certame público
    /// traga. A resposta não distingue os casos de propósito: para um chamador anônimo, distinguir
    /// seria responder "esse identificador é um rascunho?".
    /// </para>
    /// </remarks>
    [HttpGet("certames/{id}")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "certame", Versions = [1])]
    [ProducesResponseType(typeof(CertamePublicadoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    [EmiteETag(Descricao = DescricaoDoSeloDoCertame)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> ObterCertamePublicado(
        [Description("Guid do processo seletivo ou identificador legível do certame (kebab-case, congelado na publicação).")] string id,
        [FromHeader(Name = "If-None-Match")] string? ifNoneMatch,
        CancellationToken cancellationToken)
    {
        IQuery<Result<CertamePublicadoDto>> consulta = Guid.TryParse(id, out Guid processoSeletivoId)
            ? new ObterCertamePublicadoQuery(processoSeletivoId)
            : new ObterCertamePublicadoPorIdentificadorQuery(id);

        return ServirAsync(consulta, ifNoneMatch, cancellationToken);
    }

    private async Task<IActionResult> ServirAsync(
        IQuery<Result<CertamePublicadoDto>> consulta,
        string? ifNoneMatch,
        CancellationToken cancellationToken)
    {
        Result<CertamePublicadoDto> resultado = await _queryBus
            .Send(consulta, cancellationToken)
            .ConfigureAwait(false);

        // Revalidação obrigatória, não cache proibido: o endereço não muda quando o certame é
        // retificado. Escrita ANTES de ramificar porque a recusa também precisa dela — sem
        // diretiva, um cache compartilhado atribui frescor heurístico ao 404 (RFC 9111 §4.2.2).
        Response.Headers.CacheControl = "no-cache";

        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        CertamePublicadoDto certame = resultado.Value!;
        string etag = $"\"{certame.VersaoProjecao}:{certame.HashConfiguracao}\"";

        Response.Headers.ETag = etag;

        return SeloDeEntidade.IfNoneMatchCoincide(ifNoneMatch, etag)
            ? StatusCode(StatusCodes.Status304NotModified)
            : Ok(certame);
    }
}
