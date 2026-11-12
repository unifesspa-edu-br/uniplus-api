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

    /// <summary>A sensibilidade é pessoal ou sensível, e o Representante do controlador também aprova.</summary>
    public bool ExigeAprovacaoDeProtecaoDeDados { get; }

    /// <summary>Papel de governança do perfil; nulo nos perfis comuns.</summary>
    public TipoDeGovernanca? Governanca { get; }

    public bool AdmitePrazo => Entities.Perfil.AdmitePrazoCom(Governanca);

    /// <summary>O perfil está ativo: o perfil desativado não aceita decisão que o conceda.</summary>
    public bool Ativo { get; }

    public PerfilAvaliado(
        IEnumerable<string> permissoes,
        string sensibilidade,
        bool exigeAprovacaoDeProtecaoDeDados,
        TipoDeGovernanca? governanca,
        bool ativo = true)
    {
        ArgumentNullException.ThrowIfNull(permissoes);
        ArgumentException.ThrowIfNullOrWhiteSpace(sensibilidade);

        Permissoes = [.. permissoes.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)];
        Sensibilidade = sensibilidade;
        ExigeAprovacaoDeProtecaoDeDados = exigeAprovacaoDeProtecaoDeDados;
        Governanca = governanca;
        Ativo = ativo;
    }
}

/// <summary>O que o handler sabe do mundo e o agregado precisa para decidir a aprovação.</summary>
/// <param name="HaRepresentanteVigente">
/// Alguém tem hoje concessão vigente do perfil de Representante do controlador, lido sob a trava do papel.
/// </param>
public sealed record ContextoDaAprovacao(bool HaRepresentanteVigente);

/// <summary>Os motivos pelos quais a segunda aprovação deixa de ser exigida, gravados na decisão do gestor.</summary>
public static class MotivosDeDispensa
{
    /// <summary>Designação do primeiro Representante do controlador: não há outro que possa aprová-la (ADR-0162).</summary>
    public const string DesignacaoDeProtecaoDeDados = "designacao_de_protecao_de_dados";
}
