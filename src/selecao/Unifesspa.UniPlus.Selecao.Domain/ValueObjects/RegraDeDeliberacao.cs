namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
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
/// <param name="Votacao">Campos declarados para a votação.</param>
public sealed record DeclaracaoDaDeliberacao(string? Modo, int Integrantes, int Suplentes, DeclaracaoDaVotacao Votacao)
{
    internal ModoDeDeliberacao? ModoConferido() =>
        CatalogoDe<ModoDeDeliberacao>.TentarObter(Modo, out ModoDeDeliberacao valor) ? valor : null;
}

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
        Modo = declarada.ModoConferido()!.Value;
        Integrantes = declarada.Integrantes;
        Suplentes = declarada.Suplentes;
        Quorum = declarada.Votacao.Quorum;
        Criterio = declarada.Votacao.CriterioConferido();
        Desempate = declarada.Votacao.DesempateConferido();
        Encerramento = declarada.Votacao.EncerramentoConferido();
        SemUnanimidade = declarada.Votacao.SemUnanimidadeConferido();
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

}
