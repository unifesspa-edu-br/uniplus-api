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
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.API.Authorization;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Formulários do processo por finalidade (UNI-REQ-0144): leitura pública e escrita
/// administrativa, com a finalidade no caminho (<c>INSCRICAO</c>, <c>ISENCAO_TAXA</c>,
/// <c>HABILITACAO</c>). Sem <c>[Authorize]</c> de classe: a leitura é anônima, e a escrita exige a
/// permissão de configurar o processo, declarada em cada action — o host não tem fallback policy, então
/// omitir a anotação nasceria o endpoint anônimo por omissão.
/// </summary>
[ApiController]
[EmiteEsperaEsgotadaPelaTrava]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "ASP.NET Core ControllerFeatureProvider só descobre controllers public; sem isso o MVC ignora a classe e nenhum endpoint é registrado.")]
public sealed class FormulariosController : ControllerBase
{
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
    /// Apresentação pública do formulário de uma finalidade — título, seções, termos e os fatos
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
    /// vigente, o sem divulgação, a finalidade desconhecida e o processo sem formulário da
    /// finalidade. Para um chamador anônimo, distinguir seria responder "esse identificador é um
    /// rascunho?".
    /// </para>
    /// </remarks>
    [HttpGet("processos-seletivos/{id:guid}/formularios/{finalidade}")]
    [EndpointName("ObterFormularioDoProcesso")]
    [AllowAnonymous]
    [VendorMediaType(Resource = "formulario", Versions = [2])]
    [ProducesResponseType(typeof(FormularioApresentadoDoProcessoDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> ObterFormularioApresentado(Guid id, [AceitaCodigosDe<FinalidadeFormulario>] string finalidade, CancellationToken cancellationToken)
    {
        // Revalidação obrigatória, não cache proibido: o endereço não muda quando a divulgação
        // chega nem quando o certame é retificado, e sem diretiva um cache compartilhado atribui
        // frescor heurístico ao 404 transitório (RFC 9111 §4.2.2).
        Response.Headers.CacheControl = "no-cache";

        if (EstruturaFormulario.FinalidadeDoCodigo(finalidade) is not { } alvo)
        {
            return NotFound();
        }

        Result<FormularioApresentadoDoProcessoDto> resultado = await _queryBus
            .Send(new ObterFormularioApresentadoQuery(id, alvo.Codigo()), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// O formulário da finalidade como a configuração viva o define agora — o rascunho, no mesmo formato que o certame divulgado serve. É o que a simulação do
    /// administrador interpreta antes de publicar; nada é gravado.
    /// </summary>
    [HttpGet("admin/processos-seletivos/{id:guid}/formularios/{finalidade}/apresentacao")]
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
    public async Task<IActionResult> ObterFormularioApresentadoDoRascunho(Guid id, [AceitaCodigosDe<FinalidadeFormulario>] string finalidade, CancellationToken cancellationToken)
    {
        if (EstruturaFormulario.FinalidadeDoCodigo(finalidade) is not { } alvo)
        {
            return NotFound();
        }

        Result<FormularioApresentadoDoProcessoDto> resultado = await _queryBus
            .Send(new ObterFormularioApresentadoDoRascunhoQuery(id, alvo.Codigo()), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? Ok(resultado.Value) : resultado.ToActionResult(_mapper);
    }

    /// <summary>
    /// Define ou substitui o formulário da finalidade: a fase do cronograma, o título, as páginas e,
    /// opcionalmente, a consequência da falta de envio. Editável só em rascunho.
    /// </summary>
    [HttpPut("admin/processos-seletivos/{id:guid}/formularios/{finalidade}")]
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
        [AceitaCodigosDe<FinalidadeFormulario>] string finalidade,
        [FromBody] DefinirFormularioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(finalidade,
            alvo => new DefinirFormularioCommand(
                id, alvo.Codigo(), request.FaseId, request.Titulo, CorrespondenciaDaFinalidade.DeclaracaoDa(alvo).Obrigatorio, request.Paginas, request.ConsequenciaDaFaltaDeEnvio),
            cancellationToken);
    }

    /// <summary>Remove o formulário da finalidade, com os itens e os termos dele. Só em rascunho.</summary>
    [HttpDelete("admin/processos-seletivos/{id:guid}/formularios/{finalidade}")]
    [EndpointName("RemoverFormularioDoProcesso")]
    [Authorize]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public Task<IActionResult> RemoverFormulario(Guid id, [AceitaCodigosDe<FinalidadeFormulario>] string finalidade, CancellationToken cancellationToken) =>
        EnviarMutacao(finalidade,
            alvo => new RemoverFormularioCommand(id, alvo.Codigo()),
            cancellationToken);

    /// <summary>
    /// Substitui os itens do formulário da finalidade: os fatos que ele coleta, com a ordem, a seção
    /// e a pré-condição. Cada fato é coletado por um só formulário do processo.
    /// </summary>
    [HttpPut("admin/processos-seletivos/{id:guid}/formularios/{finalidade}/itens")]
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
        [AceitaCodigosDe<FinalidadeFormulario>] string finalidade,
        [FromBody] DefinirItensDoFormularioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(finalidade,
            alvo => new DefinirFatosColetadosCommand(id, alvo.Codigo(), request.Itens, request.Grupos),
            cancellationToken);
    }

    /// <summary>
    /// Substitui os termos de consentimento que o formulário da finalidade exige, escolhidos no
    /// catálogo por termo e versão (UNI-REQ-0086).
    /// </summary>
    [HttpPut("admin/processos-seletivos/{id:guid}/formularios/{finalidade}/termos")]
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
        [AceitaCodigosDe<FinalidadeFormulario>] string finalidade,
        [FromBody] DefinirTermosDoFormularioRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return EnviarMutacao(finalidade,
            alvo => new DefinirTermosDoFormularioCommand(id, alvo.Codigo(), request.Termos),
            cancellationToken);
    }

    /// <summary>
    /// Aplica ao processo a cópia de um modelo de formulário: o formulário da finalidade do modelo é
    /// criado ou substituído por inteiro, e a resposta relata o que a cópia trouxe, manteve,
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

    /// <summary>
    /// O fluxo comum das escritas: finalidade desconhecida é 404, e a resposta é 204 sem corpo.
    /// </summary>
    private async Task<IActionResult> EnviarMutacao(
        string finalidade,
        Func<FinalidadeFormulario, ICommand<Result>> comando,
        CancellationToken cancellationToken)
    {
        if (EstruturaFormulario.FinalidadeDoCodigo(finalidade) is not { } alvo)
        {
            return NotFound();
        }

        Result resultado = await _commandBus.Send(comando(alvo), cancellationToken).ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }
}
