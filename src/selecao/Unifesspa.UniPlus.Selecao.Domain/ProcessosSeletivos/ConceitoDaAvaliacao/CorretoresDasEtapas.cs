namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConceitoDaAvaliacao;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Quantos corretores tem cada regra de consolidação referenciada pelas etapas do processo: os distintos e os que ela
/// põe em campo, que incluem o adicional quando a regra declara o novo corretor para o critério que não converge. O número é dado da versão da
/// regra no rol, congelado pela referência <c>(codigo, versao, hash)</c>, e chega pronto da
/// Application: o agregado guarda só a referência, e ler o rol é I/O (ADR-0042).
/// </summary>
/// <remarks>
/// Só a etapa que declara regra de consolidação tem corretores, e o número da regra dela precisa ter sido lido: a
/// ausência da leitura não vale zero, porque um zero por omissão aceitaria uma banca menor do que a regra exige. Quem
/// calcula o mínimo pergunta antes se a regra da etapa foi <see cref="Resolvida"/>, e recusa a que não foi.
/// </remarks>
public sealed record CorretoresDasEtapas
{
    private readonly Dictionary<ReferenciaRegra, CorretoresDaRegra> _porRegra;

    /// <param name="porRegra">
    /// Os corretores lidos do rol para cada regra de consolidação que as etapas referenciam e que o rol confirma; a
    /// regra ausente do rol, ou cujo hash diverge do que ele guarda, fica de fora.
    /// </param>
    public CorretoresDasEtapas(IReadOnlyDictionary<ReferenciaRegra, CorretoresDaRegra> porRegra)
    {
        ArgumentNullException.ThrowIfNull(porRegra);
        _porRegra = new Dictionary<ReferenciaRegra, CorretoresDaRegra>(porRegra);
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
    /// Zero para a etapa sem regra de consolidação (objetiva, ou nota do ENEM: não há consolidação humana); os
    /// corretores distintos que a regra que a etapa referencia exige.
    /// </summary>
    /// <exception cref="InvalidOperationException">A etapa referencia regra cujo número não foi lido.</exception>
    public int Distintos(EtapaProcesso etapa) => Da(etapa)?.Distintos ?? 0;

    /// <summary>
    /// Zero para a etapa sem regra de consolidação; os corretores em campo da regra que a etapa referencia, que são os
    /// distintos e o adicional quando a regra declara o novo corretor.
    /// </summary>
    /// <exception cref="InvalidOperationException">A etapa referencia regra cujo número não foi lido.</exception>
    public int EmCampo(EtapaProcesso etapa) => Da(etapa)?.EmCampo ?? 0;

    // Quem pede o número confere antes Resolvida: nenhum número é presumido.
    private CorretoresDaRegra? Da(EtapaProcesso etapa)
    {
        ArgumentNullException.ThrowIfNull(etapa);

        if (etapa.RegraConsolidacao is null)
        {
            return null;
        }

        return _porRegra.TryGetValue(etapa.RegraConsolidacao, out CorretoresDaRegra? corretores)
            ? corretores
            : throw new InvalidOperationException(
                $"O número de corretores da regra de consolidação '{etapa.RegraConsolidacao.Codigo}' versão '{etapa.RegraConsolidacao.Versao}' não foi lido.");
    }
}

/// <summary>Os corretores de uma versão da regra de consolidação: os distintos e os que ela põe em campo.</summary>
/// <param name="Distintos">Os corretores que avaliam o mesmo item, declarados pela versão da regra.</param>
/// <param name="EmCampo">Os distintos e, quando a regra declara o novo corretor para o critério que não converge, o adicional.</param>
public sealed record CorretoresDaRegra(int Distintos, int EmCampo)
{
    /// <summary>A regra que não declara o novo corretor: em campo ficam só os distintos.</summary>
    public static CorretoresDaRegra SemNovoCorretor(int distintos) => new(distintos, distintos);
}
