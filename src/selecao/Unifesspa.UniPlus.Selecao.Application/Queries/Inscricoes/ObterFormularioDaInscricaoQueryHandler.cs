namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.ValueObjects;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O formulário apresentado da versão em que está a inscrição do dono do token — a divulgada, no
/// rascunho; a congelada no primeiro envio, depois dele. É a mesma projeção de
/// <c>ObterFormularioApresentadoQueryHandler</c> (<see cref="ProjecaoDoFormularioDaVersao"/>), com a
/// versão resolvida por <see cref="VersaoDaInscricaoResolver"/>.
/// </summary>
public static class ObterFormularioDaInscricaoQueryHandler
{
    public static async Task<Result<FormularioApresentadoDoProcessoDto>> Handle(
        ObterFormularioDaInscricaoQuery query,
        InscricaoDoDono inscricaoDoDono,
        VersaoDaInscricaoResolver versao,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoDoDono);
        ArgumentNullException.ThrowIfNull(versao);
        ArgumentNullException.ThrowIfNull(registroCodecs);

        Result<Inscricao> doCandidato = await inscricaoDoDono.ObterAtivaAsync(query.ProcessoSeletivoId, "ler o formulário da inscrição", cancellationToken)
            .ConfigureAwait(false);
        return doCandidato.IsFailure
            ? Result<FormularioApresentadoDoProcessoDto>.Failure(doCandidato.Error!)
            : await FormularioDaVersaoDaInscricaoAsync(
                query, doCandidato.Value!, versao, registroCodecs, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result<FormularioApresentadoDoProcessoDto>> FormularioDaVersaoDaInscricaoAsync(
        ObterFormularioDaInscricaoQuery query,
        Inscricao inscricao,
        VersaoDaInscricaoResolver versao,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> resolvida = await versao.ResolverAsync(inscricao, cancellationToken)
            .ConfigureAwait(false);
        if (resolvida.IsFailure)
        {
            return Result<FormularioApresentadoDoProcessoDto>.Failure(resolvida.Error!);
        }

        return FormularioDeInscricaoDaVersao(query, resolvida.Value!, registroCodecs);
    }

    private static Result<FormularioApresentadoDoProcessoDto> FormularioDeInscricaoDaVersao(
        ObterFormularioDaInscricaoQuery query, VersaoConfiguracao versao, IRegistroCodecsEnvelope registroCodecs)
    {
        Result<FormularioApresentadoDoProcessoDto?> projetado = ProjecaoDoFormularioDaVersao.Projetar(
            versao, FinalidadeFormulario.Inscricao, registroCodecs, acervoDaComprovacao: null);
        if (projetado.IsFailure)
        {
            return Result<FormularioApresentadoDoProcessoDto>.Failure(projetado.Error!);
        }

        // Uma versão que recebeu inscrição foi publicada com o formulário de inscrição: a ausência
        // dele numa versão fixada é estado que a publicação não produz.
        return Result<FormularioApresentadoDoProcessoDto>.Success(projetado.Value
            ?? throw new InvalidOperationException(
                $"A versão fixada da inscrição do processo {query.ProcessoSeletivoId} não tem o formulário de inscrição."));
    }
}
