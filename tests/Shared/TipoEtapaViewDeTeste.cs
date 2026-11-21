namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Contracts;

/// <summary>
/// Monta a vista do tipo de etapa que o Seleção lê do cadastro: admite pontuação e eliminação, não tem nota de origem no
/// ENEM, não exige correção independente e não admite conceito, salvo o que o teste diz. Cada método devolve um construtor
/// novo, então um ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record TipoEtapaViewDeTeste
{
    private Guid Id { get; init; }

    private string Codigo { get; init; } = string.Empty;

    private string Nome { get; init; } = "Etapa";

    private bool AdmitePontuacao { get; init; } = true;

    private bool AdmiteEliminacao { get; init; } = true;

    private bool NotaDeOrigemNoEnem { get; init; }

    private bool ExigeCorrecaoIndependente { get; init; }

    public static TipoEtapaViewDeTeste Com(Guid id, string codigo) => new() { Id = id, Codigo = codigo };

    public TipoEtapaViewDeTeste Chamado(string nome) => this with { Nome = nome };

    public TipoEtapaViewDeTeste ComCarater(bool admitePontuacao, bool admiteEliminacao) =>
        this with { AdmitePontuacao = admitePontuacao, AdmiteEliminacao = admiteEliminacao };

    public TipoEtapaViewDeTeste ComNotaDoEnem(bool tem = true) => this with { NotaDeOrigemNoEnem = tem };

    public TipoEtapaViewDeTeste ComCorrecaoIndependente(bool exige = true) => this with { ExigeCorrecaoIndependente = exige };

    public TipoEtapaView Construir() => new(
        Id, Codigo, Nome, null, new AvaliacaoDasEtapasView(AdmitePontuacao, AdmiteEliminacao, NotaDeOrigemNoEnem, ExigeCorrecaoIndependente, []));
}
