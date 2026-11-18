namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>Códigos de erro dos tipos de etapa configuráveis (UNI-REQ-0015, UNI-REQ-0087).</summary>
public static class TipoEtapaErrorCodes
{
    public const string CodigoObrigatorio = "TipoEtapa.CodigoObrigatorio";
    public const string CodigoTamanho = "TipoEtapa.CodigoTamanho";
    public const string CodigoCaractereInvalido = "TipoEtapa.CodigoCaractereInvalido";
    public const string CodigoJaExiste = "TipoEtapa.CodigoJaExiste";
    public const string NomeObrigatorio = "TipoEtapa.NomeObrigatorio";
    public const string NomeTamanho = "TipoEtapa.NomeTamanho";
    public const string NomeCaractereInvalido = "TipoEtapa.NomeCaractereInvalido";
    public const string DescricaoTamanho = "TipoEtapa.DescricaoTamanho";
    public const string DescricaoCaractereInvalido = "TipoEtapa.DescricaoCaractereInvalido";
    public const string AdmitePontuacaoObrigatorio = "TipoEtapa.AdmitePontuacaoObrigatorio";
    public const string AdmiteEliminacaoObrigatorio = "TipoEtapa.AdmiteEliminacaoObrigatorio";
    public const string ExigeCorrecaoIndependenteObrigatorio = "TipoEtapa.ExigeCorrecaoIndependenteObrigatorio";
    public const string CorrecaoIndependenteComNotaDeOrigemNoEnem = "TipoEtapa.CorrecaoIndependenteComNotaDeOrigemNoEnem";
    public const string CorrecaoIndependenteSemConceito = "TipoEtapa.CorrecaoIndependenteSemConceito";
    public const string SemCaraterAdmitido = "TipoEtapa.SemCaraterAdmitido";
    public const string ConceitosAdmitidosObrigatorio = "TipoEtapa.ConceitosAdmitidosObrigatorio";
    public const string ConceitoInexistente = "TipoEtapa.ConceitoInexistente";
    public const string ConceitoInativo = "TipoEtapa.ConceitoInativo";
    public const string ConceitoDuplicado = "TipoEtapa.ConceitoDuplicado";
    public const string NotaDeOrigemNoEnemExigePontuacao = "TipoEtapa.NotaDeOrigemNoEnemExigePontuacao";
    public const string NotaDeOrigemNoEnemNaoDesativa = "TipoEtapa.NotaDeOrigemNoEnemNaoDesativa";
    public const string NaoEncontrado = "TipoEtapa.NaoEncontrado";
    public const string JaDesativado = "TipoEtapa.JaDesativado";
    public const string ConflitoDeConcorrencia = "TipoEtapa.ConflitoDeConcorrencia";
}
