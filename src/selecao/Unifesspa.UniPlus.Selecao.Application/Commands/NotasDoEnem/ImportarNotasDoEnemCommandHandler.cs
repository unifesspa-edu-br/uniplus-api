namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using System.Security.Cryptography;

using Abstractions;

using Domain.Entities;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Leiautes;
using Domain.Services;
using Domain.ValueObjects;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Microsoft.Extensions.Logging;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Importa o retorno do INEP: confere o arquivo contra o leiaute, cruza as linhas com as inscrições da
/// edição e grava um lote novo, que passa a valer por inteiro no lugar do anterior. Reimportar o
/// arquivo do lote vigente devolve o lote existente.
/// </summary>
/// <remarks>
/// <para>A ordem importa. O Wolverine chama <c>SaveChangesAsync</c> também quando o handler devolve
/// falha, então tudo o que pode recusar o arquivo acontece antes de rastrear qualquer entidade.</para>
/// <para>O índice cego de cada CPF é uma chamada ao cofre de chaves, lenta e sem relação com o banco;
/// por isso é calculado antes da trava da linha do processo, que bloquearia publicações e retificações
/// pelo tempo todo. Dentro da trava só há consulta, cruzamento em memória e inserção.</para>
/// <para>Nada que venha do arquivo (CPF, nota, valor de célula) entra em log ou mensagem de erro.</para>
/// </remarks>
public static partial class ImportarNotasDoEnemCommandHandler
{
    public static async Task<Result<ImportacaoDeNotasDto>> Handle(
        ImportarNotasDoEnemCommand command,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IInscritosDaEdicaoDoEnem inscritos,
        IIndiceCegoDoCpf indiceCegoDoCpf,
        ILoteDeNotasDoEnemRepository lotes,
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
        ArgumentNullException.ThrowIfNull(indiceCegoDoCpf);
        ArgumentNullException.ThrowIfNull(lotes);
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
            return Result<ImportacaoDeNotasDto>.Failure(escopo.Error!);
        }

        Result<ILeiauteDeArquivoDoInep> leiaute = ResolvedorDoEscopoDeNotasDoEnem
            .ObterLeiaute(command.LeiauteCodigo, command.LeiauteVersao, TipoDeLeiauteDoInep.Retorno);
        if (leiaute.IsFailure)
        {
            return Result<ImportacaoDeNotasDto>.Failure(leiaute.Error!);
        }

        if (command.Conteudo.Length == 0)
        {
            return Result<ImportacaoDeNotasDto>.Failure(RecusasDoArquivoDoInep.Vazio.Error!);
        }

        if (command.Conteudo.LongLength > LimitesDoArquivoDoInep.TamanhoMaximoEmBytes)
        {
            return Result<ImportacaoDeNotasDto>.Failure(RecusasDoArquivoDoInep.GrandeDemais.Error!);
        }

        LeiauteDeRetornoDoInep leiauteDeRetorno = (LeiauteDeRetornoDoInep)leiaute.Value!;
        Result<ArquivoDeRetornoLido> lido = LeitorDeArquivoDoInep.Ler(command.Conteudo, leiauteDeRetorno);
        if (lido.IsFailure)
        {
            return Result<ImportacaoDeNotasDto>.Failure(lido.Error!);
        }

        string arquivoHash = Convert.ToHexStringLower(SHA256.HashData(command.Conteudo));
        IReadOnlyList<LinhaDeRetorno> linhas = lido.Value!.Linhas;

        IReadOnlyDictionary<string, IndiceCegoDoCpf> indicePorCpf = await indiceCegoDoCpf
            .CalcularAsync([.. linhas.Select(static l => l.Cpf).Distinct(StringComparer.Ordinal)], cancellationToken)
            .ConfigureAwait(false);

        // Serializa as importações do processo: o lote vigente é lido depois da trava.
        await processoSeletivoRepository.TravarAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);

        LoteDeNotasDoEnem? vigente = await lotes
            .ObterVigenteAsync(command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, cancellationToken)
            .ConfigureAwait(false);
        if (VigenciaDoLote.ArquivoJaVigente(vigente, arquivoHash))
        {
            LoteDeNotasDoEnem existente = (await lotes
                .ObterComOcorrenciasAsync(command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, vigente!.Id, cancellationToken)
                .ConfigureAwait(false))!;
            LogLoteJaVigente(logger, existente.Id, command.ProcessoSeletivoId);

            return Result<ImportacaoDeNotasDto>.Success(new ImportacaoDeNotasDto(
                LoteDeNotasDoEnemDto.De(existente, vigente: true, await NumerosAsync(lotes, existente, cancellationToken).ConfigureAwait(false)),
                Criado: false));
        }

        IReadOnlyList<InscritoDaEdicao> inscritosDaEdicao = await inscritos
            .ListarAsync(command.ProcessoSeletivoId, command.Edicao, cancellationToken)
            .ConfigureAwait(false);

        ResultadoDoCruzamento cruzamento = CruzamentoDasNotasComInscritos.Cruzar(linhas, indicePorCpf, inscritosDaEdicao);

        LoteDeNotasDoEnem lote = LoteDeNotasDoEnem.Registrar(
            command.ProcessoSeletivoId,
            command.EtapaProcessoId,
            command.Edicao,
            leiauteDeRetorno.Codigo,
            leiauteDeRetorno.Versao,
            leiauteDeRetorno.Hash,
            arquivoHash,
            userContext.UserId ?? string.Empty,
            timeProvider.GetUtcNow(),
            linhas.Count,
            escopo.Value!.VersaoConfiguracaoId,
            cruzamento,
            []);
        await lotes.AdicionarAsync(lote, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        LogLoteImportado(
            logger, lote.Id, command.ProcessoSeletivoId, lote.TotalDeLinhas, lote.TotalDeInscritosComNota,
            lote.TotalDeInscritosSemLinha, lote.TotalDeLinhasSemInscrito, lote.TotalDeInscritosEmConflito);

        return Result<ImportacaoDeNotasDto>.Success(new ImportacaoDeNotasDto(
            LoteDeNotasDoEnemDto.De(lote, vigente: true, await NumerosAsync(lotes, lote, cancellationToken).ConfigureAwait(false)),
            Criado: true));
    }

    internal static Task<IReadOnlyDictionary<Guid, string>> NumerosAsync(
        ILoteDeNotasDoEnemRepository lotes, LoteDeNotasDoEnem lote, CancellationToken cancellationToken) =>
        lotes.ObterNumerosDeInscricaoAsync(
            [.. lote.Ocorrencias.Where(static o => o.InscricaoId is not null).Select(static o => o.InscricaoId!.Value).Distinct()],
            cancellationToken);

    [LoggerMessage(Level = LogLevel.Information, Message = "Lote {LoteId} de notas do ENEM importado no processo {ProcessoId}: {Linhas} linhas, {ComNota} com nota, {SemLinha} inscritos sem linha, {SemInscrito} linhas sem inscrito, {Conflitos} em conflito")]
    private static partial void LogLoteImportado(
        ILogger logger, Guid loteId, Guid processoId, int linhas, int comNota, int semLinha, int semInscrito, int conflitos);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arquivo já é o do lote vigente {LoteId} no processo {ProcessoId}; nenhum lote novo foi criado")]
    private static partial void LogLoteJaVigente(ILogger logger, Guid loteId, Guid processoId);
}
