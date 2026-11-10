namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>
/// O que provoca a reavaliação da retomada, com a referência de cada caso: a decisão que acabou de ser gravada, a versão
/// que entrou em vigor ou a permissão que mudou de classe.
/// </summary>
public abstract record OrigemDaReavaliacao
{
    private OrigemDaReavaliacao()
    {
    }

    /// <summary>A decisão de uma etapa da retomada.</summary>
    public sealed record PelaDecisao(Guid DecisaoId) : OrigemDaReavaliacao;

    /// <summary>A entrada em vigor de uma versão, pela aprovação, pela remoção ou pela migração.</summary>
    public sealed record PelaTrocaDeVigencia(Guid VersaoQueEntrouId) : OrigemDaReavaliacao;

    /// <summary>A reclassificação de uma permissão do catálogo.</summary>
    public sealed record PelaReclassificacao(string PermissaoCodigo) : OrigemDaReavaliacao;
}
