namespace Unifesspa.UniPlus.Selecao.Application.Services;

using Domain.Entities;
using Domain.ProcessosSeletivos.Cronograma;
using Domain.ValueObjects;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Lê do cadastro a precedência entre fases como a conferência do cronograma a recebe: o domínio não lê reader (ADR-0042). O
/// cadastro não guarda vigência nem histórico, e a leitura é a do estado vigente no momento dela, uma vez por operação, dentro da
/// transação da operação e depois do bloqueio do processo.
/// </summary>
public sealed class LeituraDasPrecedenciasDosCadastros(IPrecedenciaFaseReader leitor)
{
    /// <summary>As relações vivas do cadastro.</summary>
    internal async Task<IReadOnlyList<ArestaPrecedencia>> ListarVivasAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<PrecedenciaFaseView> vivas = await leitor.ListarVivasAsync(cancellationToken).ConfigureAwait(false);

        return [.. vivas.Select(static aresta => new ArestaPrecedencia(aresta.AntecessoraCodigo, aresta.SucessoraCodigo, aresta.PermiteSobreposicao))];
    }

    /// <summary>
    /// A precedência que a operação confere: lida quando o cronograma atual precisa ser conferido contra o cadastro
    /// (<see cref="ReconferenciaDaPrecedencia"/>), e <see cref="PrecedenciasDosCadastros.NaoLidas"/> quando não precisa.
    /// </summary>
    internal async Task<PrecedenciasDosCadastros> LerAsync(
        IReadOnlyCollection<FaseCronograma>? fasesDaVersaoPublicada, IReadOnlyCollection<FaseCronograma> fasesAtuais, CancellationToken cancellationToken) =>
        ReconferenciaDaPrecedencia.Exigida(fasesDaVersaoPublicada, fasesAtuais)
            ? PrecedenciasDosCadastros.Lidas(await ListarVivasAsync(cancellationToken).ConfigureAwait(false))
            : PrecedenciasDosCadastros.NaoLidas;
}
