namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using System.Text.RegularExpressions;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Leiautes;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Gera o arquivo de envio ao INEP e registra a geração. Os CPFs vão só no arquivo devolvido: o
/// registro guarda quem, quando, a edição, o leiaute e o total. O arquivo lista os CPFs em ordem
/// crescente, para que a mesma edição gere o mesmo arquivo.
/// </summary>
public static partial class GerarEnvioAoInepCommandHandler
{
    public static async Task<Result<EnvioAoInepGeradoDto>> Handle(
        GerarEnvioAoInepCommand command,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IInscritosDaEdicaoDoEnem inscritos,
        IGeracaoDeEnvioAoInepRepository geracoes,
        ISelecaoUnitOfWork unitOfWork,
        IUserContext userContext,
        TimeProvider timeProvider,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(inscritos);
        ArgumentNullException.ThrowIfNull(geracoes);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(userContext);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        Result<EscopoDeNotasDoEnem> escopo = await ResolvedorDoEscopoDeNotasDoEnem
            .ResolverAsync(
                command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, exigeNotaDoEnem: true,
                certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (escopo.IsFailure)
        {
            return Result<EnvioAoInepGeradoDto>.Failure(escopo.Error!);
        }

        Result<ILeiauteDeArquivoDoInep> leiaute = ResolvedorDoEscopoDeNotasDoEnem
            .ObterLeiaute(command.LeiauteCodigo, command.LeiauteVersao, TipoDeLeiauteDoInep.Envio);
        if (leiaute.IsFailure)
        {
            return Result<EnvioAoInepGeradoDto>.Failure(leiaute.Error!);
        }

        IReadOnlyList<string> cpfs = await inscritos
            .ListarCpfsAsync(command.ProcessoSeletivoId, command.Edicao, cancellationToken)
            .ConfigureAwait(false);
        if (cpfs.Count == 0)
        {
            return Result<EnvioAoInepGeradoDto>.Failure(new DomainError(
                NotasDoEnemErrorCodes.EdicaoSemInscritos,
                "Nenhuma inscrição enviada escolheu esta edição, então não há CPF a enviar ao INEP."));
        }

        byte[] arquivo = EscritorDeArquivoDeEnvio.Escrever(
            (LeiauteDeEnvioDoInep)leiaute.Value!, [.. cpfs.Order(StringComparer.Ordinal)]);

        GeracaoDeEnvioAoInep geracao = GeracaoDeEnvioAoInep.Registrar(
            command.ProcessoSeletivoId,
            command.EtapaProcessoId,
            command.Edicao,
            leiaute.Value!.Codigo,
            leiaute.Value.Versao,
            leiaute.Value.Hash,
            userContext.UserId ?? string.Empty,
            timeProvider.GetUtcNow(),
            cpfs.Count,
            userContext.NomeCompleto);
        await geracoes.AdicionarAsync(geracao, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        LogEnvioGerado(logger, geracao.Id, command.ProcessoSeletivoId, geracao.TotalDeCpfs);

        return Result<EnvioAoInepGeradoDto>.Success(
            new EnvioAoInepGeradoDto(arquivo, NomeDoArquivo(command.Edicao), geracao.Id, geracao.TotalDeCpfs));
    }

    private static string NomeDoArquivo(string edicao) => $"envio-inep-enem-{NaoSeguro().Replace(edicao, "_")}.txt";

    [GeneratedRegex("[^A-Za-z0-9_-]")]
    private static partial Regex NaoSeguro();

    [LoggerMessage(Level = LogLevel.Information, Message = "Envio ao INEP {GeracaoId} gerado para o processo {ProcessoId} com {TotalDeCpfs} CPFs")]
    private static partial void LogEnvioGerado(ILogger logger, Guid geracaoId, Guid processoId, int totalDeCpfs);
}
