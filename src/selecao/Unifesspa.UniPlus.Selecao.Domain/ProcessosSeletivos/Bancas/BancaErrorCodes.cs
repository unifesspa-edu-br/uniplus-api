namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Bancas;

/// <summary>Códigos de recusa da regra de deliberação das bancas e da banca que o recurso exige.</summary>
public static class BancaErrorCodes
{
    public const string ModoDeDeliberacaoInvalido = "Banca.ModoDeDeliberacaoInvalido";
    public const string IntegrantesInvalidos = "Banca.IntegrantesInvalidos";
    public const string SuplentesInvalidos = "Banca.SuplentesInvalidos";
    public const string AtoIndividualSemVotacao = "Banca.AtoIndividualSemVotacao";
    public const string QuorumForaDoIntervalo = "Banca.QuorumForaDoIntervalo";
    public const string QuorumIncoerente = "Banca.QuorumIncoerente";
    public const string CriterioObrigatorio = "Banca.CriterioObrigatorio";
    public const string EncerramentoObrigatorio = "Banca.EncerramentoObrigatorio";
    public const string DesempateObrigatorio = "Banca.DesempateObrigatorio";
    public const string DesempateNaoSeAplica = "Banca.DesempateNaoSeAplica";
    public const string SemUnanimidadeObrigatoria = "Banca.SemUnanimidadeObrigatoria";
    public const string SemUnanimidadeNaoSeAplica = "Banca.SemUnanimidadeNaoSeAplica";
    public const string RegraDeDeliberacaoObrigatoria = "Banca.RegraDeDeliberacaoObrigatoria";
    public const string IntegrantesAbaixoDoMinimo = "Banca.IntegrantesAbaixoDoMinimo";
    public const string RecorteEmBancaDeRecursos = "Banca.RecorteEmBancaDeRecursos";
    public const string RecursoSemBanca = "Cronograma.RecursoSemBanca";
}
