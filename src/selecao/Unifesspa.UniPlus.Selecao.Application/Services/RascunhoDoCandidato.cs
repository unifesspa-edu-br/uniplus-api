namespace Unifesspa.UniPlus.Selecao.Application.Services;

using System.Text.Json;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Monta o pedido do candidato para salvar a inscrição dele: a inscrição em rascunho, dentro do período, ligada à versão do
/// formulário em que está, com o corpo lido e a identidade das ocorrências de grupo conferida.
/// </summary>
public sealed class RascunhoDoCandidato(
    InscricaoAtivaDoCandidato inscricaoAtiva,
    PeriodoDaInscricaoResolver periodo,
    VersaoDaInscricaoResolver versao,
    ContextoDaVersaoFixadaResolver contexto,
    IResolvedorFusoInstitucional fuso,
    TimeProvider relogio)
{
    public async Task<Result<PedidoDoRascunho>> PedidoAsync(
        Guid processoSeletivoId, InscricaoRequest request, IUserContext userContext, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<InscricaoNoPeriodo> inscricao = await EditavelAsync(processoSeletivoId, userContext, cancellationToken).ConfigureAwait(false);
        if (inscricao.IsFailure)
        {
            return Result<PedidoDoRascunho>.ValidationFailure(inscricao.Errors);
        }

        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoAsync(processoSeletivoId, request, inscricao.Value!.Inscricao, cancellationToken).ConfigureAwait(false);
        Result<PedidoDoRascunho> pedido = contexto.IsFailure
            ? Result<PedidoDoRascunho>.ValidationFailure(contexto.Errors)
            : PedidoDoCorpo(request, inscricao.Value!, contexto.Value!);
        return pedido.IsFailure ? pedido : ComAIdentidadeDasOcorrenciasConferida(pedido.Value!);
    }

    /// <summary>
    /// A inscrição do candidato no processo, dentro do período e ainda em rascunho, com o instante que conferiu o período: é
    /// o envio de que se contam os limites da faixa de data. A inscrição enviada não aceita rascunho: recusar antes das
    /// respostas evita pedir correção de um campo numa gravação que não seria aceita de jeito nenhum. Vem depois do período,
    /// porque fora dele nem o novo envio que a recusa orienta seria aceito.
    /// </summary>
    private async Task<Result<InscricaoNoPeriodo>> EditavelAsync(Guid processoSeletivoId, IUserContext userContext, CancellationToken cancellationToken)
    {
        Result<Inscricao> inscricao = await inscricaoAtiva.ObterAsync(processoSeletivoId, userContext, "salvar a inscrição", cancellationToken).ConfigureAwait(false);
        if (inscricao.IsFailure)
        {
            return Result<InscricaoNoPeriodo>.ValidationFailure(inscricao.Errors);
        }

        DateTimeOffset instante = relogio.GetUtcNow();
        DomainError? recusa = await periodo.ConferirAsync(processoSeletivoId, instante, cancellationToken).ConfigureAwait(false);
        recusa ??= inscricao.Value!.ConferirAindaEmRascunho();
        return recusa is null ? Result<InscricaoNoPeriodo>.Success(new InscricaoNoPeriodo(inscricao.Value!, instante)) : Result<InscricaoNoPeriodo>.Failure(recusa);
    }

    private async Task<Result<ContextoDaVersaoFixada>> ContextoDaVersaoAsync(
        Guid processoSeletivoId, InscricaoRequest request, Inscricao inscricao, CancellationToken cancellationToken)
    {
        Result adotada = await versao.AdotarAsync(inscricao, cancellationToken).ConfigureAwait(false);
        return adotada.IsFailure
            ? Result<ContextoDaVersaoFixada>.ValidationFailure(adotada.Errors)
            : await contexto.ResolverAsync(inscricao.Formulario.VersaoFormularioId, processoSeletivoId, request.OfertaCursoId, request.OfertaCursoSegundaOpcaoId, cancellationToken).ConfigureAwait(false);
    }

    private Result<PedidoDoRascunho> PedidoDoCorpo(InscricaoRequest request, InscricaoNoPeriodo inscricao, ContextoDaVersaoFixada contexto)
    {
        Result<TimeZoneInfo> fusoInstitucional = fuso.Resolver();
        if (fusoInstitucional.IsFailure)
        {
            return Result<PedidoDoRascunho>.ValidationFailure(fusoInstitucional.Errors);
        }

        Result<CorpoDoRascunho> corpo = CorpoDoRascunho.De(request);
        return corpo.IsFailure
            ? Result<PedidoDoRascunho>.ValidationFailure(corpo.Errors)
            : Result<PedidoDoRascunho>.Success(new PedidoDoRascunho(
                inscricao.Inscricao, contexto, new EnvioDasRespostas(inscricao.Instante, fusoInstitucional.Value!), corpo.Value!));
    }

    /// <summary>
    /// A ocorrência de grupo com id repetido ou em branco não se avalia: a identidade da ocorrência é o que a avaliação do
    /// formulário exige, e sem ela a avaliação lança. A recusa é a mesma do envio e vem da regra de forma do domínio, só a de identidade; as
    /// demais recusas de forma seguem o curso de antes, na gravação, depois da precondição de versão.
    /// </summary>
    private static Result<PedidoDoRascunho> ComAIdentidadeDasOcorrenciasConferida(PedidoDoRascunho pedido)
    {
        List<FieldError> repetidas = [.. PreenchimentoDoCandidato.RecusasDeForma(pedido.Corpo.Respostas, pedido.Corpo.Grupos, pedido.Contexto.Admitidas)
            .Where(static r => r.Error.Code == InscricaoErrorCodes.GrupoIdDuplicado)];
        return repetidas.Count > 0 ? Result<PedidoDoRascunho>.ValidationFailure(repetidas) : Result<PedidoDoRascunho>.Success(pedido);
    }
}
