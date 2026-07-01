namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio da <see cref="Entities.Modalidade"/> de concorrência
/// (UNI-REQ-0011), prefixados por <c>Modalidade.</c>. Mapeados para status HTTP em
/// <c>ConfiguracaoDomainErrorRegistration</c>:
/// <list type="bullet">
///   <item><description><see cref="CodigoJaExiste"/> → 409 Conflict</description></item>
///   <item><description><see cref="RemocaoBloqueadaPorReferencia"/> → 409 Conflict</description></item>
///   <item><description><see cref="NaoEncontrada"/> → 404 Not Found</description></item>
///   <item><description>demais → 422 Unprocessable Entity</description></item>
/// </list>
/// </summary>
public static class ModalidadeErrorCodes
{
    public const string CodigoObrigatorio = "Modalidade.CodigoObrigatorio";
    public const string CodigoFormatoInvalido = "Modalidade.CodigoFormatoInvalido";
    public const string CodigoJaExiste = "Modalidade.CodigoJaExiste";
    public const string DescricaoTamanho = "Modalidade.DescricaoTamanho";
    public const string NaturezaInvalida = "Modalidade.NaturezaInvalida";
    public const string ComposicaoVagasInvalida = "Modalidade.ComposicaoVagasInvalida";
    public const string RegraRemanejamentoInvalida = "Modalidade.RegraRemanejamentoInvalida";
    public const string NaturezaRemanejamentoIncoerente = "Modalidade.NaturezaRemanejamentoIncoerente";
    public const string OrigemObrigatoriaParaRetiraDe = "Modalidade.OrigemObrigatoriaParaRetiraDe";
    public const string OrigemApenasParaRetiraDe = "Modalidade.OrigemApenasParaRetiraDe";
    public const string ArgumentoRemanejamentoObrigatorio = "Modalidade.ArgumentoRemanejamentoObrigatorio";
    public const string AcaoIndeferimentoInvalida = "Modalidade.AcaoIndeferimentoInvalida";
    public const string ReferenciaInexistenteOuInativa = "Modalidade.ReferenciaInexistenteOuInativa";
    public const string RemocaoBloqueadaPorReferencia = "Modalidade.RemocaoBloqueadaPorReferencia";
    public const string BaseLegalTamanho = "Modalidade.BaseLegalTamanho";
    public const string NaoEncontrada = "Modalidade.NaoEncontrada";
}
