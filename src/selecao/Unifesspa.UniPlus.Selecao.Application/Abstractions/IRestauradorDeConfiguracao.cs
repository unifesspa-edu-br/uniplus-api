namespace Unifesspa.UniPlus.Selecao.Application.Abstractions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// Repõe no <see cref="ProcessoSeletivo"/> a configuração de uma
/// <see cref="VersaoConfiguracao"/> congelada — <b>e prova que repôs</b> (ADR-0110 D1/D2).
/// </summary>
/// <remarks>
/// <para>
/// <b>É uma operação única, e é assim de propósito.</b> Decodificar, repor e provar são
/// três passos que <b>não</b> podem ser oferecidos separadamente a um chamador: quem
/// repusesse sem provar teria feito exatamente o que a ADR proíbe — um descarte que
/// destrói configuração em silêncio. A raiz sabe repor um grafo e sabe recusar um grafo
/// incoerente, mas <b>não</b> sabe recanonicalizar (ADR-0042: o Domain não chama o
/// codec). Só aqui os dois lados existem ao mesmo tempo.
/// </para>
/// <para>
/// <b>A prova é o round-trip, e ela roda em produção — não só em teste.</b> Depois de
/// repor, o agregado é recanonicalizado com o encoder <b>daquela</b> versão e os bytes
/// têm de reproduzir os que estão congelados. Se um único campo se perdeu no caminho, os
/// bytes divergem e a operação <b>falha</b> — em vez de gravar uma configuração
/// empobrecida que ninguém mais teria como detectar.
/// </para>
/// <para>
/// <b>Falhar aqui não deixa resíduo.</b> A reposição é em memória e a transação é do
/// handler: uma prova que falha devolve <c>Failure</c> antes de qualquer
/// <c>SalvarAlteracoesAsync</c>, e o agregado mutado morre com o escopo. É o mesmo
/// contrato de toda regra de negócio recusada no projeto.
/// </para>
/// </remarks>
public interface IRestauradorDeConfiguracao
{
    /// <param name="processo">O agregado a repor — carregado com o grafo completo, e o mesmo a que a versão pertence.</param>
    /// <param name="versao">A versão congelada cuja configuração volta a valer.</param>
    Result Restaurar(ProcessoSeletivo processo, VersaoConfiguracao versao);
}
