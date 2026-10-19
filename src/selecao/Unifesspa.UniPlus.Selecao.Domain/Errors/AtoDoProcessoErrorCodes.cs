namespace Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>Códigos de erro da publicação de um ato avulso do processo e da correção de um ato publicado.</summary>
public static class AtoDoProcessoErrorCodes
{
    public const string CertameNaoDivulgado = "AtoDoProcesso.CertameNaoDivulgado";
    public const string TipoCongelaConfiguracao = "AtoDoProcesso.TipoCongelaConfiguracao";
    public const string TipoNaoPublicavelComoAvulso = "AtoDoProcesso.TipoNaoPublicavelComoAvulso";
    public const string TipoExigeAtoCorrigido = "AtoDoProcesso.TipoExigeAtoCorrigido";

    public const string DocumentoNaoEncontrado = "AtoDoProcesso.DocumentoNaoEncontrado";
    public const string DocumentoNaoConfirmado = "AtoDoProcesso.DocumentoNaoConfirmado";
    public const string DocumentoNaoEnviadoComoDocumentoDeAto = "AtoDoProcesso.DocumentoNaoEnviadoComoDocumentoDeAto";
    public const string DocumentoPrincipalNaoEhPdf = "AtoDoProcesso.DocumentoPrincipalNaoEhPdf";
    public const string DocumentoRepetido = "AtoDoProcesso.DocumentoRepetido";
    public const string DocumentoJaUsadoEmOutroAto = "AtoDoProcesso.DocumentoJaUsadoEmOutroAto";
    public const string AnexoNomeInvalido = "AtoDoProcesso.AnexoNomeInvalido";
    public const string AnexoNomeRepetido = "AtoDoProcesso.AnexoNomeRepetido";
    public const string QuantidadeDeAnexosExcedida = "AtoDoProcesso.QuantidadeDeAnexosExcedida";

    public const string AtoCorrigidoNaoEncontrado = "AtoDoProcesso.AtoCorrigidoNaoEncontrado";
    public const string AtoCorrigidoDeOutroProcesso = "AtoDoProcesso.AtoCorrigidoDeOutroProcesso";
    public const string AtoCorrigidoDeOutraClasse = "AtoDoProcesso.AtoCorrigidoDeOutraClasse";
    public const string AtoCorrigidoJaCorrigido = "AtoDoProcesso.AtoCorrigidoJaCorrigido";

    public const string AtoCorrigidoObrigatorio = "AtoDoProcesso.AtoCorrigidoObrigatorio";
    public const string AtoCorrigidoInvalido = "AtoDoProcesso.AtoCorrigidoInvalido";
    public const string MotivoDaCorrecaoObrigatorio = "AtoDoProcesso.MotivoDaCorrecaoObrigatorio";
}
