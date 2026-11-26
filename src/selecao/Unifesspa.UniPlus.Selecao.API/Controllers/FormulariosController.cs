namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using System.Diagnostics.CodeAnalysis;

using Application.Commands.ProcessosSeletivos;
using Application.DTOs;
using Application.Queries.ProcessosSeletivos;

using Contracts.Requests;

using Http;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Infrastructure.Core.Formatting;
using Unifesspa.UniPlus.Infrastructure.Core.Idempotency;
using Unifesspa.UniPlus.Infrastructure.Core.OpenApi;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;

/// <summary>
/// Formulários do processo pelo código do formulário (UNI-REQ-0144): leitura pública e escrita
/// administrativa, com o código no caminho (por exemplo <c>INSCRICAO</c> ou <c>ISENCAO_TAXA</c>). Sem
/// <c>[Authorize]</c> de classe: a leitura é anônima, e a escrita exige a permissão de configurar o
/// processo, declarada em cada action — o host não tem fallback policy, então omitir a anotação nasceria
/// o endpoint anônimo por omissão.
/// </summary>
[ApiController]
[EmiteEsperaEsgotadaPelaTrava]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class FormulariosController : ControllerBase
{
    /// <summary>
    /// Segmento do caminho que leva o código do formulário (ADR-0176: maiúsculas, dígitos e sublinhado, até
    /// 60 caracteres). O código fora do padrão não casa com a rota e responde 404; só a criação por PUT não
    /// o restringe, para devolver a recusa do domínio com o motivo.
    /// </summary>
    private const string SegmentoDoCodigo = "{codigo:regex(^(?=.{{1,60}}$)[[A-Z]][[A-Z0-9]]*(_[[A-Z0-9]]+)*$)}";

    private const string RaizDosFormulariosDoProcesso = "admin/processos-seletivos/{id:guid}/formularios/";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public FormulariosController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Apresentação pública do formulário de um código — título, seções, termos e os fatos
    /// coletados com sua apresentação, e as regras que o front interpreta (ADR-0139), projetados da
    /// versão que o certame <b>divulgado</b> serve.
    /// </summary>
    /// <remarks>
    /// <para>
    /// É a MESMA versão que <c>GET /certames/{id}</c> serve, e não a mais nova por relógio.
    /// Enquanto o ato de uma retificação não se confirma, ela não tem publicidade: servir o
    /// formulário dela faria o candidato ler um edital e preencher o de outro.
    /// </para>
    /// <para>
    /// <c>404</c> cobre, com a mesma resposta, o processo inexistente, o em rascunho, o sem versão
    /// vigente, o sem divulgação, o código fora do padrão e o processo sem formulário com
    /// esse código. Para um chamador anônimo, distinguir seria responder "esse identificador é um
    /// rascunho?".
    /// </para>
    /// </remarks>
    [HttpGet("processos-seletivos/{id:guid}/formularios/" + SegmentoDoCodigo)]
    [EndpointName("ObterFormularioDoProcesso")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "formulario", Versions = [2])]
    [ProducesResponseType(typeof(FormularioApresentadoDoProcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterFormularioApresentado(Guid id, string codigo, CancellationToken cancellationToken)
    {
        // Revalidação obrigatória, não cache proibido: o endereço não muda quando a divulgação
        // chega nem quando o certame é retificado, e sem diretiva um cache compartilhado atribui
        // frescor heurístico ao 404 transitório (RFC 9111 §4.2.2).
        Response.Headers.CacheControl = "no-cache";

        Result<FormularioApresentadoDoProcessoDto> resultado = await _queryBus
            .Send(new ObterFormularioApresentadoQuery(id, codigo), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O formulário do código como a configuração viva o define agora — o rascunho, no mesmo formato que o certame divulgado serve. É o que a simulação do
    /// administrador interpreta antes de publicar; nada é gravado.
    /// </summary>
    [HttpGet(RaizDosFormulariosDoProcesso + SegmentoDoCodigo + "/apresentacao")]
    [EndpointName("ObterFormularioDoProcessoParaManutencao")]
    [Authorize]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [VendorMediaType(Resource = "formulario", Versions = [2])]
    [ProducesResponseType(typeof(FormularioApresentadoDoProcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterFormularioApresentadoDoRascunho(Guid id, string codigo, CancellationToken cancellationToken)
    {
        Result<FormularioApresentadoDoProcessoDto> resultado = await _queryBus
            .Send(new ObterFormularioApresentadoDoRascunhoQuery(id, codigo), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Define ou substitui o formulário do código: a fase do cronograma, se é obrigatório, o título, as
    /// páginas e, opcionalmente, a consequência da falta de envio. O código novo cria o formulário; o código
    /// que o processo já tem o substitui. Editável só em rascunho.
    /// </summary>
    [HttpPut(RaizDosFormulariosDoProcesso + "{codigo}")]
    [EndpointName("DefinirFormularioDoProcesso")]
    [Authorize]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DefinirFormulario(
        Guid id,
        string codigo,
        [FromBody] DefinirFormularioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(
            new DefinirFormularioCommand(
                id, codigo, request.FaseId, request.Titulo, request.Obrigatorio, request.Paginas, request.ConsequenciaDaFaltaDeEnvio),
            cancellationToken);
    }

    /// <summary>Remove o formulário do código, com os itens e os termos dele. Só em rascunho.</summary>
    [HttpDelete(RaizDosFormulariosDoProcesso + SegmentoDoCodigo)]
    [EndpointName("RemoverFormularioDoProcesso")]
    [Authorize]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RemoverFormulario(Guid id, string codigo, CancellationToken cancellationToken) =>
        EnviarMutacao(new RemoverFormularioCommand(id, codigo), cancellationToken);

    /// <summary>
    /// Substitui os itens do formulário do código: os fatos que ele coleta, com a ordem, a seção
    /// e a pré-condição. Cada fato é coletado por um só formulário do processo.
    /// </summary>
    [HttpPut(RaizDosFormulariosDoProcesso + SegmentoDoCodigo + "/itens")]
    [EndpointName("DefinirItensDoFormularioDoProcesso")]
    [Authorize]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DefinirItens(
        Guid id,
        string codigo,
        [FromBody] DefinirItensDoFormularioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(new DefinirFatosColetadosCommand(id, codigo, request.Itens, request.Grupos), cancellationToken);
    }

    /// <summary>
    /// Substitui os termos de consentimento que o formulário do código exige, escolhidos no
    /// catálogo por termo e versão (UNI-REQ-0086).
    /// </summary>
    [HttpPut(RaizDosFormulariosDoProcesso + SegmentoDoCodigo + "/termos")]
    [EndpointName("DefinirTermosDoFormularioDoProcesso")]
    [Authorize]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [RequiresIdempotencyKey]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> DefinirTermos(
        Guid id,
        string codigo,
        [FromBody] DefinirTermosDoFormularioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(new DefinirTermosDoFormularioCommand(id, codigo, request.Termos), cancellationToken);
    }

    /// <summary>
    /// Aplica ao processo a cópia de um modelo de formulário: o formulário do código informado, que o processo já precisa ter, é
    /// substituído por inteiro pelo conteúdo do modelo, e a resposta relata o que a cópia trouxe, manteve,
    /// descartou e derivou (UNI-REQ-0144). Só em rascunho; mudar o modelo depois não muda o processo.
    /// </summary>
    [HttpPost("admin/processos-seletivos/{id:guid}/formularios/aplicacoes-de-modelo")]
    [EndpointName("AplicarModeloDeFormularioAoProcesso")]
    [Authorize]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [RequiresIdempotencyKey]
    [VendorMediaType(Resource = "aplicacao-de-modelo-formulario", Versions = [1])]
    [ProducesResponseType(typeof(AplicacaoDeModeloDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> AplicarModelo(
        Guid id,
        [FromBody] AplicacaoDeModeloInput request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<AplicacaoDeModeloDto> resultado = await _commandBus
            .Send(new AplicarModeloFormularioCommand(id, request.ModeloId, request.FormularioCodigo), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>O fluxo comum das escritas: a resposta é 204 sem corpo.</summary>
    private async Task<IActionResult> EnviarMutacao(ICommand<Result> comando, CancellationToken cancellationToken)
    {
        Result resultado = await _commandBus.Send(comando, cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
