namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Services;

/// <summary>
/// Os formulários apresentados da versão em que está a inscrição do dono do token — a divulgada, no
/// rascunho; a congelada no primeiro envio, depois dele. É a mesma projeção de
/// <c>ObterFormularioApresentadoQueryHandler</c> (<see cref="ProjecaoDoFormularioDaVersao"/>), com a
/// versão resolvida por <see cref="VersaoDaInscricaoResolver"/>. Devolve todos os formulários que a inscrição
/// responde, em ordem de código.
/// </summary>
public static class ObterFormulariosDaInscricaoQueryHandler
{
    public static async Task<Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>> Handle(
        ObterFormulariosDaInscricaoQuery query,
        InscricaoDoDono inscricaoDoDono,
        VersaoDaInscricaoResolver versao,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoDoDono);
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Result<Inscricao> doCandidato = await inscricaoDoDono.ObterAtivaAsync(query.ProcessoSeletivoId, "ler os formulários da inscrição", cancellationToken)
            .ConfigureAwait(false);
        return doCandidato.IsFailure
            ? Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>.Failure(doCandidato.Error!)
            : await FormulariosDaVersaoDaInscricaoAsync(
                query, doCandidato.Value!, versao, registroCodecs, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>> FormulariosDaVersaoDaInscricaoAsync(
        ObterFormulariosDaInscricaoQuery query,
        Inscricao inscricao,
        VersaoDaInscricaoResolver versao,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> resolvida = await versao.ResolverAsync(inscricao, cancellationToken)
            .ConfigureAwait(false);
        if (resolvida.IsFailure)
        {
            return Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>>.Failure(resolvida.Error!);
        }

        return FormulariosDeInscricaoDaVersao(query, resolvida.Value!, registroCodecs);
    }

    private static Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> FormulariosDeInscricaoDaVersao(
        ObterFormulariosDaInscricaoQuery query, VersaoConfiguracao versao, IRegistroCodecsEnvelope registroCodecs)
    {
        Result<IReadOnlyList<FormularioApresentadoDoProcessoDto>> projetados = ProjecaoDoFormularioDaVersao.ProjetarDaInscricao(versao, registroCodecs);

        // Uma versão que recebeu inscrição foi publicada com o formulário de inscrição: a ausência
        // dele numa versão fixada é estado que a publicação não produz.
        if (projetados.IsSuccess && projetados.Value!.Count == 0)
        {
            throw new InvalidOperationException(
                $"A versão fixada da inscrição do processo {query.ProcessoSeletivoId} não tem o formulário de inscrição.");
        }

        return projetados;
    }
}
