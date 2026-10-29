namespace Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;

using System.Security.Cryptography;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do passo 4. Todas as recusas de código têm o mesmo código de erro e a mesma mensagem, para não revelar se
/// há instalação pendente. A tentativa é gravada na mesma transação que a decide, inclusive quando a resposta é
/// recusa — a recusa é <see cref="Result"/>, e a transação confirma. O código certo concede os dois gestores na
/// mesma transação: qualquer falha reverte tudo.
/// </summary>
public static partial class ConfirmarInstalacaoCommandHandler
{
    public static async Task<Result> Handle(
        ConfirmarInstalacaoCommand command,
        IInstalacaoRepository instalacaoRepository,
        IConcessaoRepository concessaoRepository,
        IResumoDeCodigoDeConfirmacao resumo,
        ICatalogoDePermissoesConsultavel catalogo,
        IAutorizacaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(instalacaoRepository);
        ArgumentNullException.ThrowIfNull(concessaoRepository);
        ArgumentNullException.ThrowIfNull(resumo);
        ArgumentNullException.ThrowIfNull(catalogo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        if (IdentidadeDoToken.De(userContext) is not { } autor)
        {
            return Result.Failure(InstalacaoHandlerErros.AutorNaoIdentificado());
        }

        DateTimeOffset agora = timeProvider.GetUtcNow();

        Instalacao? instalacao = await instalacaoRepository
            .ObterAguardandoCodigoDoExecutorAsync(autor, cancellationToken).ConfigureAwait(false);
        if (instalacao is null)
        {
            // Não toca a pendência alheia: quem tenta sem ter instalação aguardando código não consome as tentativas de
            // ninguém. O fato fica registrado e logado com o sub de quem tentou.
            LogTentativaSemPendencia(logger, autor.Sub);
            return await RecusarAsync(
                instalacaoRepository, unitOfWork, null, autor, ResultadoDaTentativaDeInstalacao.SemPendencia, agora, cancellationToken)
                .ConfigureAwait(false);
        }

        bool codigoConfere = await ConferirAsync(instalacao, command.Codigo, resumo, cancellationToken).ConfigureAwait(false);
        ResultadoDaTentativaDeInstalacao resultado = instalacao.RegistrarTentativa(codigoConfere, agora);
        if (resultado != ResultadoDaTentativaDeInstalacao.Aceita)
        {
            return await RecusarAsync(instalacaoRepository, unitOfWork, instalacao.Id, autor, resultado, agora, cancellationToken)
                .ConfigureAwait(false);
        }

        // A contagem roda sob a trava, depois que ela é obtida: duas confirmações simultâneas de instalações
        // diferentes não podem passar as duas.
        await concessaoRepository.TravarGovernancaParaEscritaAsync(cancellationToken).ConfigureAwait(false);
        if (await concessaoRepository
            .ContarGestoresAtivosAsync(PerfisDeSistema.GestorDeAcessosId, Guid.Empty, agora, cancellationToken)
            .ConfigureAwait(false) > 0)
        {
            // Outra instalação venceu a corrida: esta não tem mais como valer, e o código certo não a mantém pendente.
            // A tentativa não é "aceita": nada foi confirmado.
            instalacao.Cancelar(agora);
            await instalacaoRepository.AdicionarTentativaAsync(
                TentativaDeInstalacao.Registrar(instalacao.Id, autor, ResultadoDaTentativaDeInstalacao.JaHaviaGestor, agora), cancellationToken)
                .ConfigureAwait(false);

            try
            {
                await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
            {
                // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119). Os gestores já estão
                // instalados, e a resposta é a mesma.
                unitOfWork.DescartarAlteracoesNaoSalvas();
            }

            return Result.Failure(InstalacaoHandlerErros.JaHaGestor());
        }

        Perfil? perfil = await concessaoRepository
            .ObterParaConcederAsync(PerfisDeSistema.GestorDeAcessosId, cancellationToken).ConfigureAwait(false);
        if (perfil is null)
        {
            throw new InvalidOperationException("O perfil de sistema do gestor de acessos não existe: a carga inicial não rodou.");
        }

        instalacao.Confirmar(agora);

        OrigemDaConcessao origem = new(TipoDeOrigem.Instalacao, instalacao.Id);
        List<Concessao> concessoes = [];
        List<FieldError> erros = [];
        foreach (GestorDaInstalacao gestor in instalacao.Gestores)
        {
            Result<Concessao> concessao = Concessao.Conceder(
                new Identidade(gestor.Emissor, gestor.Sub, gestor.Nome), perfil, EscopoDaConcessao.DaInstituicao(), origem, autor, agora, catalogo,
                beneficiarioEmail: gestor.Email);
            if (concessao.IsFailure)
            {
                erros.AddRange(concessao.Errors);
                continue;
            }

            concessoes.Add(concessao.Value!);
        }

        if (erros.Count > 0)
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.ValidationFailure(erros);
        }

        foreach (Concessao concessao in concessoes)
        {
            await concessaoRepository.AdicionarAsync(concessao, cancellationToken).ConfigureAwait(false);
        }

        await instalacaoRepository.AdicionarTentativaAsync(
            TentativaDeInstalacao.Registrar(instalacao.Id, autor, ResultadoDaTentativaDeInstalacao.Aceita, agora), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ConcessaoViolacoes.EhConcessaoDuplicada(ex))
        {
            // A concessão já existe para um dos gestores ou a origem já foi processada: os gestores já estão
            // instalados, e nada desta confirmação é gravado.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(InstalacaoHandlerErros.JaHaGestor());
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // Com Idempotency-Key o handler captura e descarta o rastreamento (ADR-0119).
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(InstalacaoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result.Success();
    }

    // Comparação em tempo constante. O código sem a forma de código nem chega ao cálculo do resumo.
    private static async Task<bool> ConferirAsync(
        Instalacao instalacao, string? codigo, IResumoDeCodigoDeConfirmacao resumo, CancellationToken cancellationToken)
    {
        string informado = codigo?.Trim() ?? string.Empty;
        if (!CodigoDeConfirmacao.TemAForma(informado) || instalacao.CodigoResumo is not { } gravado)
        {
            return false;
        }

        byte[] calculado = await resumo.ResumirAsync(instalacao.Id, informado, cancellationToken).ConfigureAwait(false);

        return CryptographicOperations.FixedTimeEquals(calculado, gravado);
    }

    private static async Task<Result> RecusarAsync(
        IInstalacaoRepository repository,
        IAutorizacaoUnitOfWork unitOfWork,
        Guid? instalacaoId,
        Identidade autor,
        ResultadoDaTentativaDeInstalacao resultado,
        DateTimeOffset agora,
        CancellationToken cancellationToken)
    {
        await repository.AdicionarTentativaAsync(TentativaDeInstalacao.Registrar(instalacaoId, autor, resultado, agora), cancellationToken)
            .ConfigureAwait(false);

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ViolacoesDeGravacao.EhConflitoDeConcorrencia(ex))
        {
            // A leitura trava a instalação, então só outra operação sobre a mesma linha fora desta trava chega aqui.
            unitOfWork.DescartarAlteracoesNaoSalvas();
            return Result.Failure(InstalacaoHandlerErros.ConflitoDeConcorrencia());
        }

        return Result.Failure(InstalacaoHandlerErros.CodigoInvalido());
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tentativa de confirmar a instalação sem instalação aguardando código: sub {Sub}")]
    private static partial void LogTentativaSemPendencia(ILogger logger, string sub);
}
