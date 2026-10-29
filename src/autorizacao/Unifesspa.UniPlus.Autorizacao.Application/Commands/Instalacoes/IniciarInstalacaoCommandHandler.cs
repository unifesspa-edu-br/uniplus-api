namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do passo 1. A recusa por já haver gestor sai antes de tudo: com gestor ativo nenhuma instalação
/// vale, e corrigir o resto não leva a nada. A URL de PUT sai antes de gravar: se o armazenamento falhar, nada é
/// gravado, nem o cancelamento da pendência anterior, que vai na mesma gravação da nova.
/// </summary>
public static class IniciarInstalacaoCommandHandler
{
    public const string TipoDeMidiaDoPdf = "application/pdf";

    /// <summary>Validade da URL de PUT; o PDF de até 10 MB sobe dentro dela.</summary>
    public static readonly TimeSpan PrazoDoEnvio = TimeSpan.FromMinutes(15);

    public static async Task<Result<InstalacaoIniciadaDto>> Handle(
        IniciarInstalacaoCommand command,
        IInstalacaoRepository instalacaoRepository,
        IConcessaoRepository concessaoRepository,
        IArmazenamentoDosDocumentosDaAutorizacao armazenamento,
        IDominiosInstitucionais dominios,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(instalacaoRepository);
        ArgumentNullException.ThrowIfNull(concessaoRepository);
        ArgumentNullException.ThrowIfNull(armazenamento);
        ArgumentNullException.ThrowIfNull(dominios);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);

        if (IdentidadeDoToken.De(userContext) is not { } executor)
        {
            return Result<InstalacaoIniciadaDto>.Failure(InstalacaoHandlerErros.AutorNaoIdentificado());
        }

        // O e-mail do gestor é o que a revogação da concessão pede de volta: o que não é do domínio institucional não vale.
        if (command.Gestores.Any(g => PedidoDeConcessao.EmailNormalizado(g.Email) is { } email
            && !dominios.Contem(email[(email.LastIndexOf('@') + 1)..])))
        {
            return Result<InstalacaoIniciadaDto>.ValidationFailure([new FieldError("gestores", new DomainError(
                InstalacaoErrorCodes.GestoresInvalidos,
                "O e-mail de cada gestor deve ser institucional."))]);
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();

        // A conferência definitiva é a da confirmação, sob a trava de governança; aqui só se poupa o trabalho
        // do documento quando já há gestor.
        if (await concessaoRepository
            .ContarGestoresAtivosAsync(PerfisDeSistema.GestorDeAcessosId, Guid.Empty, agora, cancellationToken)
            .ConfigureAwait(false) > 0)
        {
            return Result<InstalacaoIniciadaDto>.Failure(InstalacaoHandlerErros.JaHaGestor());
        }

        // O emissor aceito é o do próprio token, que a validação já confrontou com o emissor configurado.
        Result<Instalacao> iniciada = Instalacao.Iniciar(
            executor, command.Gestores, executor.Emissor, command.NumeroDoDocumento, command.TamanhoDoPdf, agora);
        if (iniciada.IsFailure)
        {
            return Result<InstalacaoIniciadaDto>.ValidationFailure(iniciada.Errors);
        }

        Instalacao instalacao = iniciada.Value!;
        string url = await armazenamento
            .GerarUrlDeEnvioAsync(instalacao.ChaveDeEnvio, TipoDeMidiaDoPdf, PrazoDoEnvio, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            // Cancelamento e nova pendência vão numa única gravação: se a segunda falhar, a anterior não fica
            // cancelada sozinha. O índice único parcial admite uma pendente por executor.
            Instalacao? anterior = await instalacaoRepository.ObterPendenteDoExecutorAsync(executor, cancellationToken).ConfigureAwait(false);
            if (anterior is not null)
            {
                anterior.Cancelar(agora);
            }

            await instalacaoRepository.AdicionarAsync(instalacao, cancellationToken).ConfigureAwait(false);
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (InstalacaoViolacoes.EhPendenciaDuplicada(ex) || ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Duas iniciações simultâneas do mesmo executor: a que perdeu a corrida recomeça, em vez de 500.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result<InstalacaoIniciadaDto>.Failure(InstalacaoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result<InstalacaoIniciadaDto>.Success(new InstalacaoIniciadaDto(
            instalacao.Id,
            Domain.Enums.SituacaoDaInstalacaoCodigo.AguardandoDocumento,
            new Uri(url, UriKind.Absolute),
            TipoDeMidiaDoPdf,
            agora + PrazoDoEnvio));
    }
}
