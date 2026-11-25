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
/// O formulário apresentado da versão em que está a inscrição do dono do token — a divulgada, no
/// rascunho; a congelada no primeiro envio, depois dele. É a mesma projeção de
/// <c>ObterFormularioApresentadoQueryHandler</c> (<see cref="ProjecaoDoFormularioDaVersao"/>), com a
/// versão resolvida por <see cref="VersaoDaInscricaoResolver"/>.
/// </summary>
public static class ObterFormularioDaInscricaoQueryHandler
{
    public static async Task<Result<FormularioApresentadoDoProcessoDto>> Handle(
        ObterFormularioDaInscricaoQuery query,
        IInscricaoRepository inscricaoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(userContext);

        Result<Inscricao> doCandidato = await InscricaoDoCandidatoAsync(query, inscricaoRepository, userContext, cancellationToken)
            .ConfigureAwait(false);
        return doCandidato.IsFailure
            ? Result<FormularioApresentadoDoProcessoDto>.Failure(doCandidato.Error!)
            : await FormularioDaVersaoDaInscricaoAsync(
                query, doCandidato.Value!, certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
                .ConfigureAwait(false);
    }

    private static async Task<Result<Inscricao>> InscricaoDoCandidatoAsync(
        ObterFormularioDaInscricaoQuery query,
        IInscricaoRepository inscricaoRepository,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        Result<Cpf> cpf = CpfDoToken(userContext);
        if (cpf.IsFailure)
        {
            return Result<Inscricao>.Failure(cpf.Error!);
        }

        Inscricao? inscricao = await inscricaoRepository
            .ObterAtivaPorProcessoECpfAsync(query.ProcessoSeletivoId, cpf.Value!, cancellationToken)
            .ConfigureAwait(false);
        return inscricao is null
            ? Result<Inscricao>.Failure(Inscricao.NaoEncontrada)
            : DoDonoDoToken(inscricao, userContext);
    }

    private static Result<Inscricao> DoDonoDoToken(Inscricao inscricao, IUserContext userContext) =>
        inscricao.Titular.ConferirDono(userContext.UserId ?? string.Empty) is { } erroDono
            ? Result<Inscricao>.Failure(erroDono)
            : Result<Inscricao>.Success(inscricao);

    private static Result<Cpf> CpfDoToken(IUserContext userContext)
    {
        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<Cpf>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode ler o formulário da inscrição."));
        }

        Result<Cpf> cpf = Cpf.Criar(cpfDoToken);
        return cpf.IsFailure
            ? Result<Cpf>.Failure(new DomainError("Inscricao.CpfAusenteNoToken", "O claim cpf do token não é um CPF válido."))
            : cpf;
    }

    private static async Task<Result<FormularioApresentadoDoProcessoDto>> FormularioDaVersaoDaInscricaoAsync(
        ObterFormularioDaInscricaoQuery query,
        Inscricao inscricao,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        Result<VersaoConfiguracao> resolvida = await new VersaoDaInscricaoResolver(certameDivulgadoRepository, processoSeletivoRepository, registroCodecs).ResolverAsync(inscricao, cancellationToken)
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
