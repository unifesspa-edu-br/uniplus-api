namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Domain.Entities;

using DTOs;

using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// O formulário renderizável da versão em que está a inscrição do dono do token — a divulgada, no
/// rascunho; a congelada no primeiro envio, depois dele. É a mesma projeção de
/// <c>ObterFormularioRenderizavelQueryHandler</c> (<see cref="ProjecaoDoFormularioDaVersao"/>), com a
/// versão resolvida por <see cref="VersaoDaInscricaoResolver"/>.
/// </summary>
public static class ObterFormularioDaInscricaoQueryHandler
{
    public static async Task<Result<FormularioRenderizavelDto>> Handle(
        ObterFormularioDaInscricaoQuery query,
        InscricaoAtivaDoCandidato inscricaoAtiva,
        FormularioDaInscricao formulario,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoAtiva);
        ArgumentNullException.ThrowIfNull(formulario);

        Result<Inscricao> inscricao = await inscricaoAtiva
            .ObterAsync(query.ProcessoSeletivoId, userContext, "ler o formulário da inscrição", cancellationToken).ConfigureAwait(false);
        return inscricao.IsFailure
            ? Result<FormularioRenderizavelDto>.Failure(inscricao.Error!)
            : await formulario.ProjetarAsync(inscricao.Value!, cancellationToken).ConfigureAwait(false);
    }
}
