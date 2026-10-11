namespace Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Kernel.Domain.ValueObjects;
using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Inicia a inscrição (issue #1858) — ou devolve a que já existe, idempotente por construção, não
/// por chave de idempotência: o dono é o CPF do token, não um valor que o cliente fornece.
/// </summary>
/// <remarks>
/// <para>
/// <b>A versão do formulário é fixada aqui, da divulgação — nunca do relógio</b> (UNI-REQ-0084),
/// no mesmo caminho de <c>ObterFormularioRenderizavelQueryHandler</c>: a divulgação aponta o ato
/// que confirmou a publicidade, e é a versão que ele congelou — não a mais nova, que sob
/// retificação pendente ainda não tem publicidade nenhuma.
/// </para>
/// <para>
/// <b>Dois "iniciar" simultâneos do mesmo CPF:</b> a checagem em memória (buscar antes de criar)
/// resolve o caso comum, mas duas requisições que leem "não existe" ao mesmo tempo passam as duas
/// — e é o índice único quem decide. A perdedora não vira 500: descarta o rastreamento e relê a
/// inscrição que a vencedora gravou.
/// </para>
/// </remarks>
public static class IniciarInscricaoCommandHandler
{
    public static async Task<Result<InscricaoDto>> Handle(
        IniciarInscricaoCommand command,
        IInscricaoRepository inscricaoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IResolvedorFusoInstitucional resolvedorFuso,
        TimeProvider timeProvider,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(inscricaoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(resolvedorFuso);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);

        if (userContext.Cpf is not { } cpfDoToken)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O token não traz o claim cpf — só um candidato autenticado com CPF pode se inscrever."));
        }

        Result<Cpf> cpfResult = Cpf.Criar(cpfDoToken);
        if (cpfResult.IsFailure)
        {
            return Result<InscricaoDto>.Failure(new DomainError(
                "Inscricao.CpfAusenteNoToken",
                "O claim cpf do token não é um CPF válido."));
        }

        Cpf cpf = cpfResult.Value!;

        Inscricao? existente = await inscricaoRepository
            .ObterPorProcessoECpfAsync(command.ProcessoSeletivoId, cpf, cancellationToken)
            .ConfigureAwait(false);
        if (existente is not null)
        {
            return Result<InscricaoDto>.Success(InscricaoDto.De(existente));
        }

        Result<Services.VersaoDivulgada> versaoDivulgada = await Services.VersaoDivulgadaResolver
            .ResolverAsync(command.ProcessoSeletivoId, certameDivulgadoRepository, processoSeletivoRepository, cancellationToken)
            .ConfigureAwait(false);
        if (versaoDivulgada.IsFailure)
        {
            return Result<InscricaoDto>.Failure(versaoDivulgada.Error!);
        }

        // A inscrição já existente é devolvida acima mesmo fora do período: devolvê-la é leitura.
        if (Services.PeriodoDaInscricaoResolver.Conferir(versaoDivulgada.Value!.Versao, registroCodecs, resolvedorFuso, timeProvider.GetUtcNow()) is { } foraDoPeriodo)
        {
            return Result<InscricaoDto>.Failure(foraDoPeriodo);
        }

        Result<Inscricao> iniciar = Inscricao.Iniciar(
            command.ProcessoSeletivoId, cpf, userContext.UserId ?? string.Empty, userContext.Issuer ?? string.Empty,
            versaoDivulgada.Value!.Versao.Id);
        if (iniciar.IsFailure)
        {
            return Result<InscricaoDto>.Failure(iniciar.Error!);
        }

        Inscricao inscricao = iniciar.Value!;
        await inscricaoRepository.AdicionarAsync(inscricao, cancellationToken).ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) == InscricaoConstraintViolation.ProcessoCpfConstraint)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();

            Inscricao? vencedora = await inscricaoRepository
                .ObterPorProcessoECpfAsync(command.ProcessoSeletivoId, cpf, cancellationToken)
                .ConfigureAwait(false);
            if (vencedora is null)
            {
                // A corrida só dá este erro porque alguém inseriu a linha que colidiu — se a
                // releitura não a encontra, o estado é inconsistente, não "não encontrado".
                return Result<InscricaoDto>.Failure(new DomainError(
                    "Inscricao.CorridaInconsistente",
                    "A inscrição colidiu no índice único, mas não foi possível relê-la."));
            }

            return Result<InscricaoDto>.Success(InscricaoDto.De(vencedora));
        }

        return Result<InscricaoDto>.Success(InscricaoDto.De(inscricao));
    }
}
