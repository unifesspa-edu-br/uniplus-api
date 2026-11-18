namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Acl;

/// <summary>
/// Reconcilia a réplica de vínculos com o que o SIGAA tem hoje.
/// </summary>
/// <remarks>
/// <para>
/// A origem não sabe dizer "o que mudou desde ontem", então a reconciliação lê o conjunto
/// inteiro que interessa e compara. O que interessa são duas coisas: os vínculos criados
/// nos últimos anos, e os vínculos ainda em andamento — sem limite de idade, porque quem
/// ingressou há doze anos e ainda está matriculado continua sendo aluno.
/// </para>
/// <para>
/// As duas varreduras se sobrepõem bastante, e é por isso que são unidas <b>pelo
/// identificador do vínculo na origem</b> antes de qualquer gravação. Unir por CPF seria
/// errado: a mesma pessoa pode ter mais de um vínculo, e reduzi-los a um apagaria
/// justamente a informação que o módulo existe para guardar.
/// </para>
/// </remarks>
public sealed partial class OrquestradorDeSincronizacao
{
    private readonly ISigaaVinculoDiscenteClient _origem;
    private readonly IGravadorDeVinculos _gravador;
    private readonly SincronizacaoOptions _opcoes;
    private readonly ILogger<OrquestradorDeSincronizacao> _logger;

    public OrquestradorDeSincronizacao(
        ISigaaVinculoDiscenteClient origem,
        IGravadorDeVinculos gravador,
        IOptions<SincronizacaoOptions> opcoes,
        ILogger<OrquestradorDeSincronizacao> logger)
    {
        ArgumentNullException.ThrowIfNull(origem);
        ArgumentNullException.ThrowIfNull(gravador);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(logger);

        _origem = origem;
        _gravador = gravador;
        _opcoes = opcoes.Value;
        _logger = logger;
    }

