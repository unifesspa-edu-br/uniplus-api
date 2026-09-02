namespace Unifesspa.UniPlus.Discentes.Domain.Entities;

/// <summary>
/// Um vínculo pronto para entrar na réplica, acompanhado do resumo do conteúdo que veio
/// da origem.
/// </summary>
/// <param name="Vinculo">O vínculo como o domínio o entende.</param>
/// <param name="ResumoDoConteudo">
/// Resumo dos campos trazidos da origem. É o que permite reconhecer que o vínculo não
/// mudou desde a última execução e poupar a escrita — numa sincronização diária de
/// dezenas de milhares de linhas, a esmagadora maioria não muda de um dia para o outro.
/// </param>
public sealed record VinculoSincronizavel(VinculoDiscente Vinculo, string ResumoDoConteudo);

/// <summary>
/// O que a gravação de um lote produziu.
/// </summary>
/// <param name="Inseridos">Vínculos que ainda não existiam na réplica.</param>
/// <param name="Atualizados">Vínculos que já existiam e cujo conteúdo mudou.</param>
/// <param name="Inalterados">
/// Vínculos que já existiam com o mesmo conteúdo e por isso não foram reescritos.
/// </param>
public sealed record ResultadoDaGravacao(int Inseridos, int Atualizados, int Inalterados)
{
    /// <summary>Quantos vínculos do lote foram efetivamente escritos.</summary>
    public int Escritos => Inseridos + Atualizados;
}
