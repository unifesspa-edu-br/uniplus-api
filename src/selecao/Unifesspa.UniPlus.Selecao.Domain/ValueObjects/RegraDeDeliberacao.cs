namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>
/// O que o edital declara sobre a composição de uma banca e a forma como ela delibera. Nenhuma
/// regra de deliberação é fixa no sistema: cada banca declarada carrega a sua. Na deliberação colegiada, a votação vale para
/// toda decisão. No ato individual, a banca pode declarar ao lado a votação usada só no julgamento do recurso: quórum, critério
/// e encerramento juntos, ou nenhum.
/// </summary>
/// <param name="Modo">Votação do colegiado ou ato individual.</param>
/// <param name="Integrantes">Titulares da banca, no mínimo 1.</param>
/// <param name="Suplentes">Suplentes da banca, no mínimo 0.</param>
/// <param name="Quorum">De 1 ao número de integrantes; na votação do colegiado e na votação do recurso do ato individual.</param>
/// <param name="Criterio">O que basta para a votação decidir; na votação do colegiado e na votação do recurso do ato individual.</param>
/// <param name="Desempate">Obrigatório nos critérios de maioria com mais de 1 integrante.</param>
/// <param name="Encerramento">Quando a votação se encerra; na votação do colegiado e na votação do recurso do ato individual.</param>
/// <param name="SemUnanimidade">Obrigatório na unanimidade com mais de 1 integrante.</param>
public sealed record DeclaracaoDaDeliberacao(
    ModoDeDeliberacao Modo,
    int Integrantes,
    int Suplentes,
    int? Quorum,
    CriterioDeVotacao? Criterio,
    DesempateDaVotacao? Desempate,
    EncerramentoDaVotacao? Encerramento,
    ResultadoSemUnanimidade? SemUnanimidade);

/// <summary>A regra de deliberação de uma banca, congelada na publicação junto com a banca.</summary>
public sealed class RegraDeDeliberacao
{
    public ModoDeDeliberacao Modo { get; private set; }

    public int Integrantes { get; private set; }

    public int Suplentes { get; private set; }

    public int? Quorum { get; private set; }

    public CriterioDeVotacao? Criterio { get; private set; }

    public DesempateDaVotacao? Desempate { get; private set; }

    public EncerramentoDaVotacao? Encerramento { get; private set; }

    public ResultadoSemUnanimidade? SemUnanimidade { get; private set; }

    /// <summary>Quantos titulares precisam atuar: o quórum da votação, ou um titular quando não há votação.</summary>
    public int QuorumExigido => Quorum ?? 1;

    /// <summary>
    /// Se a banca decide por ato individual sem declarar a votação usada no julgamento do recurso: quórum, critério e
    /// encerramento juntos.
    /// </summary>
    public bool AtoIndividualSemVotacao =>
        Modo == ModoDeDeliberacao.AtoIndividual && !(Quorum.HasValue && Criterio.HasValue && Encerramento.HasValue);

    private RegraDeDeliberacao() { }

    private RegraDeDeliberacao(DeclaracaoDaDeliberacao declarada)
    {
        Modo = declarada.Modo;
        Integrantes = declarada.Integrantes;
        Suplentes = declarada.Suplentes;
        Quorum = declarada.Quorum;
        Criterio = declarada.Criterio;
        Desempate = declarada.Desempate;
        Encerramento = declarada.Encerramento;
        SemUnanimidade = declarada.SemUnanimidade;
    }

    /// <summary>Acumula, campo a campo, tudo o que a declaração viola.</summary>
    public static Result<RegraDeDeliberacao> Criar(DeclaracaoDaDeliberacao declarada)
    {
        ArgumentNullException.ThrowIfNull(declarada);

        List<FieldError> violacoes = ValidacaoDaDeliberacao.Violacoes(declarada);
        return violacoes.Count > 0
            ? Result<RegraDeDeliberacao>.ValidationFailure(violacoes)
            : Result<RegraDeDeliberacao>.Success(new RegraDeDeliberacao(declarada));
    }

    /// <summary>A declaração que originou esta regra, para a comparação e para a reposição.</summary>
    public DeclaracaoDaDeliberacao Declaracao() =>
        new(Modo, Integrantes, Suplentes, Quorum, Criterio, Desempate, Encerramento, SemUnanimidade);
}
