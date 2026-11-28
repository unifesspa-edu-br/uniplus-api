namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;

internal sealed class CadastrosDaClassificacao(IRegraCatalogoReader regraCatalogoReader, IPesoAreaEnemReader pesoAreaEnemReader)
{
    /// <summary>
    /// Consulta a fórmula da nota, a precisão, a ordem de alocação e as eliminações sem parar na primeira recusa: cada uma
    /// é independente das outras, e o operador corrige tudo de uma vez (ADR-0125).
    /// </summary>
    public async Task<RegrasCitadasNaClassificacao> ResolverRegrasAsync(DefinirClassificacaoCommand command, CancellationToken cancellationToken)
    {
        Result<ReferenciaRegra>? calculo = await ResolverRegraAsync(command.RegraCalculoCodigo, command.RegraCalculoVersao,
            TipoRegra.RegraCalculo, "regraCalculoCodigo", cancellationToken).ConfigureAwait(false);
        Result<ReferenciaRegra>? arredondamento = await ResolverRegraAsync(command.RegraArredondamentoCodigo, command.RegraArredondamentoVersao,
            TipoRegra.RegraArredondamento, "regraArredondamentoCodigo", cancellationToken).ConfigureAwait(false);
        Result<ReferenciaRegra>? ordem = await ResolverRegraAsync(command.RegraOrdemAlocacaoCodigo, command.RegraOrdemAlocacaoVersao,
            TipoRegra.RegraOrdemAlocacao, "regraOrdemAlocacaoCodigo", cancellationToken).ConfigureAwait(false);
        EliminacoesResolvidas eliminacoes = await ResolverEliminacoesAsync(command, cancellationToken).ConfigureAwait(false);

        return new(
            Resolvida(calculo),
            Resolvida(arredondamento),
            Resolvida(ordem),
            [.. calculo?.Errors ?? [], .. arredondamento?.Errors ?? [], .. ordem?.Errors ?? []],
            eliminacoes);
    }

    /// <summary>
    /// Consulta apenas quando a fórmula da nota foi confirmada e a combinação aceita a resolução; as demais combinações são
    /// conferidas pelo domínio.
    /// </summary>
    public async Task<QuadroDePesosResolvido> ResolverQuadroAsync(
        ReferenciaRegra? regraCalculo, DefinirClassificacaoCommand command, CancellationToken cancellationToken) =>
        regraCalculo is not null
            && command.BaseadoEmEnem is { } baseadoEmEnem
            && ConfiguracaoClassificacao.ExigeQuadroPesoAreaEnem(regraCalculo, baseadoEmEnem)
            && !string.IsNullOrWhiteSpace(command.ResolucaoPesoAreaEnem)
            ? await QuadroDePesosResolvido.DaResolucaoAsync(command.ResolucaoPesoAreaEnem, pesoAreaEnemReader, cancellationToken).ConfigureAwait(false)
            : QuadroDePesosResolvido.SemConsulta(command.ResolucaoPesoAreaEnem);

    private async Task<Result<ReferenciaRegra>?> ResolverRegraAsync(
        string? codigo, string? versao, TipoRegra tipoEsperado, string campo, CancellationToken cancellationToken)
    {
        if (codigo is null || versao is null)
        {
            return null;
        }

        RegraCatalogo? regra = await regraCatalogoReader.ObterAsync(codigo, versao, cancellationToken).ConfigureAwait(false);
        return regra switch
        {
            null => Recusa(campo, "ConfiguracaoClassificacao.RegraNaoEncontrada", "Regra não encontrada no catálogo de regras."),
            { Tipo: var tipo } when tipo != tipoEsperado => Recusa(campo, "ConfiguracaoClassificacao.RegraTipoInvalido", "A regra não é do tipo esperado."),
            _ => Result<ReferenciaRegra>.Success(regra.Referencia()),
        };
    }

    private static ReferenciaRegra? Resolvida(Result<ReferenciaRegra>? regra) => regra is { IsSuccess: true } ? regra.Value : null;

    private static Result<ReferenciaRegra> Recusa(string campo, string codigo, string mensagem) =>
        Result<ReferenciaRegra>.ValidationFailure([new(campo, new DomainError(codigo, mensagem))]);

    // No primeiro item recusado a lista para: os seguintes ficam sem posição montada.
    private async Task<EliminacoesResolvidas> ResolverEliminacoesAsync(DefinirClassificacaoCommand command, CancellationToken cancellationToken)
    {
        if (command.RegrasEliminacao is not { } entradas)
        {
            return EliminacoesResolvidas.NaoInformadas();
        }

        EliminacoesResolvidas resolvidas = EliminacoesResolvidas.ParaQuantidade(entradas.Count);
        for (int posicao = 0; posicao < entradas.Count && resolvidas.SemRecusa; posicao++)
        {
            RegraCatalogo? regra = await regraCatalogoReader.ObterAsync(
                entradas[posicao].RegraCodigo, entradas[posicao].RegraVersao, cancellationToken).ConfigureAwait(false);
            resolvidas.Acrescentar(posicao, regra, entradas[posicao]);
        }

        return resolvidas;
    }
}
