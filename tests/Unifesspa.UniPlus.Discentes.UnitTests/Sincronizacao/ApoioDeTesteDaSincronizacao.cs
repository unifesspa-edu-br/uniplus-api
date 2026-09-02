namespace Unifesspa.UniPlus.Discentes.UnitTests.Sincronizacao;

using System.Runtime.CompilerServices;

using Unifesspa.UniPlus.Discentes.Application.Abstractions;
using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Enums;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sigaa.Contracts;
using Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;
using Unifesspa.UniPlus.Discentes.UnitTests.Sigaa;

/// <summary>
/// Origem de vínculos controlada pelo teste, no lugar da API do SIGAA.
/// </summary>
/// <remarks>
/// Distingue as duas varreduras pelo recorte pedido, que é justamente o que o orquestrador
/// decide: a primeira recorta por idade do vínculo, a segunda por situação.
/// </remarks>
internal sealed class OrigemSimulada : ISigaaVinculoDiscenteClient
{
    private VinculoDiscentePayload[] _porIngresso = [];
    private VinculoDiscentePayload[] _porSituacao = [];

    /// <summary>Recortes que o orquestrador pediu, na ordem.</summary>
    public List<FiltroDeVinculos> FiltrosPedidos { get; } = [];

    /// <summary>Faz a varredura por idade do vínculo falhar.</summary>
    public bool FalharNaPrimeiraVarredura { get; init; }

    /// <summary>Faz a varredura por situação falhar, depois da primeira ter entregue.</summary>
    public bool FalharNaSegundaVarredura { get; set; }

    /// <summary>Entrega uma página e falha ao buscar a seguinte, com o lote ainda incompleto.</summary>
    public bool FalharAoBuscarPaginaSeguinte { get; init; }

    public void ResponderPorIngresso(params VinculoDiscentePayload[] vinculos) =>
        _porIngresso = vinculos;

    public void ResponderPorSituacao(params VinculoDiscentePayload[] vinculos) =>
        _porSituacao = vinculos;

    public Task<PaginaDeVinculos> ObterPaginaAsync(
        FiltroDeVinculos filtro,
        int pagina,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("O orquestrador percorre as páginas; não pede uma isolada.");

    public async IAsyncEnumerable<PaginaDeVinculos> PercorrerAsync(
        FiltroDeVinculos filtro,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        FiltrosPedidos.Add(filtro);

        bool porIdade = filtro.AnoIngressoMinimo is not null;

        if ((porIdade && FalharNaPrimeiraVarredura) || (!porIdade && FalharNaSegundaVarredura))
        {
            throw new InvalidOperationException("Origem indisponível no meio da varredura.");
        }

        VinculoDiscentePayload[] itens = porIdade ? _porIngresso : _porSituacao;

        if (porIdade && FalharAoBuscarPaginaSeguinte)
        {
            await Task.Yield();
            yield return new PaginaDeVinculos(itens, null, TemProximaPagina: true);

            throw new InvalidOperationException("Origem indisponível ao buscar a página seguinte.");
        }

        await Task.Yield();
        yield return new PaginaDeVinculos(itens, itens.Length, TemProximaPagina: false);
    }
}

/// <summary>
/// Gravador controlado pelo teste, no lugar do banco.
/// </summary>
internal sealed class GravadorSimulado : IGravadorDeVinculos
{
    /// <summary>Índice do lote que deve falhar, quando o teste quer exercitar isso.</summary>
    public int? FalharNoLoteDeIndice { get; init; }

    /// <summary>Faz todo vínculo passar por já existente e igual.</summary>
    public bool TratarTudoComoInalterado { get; init; }

    /// <summary>Tamanho de cada lote recebido, na ordem.</summary>
    public List<int> TamanhosDosLotes { get; } = [];

    /// <summary>Identificadores de origem que chegaram para gravação.</summary>
    public List<long> IdentificadoresGravados { get; } = [];

    /// <summary>Quantos vínculos do lote que falha já estavam iguais na réplica.</summary>
    public int InalteradosNoLoteQueFalha { get; init; }

    public Task<DesfechoDaGravacao> GravarAsync(
        IReadOnlyList<VinculoSincronizavel> lote,
        CancellationToken cancellationToken = default)
    {
        TamanhosDosLotes.Add(lote.Count);

        if (FalharNoLoteDeIndice == TamanhosDosLotes.Count - 1)
        {
            return Task.FromResult(new DesfechoDaGravacao(
                new ResultadoDaGravacao(0, 0, InalteradosNoLoteQueFalha),
                new InvalidOperationException("Falha simulada na gravação do lote.")));
        }

        IdentificadoresGravados.AddRange(lote.Select(v => v.Vinculo.Snapshot.IdDiscenteSigaa));

        return Task.FromResult(new DesfechoDaGravacao(TratarTudoComoInalterado
            ? new ResultadoDaGravacao(0, 0, lote.Count)
            : new ResultadoDaGravacao(lote.Count, 0, 0)));
    }
}

/// <summary>Origem que não responde, para exercitar o caminho de falha.</summary>
internal sealed class OrigemQueFalha : ISigaaVinculoDiscenteClient
{
    public Task<PaginaDeVinculos> ObterPaginaAsync(
        FiltroDeVinculos filtro,
        int pagina,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Origem indisponível.");

    public IAsyncEnumerable<PaginaDeVinculos> PercorrerAsync(
        FiltroDeVinculos filtro,
        CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException("Origem indisponível.");
}

/// <summary>Guarda as execuções em memória.</summary>
internal sealed class ExecucoesEmMemoria : IRegistroDeExecucoes
{
    public List<SyncRun> Registradas { get; } = [];

    public Task<Guid> IniciarAsync(DateOnly dataDeReferencia, CancellationToken cancellationToken = default)
    {
        SyncRun execucao = SyncRun.Iniciar(
            dataDeReferencia,
            new RelogioControlado(new DateTimeOffset(2026, 9, 2, 3, 0, 0, TimeSpan.Zero)));

        Registradas.Add(execucao);
        return Task.FromResult(execucao.Id);
    }

    public Task ConcluirAsync(
        Guid execucaoId,
        ContagensDaExecucao contagens,
        SyncRunStatus situacao,
        CancellationToken cancellationToken = default)
    {
        SyncRun execucao = Registradas.Find(e => e.Id == execucaoId)
            ?? throw new InvalidOperationException("Execução não registrada.");

        execucao.Concluir(
            contagens,
            situacao,
            new RelogioControlado(new DateTimeOffset(2026, 9, 2, 3, 5, 0, TimeSpan.Zero)));

        return Task.CompletedTask;
    }
}
