namespace Unifesspa.UniPlus.Testes.Compartilhado;

using Unifesspa.UniPlus.Configuracao.Application.Commands.TiposEtapa;

/// <summary>
/// Monta a avaliação das etapas de um tipo no pedido de gravação dos testes: admite pontuação e eliminação, não exige
/// correção independente e não admite conceito, salvo o que o teste diz. Cada método devolve um construtor novo, então um
/// ponto de partida compartilhado não vaza para os demais.
/// </summary>
internal sealed record AvaliacaoDasEtapasDeTeste
{
    private bool? AdmitePontuacao { get; init; } = true;

    private bool? AdmiteEliminacao { get; init; } = true;

    private bool? ExigeCorrecaoIndependente { get; init; } = false;

    private IReadOnlyList<string>? ConceitosAdmitidos { get; init; } = [];

    public static AvaliacaoDasEtapasDeTeste Padrao() => new();

    public AvaliacaoDasEtapasDeTeste ComPontuacao(bool? admite) => this with { AdmitePontuacao = admite };

    public AvaliacaoDasEtapasDeTeste ComEliminacao(bool? admite) => this with { AdmiteEliminacao = admite };

    public AvaliacaoDasEtapasDeTeste ComCorrecaoIndependente(bool? exige) => this with { ExigeCorrecaoIndependente = exige };

    public AvaliacaoDasEtapasDeTeste ComConceitos(params string[]? codigos) => this with { ConceitosAdmitidos = codigos };

    public AvaliacaoDasEtapasInput Construir() => new(AdmitePontuacao, AdmiteEliminacao, ExigeCorrecaoIndependente, ConceitosAdmitidos);
}
