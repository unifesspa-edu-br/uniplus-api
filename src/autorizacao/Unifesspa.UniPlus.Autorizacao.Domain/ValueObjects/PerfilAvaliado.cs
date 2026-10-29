namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// O perfil como o aprovador o avalia, no instante da decisão: as permissões que ele reúne e a
/// sensibilidade que o catálogo atribui a elas. É o retrato do que foi decidido, não uma cópia
/// viva do perfil, e o domínio não conhece o catálogo.
/// </summary>
public sealed record PerfilAvaliado
{
    public IReadOnlyList<string> Permissoes { get; }

    /// <summary>Valor canônico da sensibilidade mais alta entre as permissões do perfil.</summary>
    public string Sensibilidade { get; }

    /// <summary>A sensibilidade é pessoal ou sensível, e a encarregada de dados também aprova.</summary>
    public bool ExigeEncarregada { get; }

    /// <summary>Papel de governança do perfil; nulo nos perfis comuns.</summary>
    public TipoDeGovernanca? Governanca { get; }

    /// <summary>O perfil está ativo: o perfil desativado não aceita decisão que o conceda.</summary>
    public bool Ativo { get; }

    public PerfilAvaliado(
        IEnumerable<string> permissoes,
        string sensibilidade,
        bool exigeEncarregada,
        TipoDeGovernanca? governanca,
        bool ativo = true)
    {
        ArgumentNullException.ThrowIfNull(permissoes);
        ArgumentException.ThrowIfNullOrWhiteSpace(sensibilidade);

        Permissoes = [.. permissoes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Sensibilidade = sensibilidade;
        ExigeEncarregada = exigeEncarregada;
        Governanca = governanca;
        Ativo = ativo;
    }
}

/// <summary>O que o handler sabe do mundo e o agregado precisa para decidir a aprovação.</summary>
/// <param name="BeneficiarioJaEEncarregada">O beneficiário tem hoje concessão ativa do perfil da encarregada de dados.</param>
public sealed record ContextoDaAprovacao(bool BeneficiarioJaEEncarregada);

/// <summary>Os motivos pelos quais a segunda aprovação deixa de ser exigida, gravados na decisão do gestor.</summary>
public static class MotivosDeDispensa
{
    /// <summary>Não há encarregada que possa aprovar a própria designação.</summary>
    public const string DesignacaoDaEncarregada = "designacao_da_encarregada";

    /// <summary>O beneficiário já é a encarregada de dados: a aprovação do gestor conclui o pedido (ADR-0152).</summary>
    public const string BeneficiariaJaEEncarregada = "beneficiaria_ja_e_encarregada";
}
