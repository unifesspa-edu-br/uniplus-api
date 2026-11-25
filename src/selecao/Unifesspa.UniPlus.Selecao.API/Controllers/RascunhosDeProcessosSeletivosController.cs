namespace Unifesspa.UniPlus.Selecao.API.Controllers;

using Application.Commands.Rascunhos;
using Application.DTOs;
using Application.Queries.Rascunhos;

using Contracts.Responses;

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
using Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O rascunho do processo seletivo: o documento da configuração antes da primeira publicação. O corpo de criação e de
/// substituição é o próprio documento JSON, no formato de <c>contracts/selecao/configuracao/v1</c>, de até 2 MiB; a
/// exportação devolve o mesmo documento. Cada revisão do rascunho tem um <c>ETag</c>, e a substituição e a exclusão exigem
/// o <c>If-Match</c> da revisão que a pessoa leu.
/// </summary>
[ApiController]
[Route("rascunhos-de-processos-seletivos")]
[Tags("RascunhoDeProcessoSeletivo")]
[Authorize]
public sealed class RascunhosDeProcessosSeletivosController : ControllerBase
{
    private const string DescricaoDoETag = "A revisão do rascunho que a resposta descreve, para o If-Match da próxima substituição ou exclusão.";

    private const string DescricaoDoCorpoAcimaDoLimiteNaCriacao =
        "O corpo passa de 2 MiB (uniplus.idempotency.body_muito_grande), ou o documento passa do limite de profundidade ou de nós do formato "
        + "(uniplus.selecao.documento_da_configuracao.acima_do_limite).";

    private const string DescricaoDoCorpoAcimaDoLimiteNaSubstituicao =
        "O documento passa de um limite do formato: 2 MiB, profundidade ou quantidade de nós (uniplus.selecao.documento_da_configuracao.acima_do_limite).";

    private const string DescricaoDaSubstituicao =
        "Substitui o documento inteiro do rascunho. O corpo é o documento completo: todo bloco que o corpo não traz é apagado, e nada é "
        + "mesclado com o que estava salvo. Uma lista enviada como [] continua presente e declara nenhum item; só o bloco omitido é "
        + "apagado. Para mudar uma parte, leia o rascunho pelo GET, altere a parte e envie o documento inteiro. Devolva a chave de cada "
        + "parte que o esquema identifica por chave, como a leitura a trouxe, porque as referências entre as partes (decisões, fatos, "
        + "produtos) usam a chave; essa parte, sem a chave, recusa o salvamento (422). A distribuição de vagas é identificada por "
        + "ofertaCursoCodigo e não tem chave. O id da raiz e os campos somente de leitura enviados são ignorados: vale o rascunho da "
        + "rota. Exige o If-Match da revisão lida. Se outra alteração do mesmo rascunho estiver em andamento e a espera passar do "
        + "limite, a resposta é 503, nada é gravado e o envio pode ser repetido.";

    private readonly ICommandBus _commandBus;
    private readonly IQueryBus _queryBus;
    private readonly IDomainErrorMapper _mapper;

    public RascunhosDeProcessosSeletivosController(ICommandBus commandBus, IQueryBus queryBus, IDomainErrorMapper mapper)
    {
        _commandBus = commandBus;
        _queryBus = queryBus;
        _mapper = mapper;
    }