    /// <summary>
    /// Executa uma reconciliação completa e devolve o que aconteceu.
    /// </summary>
    /// <param name="dataDeReferencia">
    /// A data que esta execução representa. O limite de ingresso sai dela, e não do relógio:
    /// uma execução do dia 31 de dezembro que só for processada no dia 1º de janeiro — fila
    /// atrasada, nova tentativa — avançaria o limite em um ano e deixaria de fora os vínculos
    /// do ano da virada, enquanto o registro da execução continuaria afirmando representar o
    /// dia 31. O conjunto consultado e a data registrada precisam ser o mesmo filtro.
    /// </param>
    /// <remarks>
    /// Uma falha na leitura não descarta as contagens, e vale para cancelamento também: o
    /// que já foi gravado continua gravado, e precisa aparecer no registro da execução.
    /// Descartá-las deixaria a réplica com escritas que o registro diz não terem acontecido.
    /// </remarks>
    public async Task<ResumoDaSincronizacao> ExecutarAsync(
        DateOnly dataDeReferencia,
        CancellationToken cancellationToken)
    {
        ContagemDaSincronizacao contagem = new();

        try
        {
            await ReconciliarOsFiltrosAsync(dataDeReferencia, contagem, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception excecao)
        {
            contagem.RegistrarInterrupcao(excecao);
            LogLeituraInterrompida(_logger, excecao);
        }

        return contagem.Fechar();
    }

    /// <summary>
    /// Reconcilia os dois filtros, um depois do outro.
    /// </summary>
    /// <remarks>
    /// A união acontece aqui, na memória do processo, e não no banco: guardar para depois
    /// descobrir que o vínculo já tinha vindo na outra varredura custaria uma escrita a mais
    /// por vínculo repetido — e a sobreposição entre as duas é grande.
    /// </remarks>
    private async Task ReconciliarOsFiltrosAsync(
        DateOnly dataDeReferencia,
        ContagemDaSincronizacao contagem,
        CancellationToken cancellationToken)
    {
        HashSet<long> jaProcessados = [];

        foreach (FiltroDeVinculos filtro in FiltrosDaExecucao(dataDeReferencia))
        {
            await ReconciliarAsync(filtro, jaProcessados, contagem, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Os dois filtros da execução: os vínculos de ingresso recente e os vínculos em
    /// andamento, sem limite de idade.
    /// </summary>
    private IEnumerable<FiltroDeVinculos> FiltrosDaExecucao(DateOnly dataDeReferencia)
    {
        int anoLimiteDeIngresso = dataDeReferencia.Year - _opcoes.AnosDeIngressoConsiderados;

        yield return new FiltroDeVinculos(_opcoes.NivelDeEnsino, AnoIngressoMinimo: anoLimiteDeIngresso);
        yield return new FiltroDeVinculos(_opcoes.NivelDeEnsino, Situacoes: _opcoes.SituacoesEmAndamento);
    }

    /// <summary>
    /// Reconcilia um filtro.
    /// </summary>
    /// <remarks>
    /// Se a leitura parar no meio, o que já foi decodificado continua válido. Uma página é
    /// menor que um lote, então uma falha no meio da varredura deixaria pendentes os
    /// vínculos de várias páginas já lidas — descartá-los faria a execução relê-los amanhã
    /// sem necessidade, e contá-los como lidos e não aproveitados.
    /// </remarks>
    private async Task ReconciliarAsync(
        FiltroDeVinculos filtro,
        HashSet<long> jaProcessados,
        ContagemDaSincronizacao contagem,
        CancellationToken cancellationToken)
    {
        List<VinculoSincronizavel> lote = new(_opcoes.TamanhoDoLote);

        try
        {
            await PercorrerEGravarAsync(filtro, jaProcessados, contagem, lote, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            await GravarPendentesAsync(lote, contagem, cancellationToken).ConfigureAwait(false);
            throw;
        }

        await GravarPendentesAsync(lote, contagem, cancellationToken).ConfigureAwait(false);
    }

    private async Task PercorrerEGravarAsync(
        FiltroDeVinculos filtro,
        HashSet<long> jaProcessados,
        ContagemDaSincronizacao contagem,
        List<VinculoSincronizavel> lote,
        CancellationToken cancellationToken)
    {
        await foreach (PaginaDeVinculos pagina in
            _origem.PercorrerAsync(filtro, cancellationToken).ConfigureAwait(false))
        {
            ResultadoDaDecodificacao decodificada = DecodificadorDeVinculos.Decodificar(pagina.Itens);

            contagem.RegistrarLidos(pagina.Itens.Count);
            contagem.RegistrarDescartes(decodificada.Descartados);

            foreach (VinculoDecodificado item in decodificada.Aceitos)
            {
                await AcumularAsync(item, jaProcessados, contagem, lote, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Põe o vínculo no lote, a menos que ele já tenha vindo na outra varredura — as duas
    /// se sobrepõem por construção — e grava o lote quando ele enche.
    /// </summary>
    private async Task AcumularAsync(
        VinculoDecodificado item,
        HashSet<long> jaProcessados,
        ContagemDaSincronizacao contagem,
        List<VinculoSincronizavel> lote,
        CancellationToken cancellationToken)
    {
        if (!jaProcessados.Add(item.Vinculo.DadosCopiados.Identificacao.IdDiscenteSigaa))
        {
            contagem.RegistrarRepetido();
            return;
        }

        lote.Add(new VinculoSincronizavel(item.Vinculo, item.ResumoDoConteudo));

        if (lote.Count >= _opcoes.TamanhoDoLote)
        {
            await GravarAsync(lote, contagem, cancellationToken).ConfigureAwait(false);
            lote.Clear();
        }
    }

    private async Task GravarPendentesAsync(
        List<VinculoSincronizavel> lote,
        ContagemDaSincronizacao contagem,
        CancellationToken cancellationToken)
    {
        if (lote.Count > 0)
        {
            await GravarAsync(lote, contagem, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Grava um lote e contabiliza o desfecho.
    /// </summary>
    /// <remarks>
    /// A falha de um lote não interrompe a reconciliação: os lotes já gravados continuam
    /// gravados, os seguintes continuam sendo tentados, e a execução termina marcada como
    /// parcial. Interromper deixaria a réplica num estado tão incompleto quanto, sem a
    /// vantagem de ter aproveitado o que dava.
    /// </remarks>
    private async Task GravarAsync(
        List<VinculoSincronizavel> lote,
        ContagemDaSincronizacao contagem,
        CancellationToken cancellationToken)
    {
        DesfechoDaGravacao desfecho = await _gravador
            .GravarAsync(lote, cancellationToken)
            .ConfigureAwait(false);

        contagem.RegistrarGravacao(desfecho.Classificacao);

        if (desfecho.Falha is { } falha)
        {
            int naoGravados = lote.Count - desfecho.Classificacao.Inalterados;
            contagem.RegistrarLoteComFalha(naoGravados);
            LogLoteFalhou(_logger, naoGravados, falha);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Lote de sincronização de discentes falhou. VinculosNoLote={VinculosNoLote}")]
    private static partial void LogLoteFalhou(ILogger logger, int vinculosNoLote, Exception excecao);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Leitura dos vínculos na origem foi interrompida; o que já foi gravado permanece.")]
    private static partial void LogLeituraInterrompida(ILogger logger, Exception excecao);
}
