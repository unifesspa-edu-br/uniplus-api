namespace Unifesspa.UniPlus.Selecao.Application.Commands.NotasDoEnem;

using System.Security.Cryptography;

using Abstractions;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.Leiautes;
using Domain.Services;
using Domain.ValueObjects;

using DTOs.NotasDoEnem;

using Kernel.Results;

using Microsoft.Extensions.Logging;

using Services;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;

/// <summary>
/// Importa o retorno do INEP: confere o arquivo contra o leiaute, cruza as linhas com as inscrições da
/// edição e grava um lote novo, que passa a valer por inteiro no lugar do anterior. Reimportar o
/// arquivo do lote vigente devolve o lote existente.
/// </summary>
/// <remarks>
/// <para>A ordem importa. O Wolverine chama <c>SaveChangesAsync</c> também quando o handler devolve
/// falha, então tudo o que pode recusar o arquivo ou o lote acontece antes de rastrear qualquer entidade.</para>
/// <para>O escopo é conferido antes de tudo, para não gastar o cofre com um pedido inválido. O índice
/// cego de cada CPF é uma chamada ao cofre de chaves, lenta e sem relação com o banco; por isso é
/// calculado antes da trava da linha do processo, que bloquearia publicações e retificações pelo tempo
/// todo. A trava serializa o processo contra a publicação: o edital divulgado é resolvido de novo
/// dentro dela, e a apuração e a vigência do lote usam essa resolução, não a primeira, que uma
/// retificação divulgada no intervalo teria deixado velha.</para>
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

        // Serializa as importações do processo e as publicações: o lote vigente e o edital divulgado
        // são lidos depois da trava.
        await processoSeletivoRepository.TravarAsync(command.ProcessoSeletivoId, cancellationToken).ConfigureAwait(false);

        Result<EscopoDeNotasDoEnem> escopoNaTrava = await ResolvedorDoEscopoDeNotasDoEnem
            .ResolverAsync(
                command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, exigeNotaDoEnem: true,
                certameDivulgadoRepository, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);
        if (escopoNaTrava.IsFailure)
        {
            return Result<ImportacaoDeNotasDto>.Failure(escopoNaTrava.Error!);
        }

        Guid versaoConfiguracaoId = escopoNaTrava.Value!.VersaoConfiguracaoId;

        LoteDeNotasDoEnem? vigente = await lotes
            .ObterVigenteAsync(command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, cancellationToken)
            .ConfigureAwait(false);
        if (VigenciaDoLote.ArquivoJaVigente(vigente, arquivoHash, versaoConfiguracaoId))
        {
            LoteDeNotasDoEnem existente = (await lotes
                .ObterComOcorrenciasAsync(command.ProcessoSeletivoId, command.EtapaProcessoId, command.Edicao, vigente!.Id, cancellationToken)
                .ConfigureAwait(false))!;
            LogLoteJaVigente(logger, existente.Id, command.ProcessoSeletivoId);

            return Result<ImportacaoDeNotasDto>.Success(new ImportacaoDeNotasDto(
                await DetalheAsync(lotes, existente, vigente: true, processoSeletivoRepository, registroCodecs, cancellationToken).ConfigureAwait(false),
                Criado: false));
        }

        IReadOnlyList<InscritoDaEdicao> inscritosDaEdicao = await inscritos
            .ListarAsync(command.ProcessoSeletivoId, command.Edicao, cancellationToken)
            .ConfigureAwait(false);

        EditalParaApuracaoDaNota edital = EditalParaApuracaoDaNota.De(escopoNaTrava.Value.Grafo);
        int comOfertaForaDoEdital = inscritosDaEdicao.Count(i => i.OfertasConcorridas.Any(o => !edital.GrupoPorOferta.ContainsKey(o)));
        if (comOfertaForaDoEdital > 0)
        {
            return Result<ImportacaoDeNotasDto>.Failure(new DomainError(
                NotasDoEnemErrorCodes.OfertaDeInscricaoForaDoEdital,
                $"{comOfertaForaDoEdital} inscrição(ões) da edição concorre(m) a um curso que não está no edital divulgado. Confira a retificação do edital e a situação dessas inscrições antes de importar."));
        }

        ResultadoDoCruzamento cruzamento = CruzamentoDasNotasComInscritos.Cruzar(linhas, indicePorCpf, inscritosDaEdicao);
        IReadOnlyList<AvaliacaoApurada> avaliacoes = ApuracaoDaNotaDoEnem.ApurarDoLote(
            cruzamento, inscritosDaEdicao, edital.GrupoPorOferta, edital.Quadro);

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
            versaoConfiguracaoId,
            cruzamento,
            avaliacoes,
            userContext.NomeCompleto);
        await lotes.AdicionarAsync(lote, cancellationToken).ConfigureAwait(false);
        await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);

        int apuradas = avaliacoes.Count(static a => a.Situacao == SituacaoDaAvaliacao.Apurada);
        LogLoteImportado(
            logger, lote.Id, command.ProcessoSeletivoId, lote.TotalDeLinhas, lote.TotalDeInscritosComNota,
            lote.TotalDeInscritosSemLinha, lote.TotalDeLinhasSemInscrito, lote.TotalDeInscritosEmConflito,
            apuradas, avaliacoes.Count - apuradas);

        return Result<ImportacaoDeNotasDto>.Success(new ImportacaoDeNotasDto(
            await DetalheAsync(lotes, lote, vigente: true, processoSeletivoRepository, registroCodecs, cancellationToken).ConfigureAwait(false),
            Criado: true));
    }

    /// <summary>
    /// O lote com as ocorrências e os ausentes. Número, nome e CPF mascarado de todos os inscritos citados
    /// saem de uma leitura por inscrição, não de uma por ocorrência.
    /// </summary>
    internal static async Task<LoteDeNotasDoEnemDto> DetalheAsync(
        ILoteDeNotasDoEnemRepository lotes,
        LoteDeNotasDoEnem lote,
        bool vigente,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> ausentes = await lotes
            .ObterAusentesDoLoteAsync(lote.Id, cancellationToken)
            .ConfigureAwait(false);

        Guid[] inscricaoIds =
        [
            .. lote.Ocorrencias.Where(static o => o.InscricaoId is not null).Select(static o => o.InscricaoId!.Value)
                .Concat(ausentes.Keys)
                .Distinct(),
        ];

        IReadOnlyDictionary<Guid, InscritoDoLote> inscritos = await lotes.ObterInscritosAsync(inscricaoIds, cancellationToken).ConfigureAwait(false);
        IReadOnlyDictionary<Guid, IReadOnlyList<OpcaoDeCursoDoLoteDto>> opcoes = await NomesDasOfertasDoLote
            .ResolverAsync(lote.ProcessoSeletivoId, inscritos, processoSeletivoRepository, registroCodecs, cancellationToken)
            .ConfigureAwait(false);

        return LoteDeNotasDoEnemDto.De(lote, vigente, new DetalheDoLoteDeNotas(inscritos, ausentes, opcoes), ausentes.Count);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Lote {LoteId} de notas do ENEM importado no processo {ProcessoId}: {Linhas} linhas, {ComNota} com nota, {SemLinha} inscritos sem linha, {SemInscrito} linhas sem inscrito, {Conflitos} em conflito; {Apuradas} avaliações apuradas e {SemNota} sem nota")]
    private static partial void LogLoteImportado(
        ILogger logger, Guid loteId, Guid processoId, int linhas, int comNota, int semLinha, int semInscrito, int conflitos, int apuradas, int semNota);

    [LoggerMessage(Level = LogLevel.Information, Message = "Arquivo já é o do lote vigente {LoteId} no processo {ProcessoId}; nenhum lote novo foi criado")]
    private static partial void LogLoteJaVigente(ILogger logger, Guid loteId, Guid processoId);
}