    /// <summary>
    /// Cria um rascunho a partir do documento da configuração, digitado pela tela ou importado de um arquivo. O documento
    /// precisa respeitar o formato e nomear em <c>identificacao.unidadeAdministradoraCodigo</c> uma unidade em que a pessoa
    /// configura processos. O rascunho nasce na revisão 1 e o <c>id</c> devolvido será o do processo na publicação.
    /// </summary>
    /// <remarks>
    /// O <c>id</c>, a <c>versao</c> e o <c>publicadoEm</c> da raiz e todo campo marcado <c>readOnly</c> no esquema são
    /// ignorados. Toda parte que o esquema exige com <c>chave</c> a traz: a chave é escolhida por quem cria a parte, e o
    /// sistema não a atribui. A parte sem <c>chave</c> recusa o salvamento (422), no ponteiro da chave que falta. O
    /// diagnóstico das regras de negócio ainda não é calculado: <c>diagnosticos</c> vem vazio.
    /// <para>
    /// A resposta não devolve o documento salvo. Os campos somente de leitura da revisão salva obtêm-se relendo a revisão por
    /// <c>GET /rascunhos-de-processos-seletivos/{id}</c> (o <c>Location</c> da resposta). Se o <c>ETag</c> do GET for o
    /// recebido no salvar, a releitura é a revisão salva e o cliente pode copiar dela, pelo ponteiro, os campos somente de
    /// leitura. O <c>ETag</c> confere só a revisão guardada no servidor, não o que foi digitado depois do envio: um campo
    /// descritivo (o nome e a sigla da unidade, o nome do tipo de processo) só é copiado se o campo de código que o define
    /// (<c>unidadeAdministradoraCodigo</c>, <c>tipoProcessoCodigo</c>) continua o mesmo da tela; se foi alterado, o
    /// descritivo da releitura é de outro código e não vale. Se o <c>ETag</c> for diferente, outra pessoa salvou, e o próximo
    /// PUT com o <c>If-Match</c> antigo recebe 412.
    /// </para>
    /// </remarks>
    [HttpPost]
    [EndpointName("CriarRascunhoDeProcessoSeletivo")]
    [RequiresIdempotencyKey(MaxBodyBytes = LimitesDoDocumento.TamanhoMaximoEmBytes)]
    [Consumes("application/json")]
    [CorpoJson(EsquemaPublicadoDoDocumento.Componente)]
    [EmiteETag(Descricao = DescricaoDoETag)]
    [EmiteHeader("Location", "Endereço do rascunho criado.", Status = "201")]
    [ProducesResponseType(typeof(RascunhoCriadoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden,
        Description = "A pessoa não configura processos na unidade do documento, ou o código não resolve para uma unidade (uniplus.selecao.rascunho_de_processo_seletivo.unidade_fora_da_permissao).")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge,
        Description = DescricaoDoCorpoAcimaDoLimiteNaCriacao)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Criar(CancellationToken cancellationToken)
    {
        Result<RascunhoSalvoDto> resultado = await EnviarDocumentoAsync(
            documento => new CriarRascunhoDeProcessoSeletivoCommand(documento), cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.ETag = resultado.Value!.ETag;
        return CreatedAtAction(nameof(Obter), new { id = resultado.Value.Id }, RascunhoCriadoResponse.De(resultado.Value, _mapper));
    }

    /// <summary>
    /// Exporta o documento do rascunho. Traz o <c>id</c> do rascunho na raiz e, ao lado dos códigos de identificação, os
    /// descritivos só de leitura que o Uni+ já guarda: nome e sigla da unidade administradora e nome do tipo de processo.
    /// Não traz <c>versao</c> nem <c>publicadoEm</c>, que não existem antes da publicação. Os demais descritivos só de leitura
    /// do formato (distribuição de vagas, cronograma, exigências e derivados) ainda não são preenchidos. O <c>ETag</c> da
    /// resposta identifica a revisão exportada.
    /// </summary>
    [HttpGet("{id:guid}")]
    [EndpointName("ObterRascunhoDeProcessoSeletivo")]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [VendorMediaType(Resource = "rascunho-de-processo-seletivo", Versions = [1])]
    [RespostaJson(EsquemaPublicadoDoDocumento.Componente)]
    [EmiteETag(Descricao = DescricaoDoETag)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status406NotAcceptable)]
    public async Task<IActionResult> Obter(Guid id, CancellationToken cancellationToken)
    {
        Result<RascunhoExportadoDto> resultado = await _queryBus
            .Send(new ObterRascunhoDeProcessoSeletivoQuery(id), cancellationToken)
            .ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.ETag = resultado.Value!.ETag;
        return Ok(resultado.Value.Documento);
    }

    /// <summary>
    /// Substitui o documento inteiro do rascunho, na revisão que a pessoa leu (<c>If-Match</c>). O corpo é o documento
    /// completo: o bloco omitido é apagado, e o que o documento anterior tinha não é mesclado.
    /// </summary>
    /// <remarks>
    /// Os campos <c>readOnly</c> enviados e o <c>id</c> da raiz são ignorados: vale o rascunho da rota. Toda parte que o
    /// esquema exige com <c>chave</c> a traz; a parte sem <c>chave</c> recusa o salvamento (422), no ponteiro da chave que
    /// falta. Para conservar as referências entre partes do documento (decisões, dados, publicações previstas), o cliente devolve a
    /// <c>chave</c> de cada parte como a exportação a trouxe; a chave é escolhida por quem cria a parte e não muda ao
    /// renomear nem ao reordenar, e a referência a uma chave que não existe mais vira diagnóstico. A forma inválida recusa o
    /// documento inteiro sem substituir o anterior. A unidade administradora pode
    /// mudar: a pessoa precisa configurar processos na unidade nova, além da atual. Sem <c>Idempotency-Key</c>: a
    /// substituição integral com precondição já é idempotente. A espera pela trava da linha, quando outra operação a segura,
    /// responde 503.
    /// <para>
    /// A resposta não devolve o documento salvo. Os campos somente de leitura da revisão salva obtêm-se relendo a revisão
    /// por <c>GET /rascunhos-de-processos-seletivos/{id}</c>. Se o <c>ETag</c> do GET for o recebido no salvar, a releitura é
    /// a revisão salva e o cliente pode copiar dela, pelo ponteiro, os campos somente de leitura. O <c>ETag</c> confere só a
    /// revisão guardada no servidor, não o que foi digitado depois do envio: um campo descritivo (o nome e a sigla da
    /// unidade, o nome do tipo de processo) só é copiado se o campo de código que o define
    /// (<c>unidadeAdministradoraCodigo</c>, <c>tipoProcessoCodigo</c>) continua o mesmo da tela; se foi alterado, o
    /// descritivo da releitura é de outro código e não vale. Se o <c>ETag</c> for diferente, outra pessoa salvou, e o próximo
    /// PUT com o <c>If-Match</c> antigo recebe 412.
    /// </para>
    /// </remarks>
    [HttpPut("{id:guid}")]
    [EndpointName("SubstituirRascunhoDeProcessoSeletivo")]
    [EndpointSummary("Substitui o documento inteiro do rascunho")]
    [EndpointDescription(DescricaoDaSubstituicao)]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [Consumes("application/json")]
    [CorpoJson(EsquemaPublicadoDoDocumento.Componente)]
    [PrecondicaoObrigatoria]
    [EmiteETag(Descricao = DescricaoDoETag)]
    [EmiteEsperaEsgotadaPelaTrava]
    [ProducesResponseType(typeof(RascunhoSubstituidoResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "O rascunho já foi publicado e virou o processo seletivo de mesmo identificador (uniplus.selecao.rascunho_de_processo_seletivo.ja_publicado).")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status413PayloadTooLarge,
        Description = DescricaoDoCorpoAcimaDoLimiteNaSubstituicao)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Substituir(
        Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        Result<PrecondicaoIfMatch> precondicao = PrecondicaoDoRascunho.Exigir(ifMatch);
        Result<RascunhoSalvoDto> resultado = precondicao.IsFailure
            ? Result<RascunhoSalvoDto>.ValidationFailure(precondicao.Errors)
            : await EnviarDocumentoAsync(
                documento => new SubstituirRascunhoDeProcessoSeletivoCommand(id, documento, precondicao.Value!), cancellationToken).ConfigureAwait(false);
        if (resultado.IsFailure)
        {
            return resultado.ToActionResult(_mapper);
        }

        Response.Headers.ETag = resultado.Value!.ETag;
        return Ok(RascunhoSubstituidoResponse.De(resultado.Value, _mapper));
    }

    /// <summary>
    /// Exclui o rascunho, na revisão que a pessoa leu (<c>If-Match</c>). Só o rascunho que ainda não virou processo se exclui:
    /// o publicado responde 409.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [EndpointName("ExcluirRascunhoDeProcessoSeletivo")]
    [ExigePermissaoNoProcesso(PermissaoSelecao.ProcessosConfigurar)]
    [PrecondicaoObrigatoria]
    [EmiteEsperaEsgotadaPelaTrava]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict,
        Description = "O rascunho já foi publicado e virou o processo seletivo de mesmo identificador (uniplus.selecao.rascunho_de_processo_seletivo.ja_publicado).")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status428PreconditionRequired)]
    public async Task<IActionResult> Excluir(
        Guid id, [FromHeader(Name = "If-Match")] string? ifMatch, CancellationToken cancellationToken)
    {
        Result<PrecondicaoIfMatch> precondicao = IfMatchHeader.Analisar(ifMatch);
        if (precondicao.IsFailure)
        {
            return precondicao.ToActionResult(_mapper);
        }

        Result resultado = await _commandBus
            .Send(new ExcluirRascunhoDeProcessoSeletivoCommand(id, precondicao.Value!), cancellationToken)
            .ConfigureAwait(false);
        return resultado.IsSuccess ? NoContent() : resultado.ToActionResult(_mapper);
    }

    // O corpo é lido aqui, na borda, e só o texto do documento atravessa para o comando.
    private async Task<Result<RascunhoSalvoDto>> EnviarDocumentoAsync(
        Func<string, ICommand<Result<RascunhoSalvoDto>>> comando, CancellationToken cancellationToken)
    {
        Result<string> documento = await CorpoDoDocumento.LerAsync(Request.Body, cancellationToken).ConfigureAwait(false);
        return documento.IsFailure
            ? Result<RascunhoSalvoDto>.ValidationFailure(documento.Errors)
            : await _commandBus.Send(comando(documento.Value!), cancellationToken).ConfigureAwait(false);
    }
}
