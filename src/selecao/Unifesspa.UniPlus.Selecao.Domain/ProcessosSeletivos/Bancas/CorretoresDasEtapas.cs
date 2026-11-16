namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quantos corretores distintos exige cada regra de consolidação referenciada pelas etapas do processo. O número é
/// dado da versão da regra no rol, congelado pela referência <c>(codigo, versao, hash)</c>, e chega pronto da
/// Application: o agregado guarda só a referência, e ler o rol é I/O (ADR-0042).
/// </summary>
/// <remarks>
/// Só a etapa que declara regra de consolidação tem corretores, e o número da regra dela precisa ter sido lido: a
/// ausência da leitura não vale zero, porque um zero por omissão aceitaria uma banca menor do que a regra exige. Quem
/// calcula o mínimo pergunta antes se a regra da etapa foi <see cref="Resolvida"/>, e recusa a que não foi.
/// </remarks>
public sealed record CorretoresDasEtapas
{
    private readonly Dictionary<ReferenciaRegra, int> _porRegra;

    /// <param name="porRegra">
    /// O número lido do rol para cada regra de consolidação que as etapas referenciam e que o rol confirma; a regra
    /// ausente do rol, ou cujo hash diverge do que ele guarda, fica de fora.
    /// </param>
    public CorretoresDasEtapas(IReadOnlyDictionary<ReferenciaRegra, int> porRegra)
    {
        ArgumentNullException.ThrowIfNull(porRegra);
        _porRegra = new Dictionary<ReferenciaRegra, int>(porRegra);
    }

    /// <summary>
    /// Se a etapa tem resposta: a que não declara regra de consolidação sempre tem, e a que declara tem quando o número
    /// da regra dela foi lido do rol.
    /// </summary>
    public bool Resolvida(EtapaProcesso etapa)
    {
        ArgumentNullException.ThrowIfNull(etapa);

        return etapa.RegraConsolidacao is null || _porRegra.ContainsKey(etapa.RegraConsolidacao);
    }

    /// <summary>
    /// Zero para a etapa sem regra de consolidação (objetiva, ou nota do ENEM: não há consolidação humana); o número
    /// lido para a regra que a etapa referencia.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// A etapa referencia regra cujo número não foi lido: quem pede o número confere antes <see cref="Resolvida"/>, e
    /// nenhum número é presumido.
    /// </exception>
    public int De(EtapaProcesso etapa)
    {
        ArgumentNullException.ThrowIfNull(etapa);

        if (etapa.RegraConsolidacao is null)
        {
            return 0;
        }

        return _porRegra.TryGetValue(etapa.RegraConsolidacao, out int corretores)
            ? corretores
            : throw new InvalidOperationException(
                $"O número de corretores da regra de consolidação '{etapa.RegraConsolidacao.Codigo}' versão '{etapa.RegraConsolidacao.Versao}' não foi lido.");
    }
}
