namespace Unifesspa.UniPlus.Selecao.Application.Queries.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Regras.Formularios;

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
        IInscricaoRepository inscricaoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(enderecoNoAcervo);
        ArgumentNullException.ThrowIfNull(userContext);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode ler o formulário da inscrição."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."));
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterAtivaPorProcessoECpfAsync(query.ProcessoSeletivoId, cpfResult.Value!, cancellationToken)
            .ConfigureAwait(false);
        if (inscricao is null)
        {
            return Result<FormularioRenderizavelDto>.Failure(Inscricao.NaoEncontrada);
        }

        if (inscricao.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono)
        {
            return Result<FormularioRenderizavelDto>.Failure(erroDono);
        }

        Result<VersaoConfiguracao> resolvida = await VersaoDaInscricaoResolver
            .ResolverAsync(inscricao, certameDivulgadoRepository, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (resolvida.IsFailure)
        {
            return Result<FormularioRenderizavelDto>.Failure(resolvida.Error!);
        }

        return ProjecaoDoFormularioDaVersao.Projetar(
            resolvida.Value!, FinalidadeFormulario.Inscricao, resolvida.Value!.AtoCriadorId, registroCodecs, enderecoNoAcervo,
            comComprovacaoDocumental: false);
    }
}
