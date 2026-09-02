namespace Unifesspa.UniPlus.Discentes.Infrastructure.Sincronizacao;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Discentes.Application.Abstractions;
using Unifesspa.UniPlus.Discentes.Domain.Entities;
using Unifesspa.UniPlus.Discentes.Domain.Interfaces;

/// <summary>
/// Grava um lote de vínculos e o confirma.
/// </summary>
/// <remarks>
/// Existe como porta para que o orquestrador seja exercitável sem banco: ele decide o que
/// gravar, a implementação decide como.
/// </remarks>
public interface IGravadorDeVinculos
{
    Task<DesfechoDaGravacao> GravarAsync(
        IReadOnlyList<VinculoSincronizavel> lote,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// O que aconteceu com um lote.
/// </summary>
/// <param name="Classificacao">
/// Como os vínculos do lote foram classificados antes de gravar. Vale mesmo quando a
/// gravação falha: os que já estavam iguais na réplica continuam corretos lá, e contá-los
/// como não gravados faria o registro da execução subestimar o que ela alcançou.
/// </param>
/// <param name="Falha">O que impediu a gravação, quando foi o caso.</param>
public sealed record DesfechoDaGravacao(ResultadoDaGravacao Classificacao, Exception? Falha = null);

/// <summary>
/// Grava cada lote em escopo próprio, com confirmação independente.
/// </summary>
/// <remarks>
/// <para>
/// O escopo por lote é o que sustenta a promessa de que uma execução interrompida não
/// desfaz o que já foi gravado. Reaproveitar o escopo de quem chama deixaria essa garantia
/// dependendo de não haver transação abrangendo a sincronização inteira — e basta uma
/// política de mensageria mudar para que dezenas de milhares de vínculos confirmados
/// sumam numa falha no último lote.
/// </para>
/// <para>
/// O escopo próprio também isola o rastreamento de mudanças: um lote que falha não deixa
/// entidades pendentes no contexto do lote seguinte.
/// </para>
/// </remarks>
internal sealed class GravadorDeVinculos : IGravadorDeVinculos
{
    private readonly IServiceScopeFactory _escopos;

    public GravadorDeVinculos(IServiceScopeFactory escopos)
    {
        ArgumentNullException.ThrowIfNull(escopos);
        _escopos = escopos;
    }

    public async Task<DesfechoDaGravacao> GravarAsync(
        IReadOnlyList<VinculoSincronizavel> lote,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope escopo = _escopos.CreateAsyncScope();

        IVinculoDiscenteRepository repositorio =
            escopo.ServiceProvider.GetRequiredService<IVinculoDiscenteRepository>();
        IDiscentesUnitOfWork unidadeDeTrabalho =
            escopo.ServiceProvider.GetRequiredService<IDiscentesUnitOfWork>();

        // Preparar o lote também pode falhar — cifrar o CPF de um vínculo alterado é o caso
        // mais provável —, e essa falha precisa do mesmo tratamento por lote que a da
        // confirmação. Fora do try, ela subiria até o orquestrador e encerraria a varredura,
        // deixando de tentar todos os lotes seguintes por causa de um único vínculo.
        ResultadoDaGravacao classificacao = new(0, 0, 0);

        try
        {
            classificacao = await repositorio
                .GravarLoteAsync(lote, cancellationToken)
                .ConfigureAwait(false);

            await unidadeDeTrabalho.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (FalhaAoPrepararLoteException falha)
        {
            // A preparação parou no meio, mas o que ela já havia reconhecido como igual
            // continua valendo — a réplica não foi tocada nesses vínculos.
            return new DesfechoDaGravacao(
                falha.Parcial with { Inseridos = 0, Atualizados = 0 },
                falha.InnerException ?? falha);
        }
        catch (Exception excecao) when (excecao is not OperationCanceledException)
        {
            // A classificação sobe junto com a falha: o que já estava igual na réplica
            // continua correto lá, e contá-lo como não gravado subestimaria a execução.
            // Quando a falha é da preparação, ela ainda é a inicial e nada foi aproveitado.
            return new DesfechoDaGravacao(
                classificacao with { Inseridos = 0, Atualizados = 0 },
                excecao);
        }

        return new DesfechoDaGravacao(classificacao);
    }
}
