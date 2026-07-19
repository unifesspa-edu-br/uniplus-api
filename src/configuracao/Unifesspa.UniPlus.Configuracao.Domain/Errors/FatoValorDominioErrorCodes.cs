namespace Unifesspa.UniPlus.Configuracao.Domain.Errors;

/// <summary>
/// Códigos de erro de domínio de <see cref="Entities.FatoValorDominio"/> — o valor
/// individual de um categórico estático do catálogo de fatos (ADR-0116), prefixados
/// por <c>FatoValorDominio.</c>. Mapeados para 422 Unprocessable Entity em
/// <c>ConfiguracaoDomainErrorRegistration</c> (nenhum é 404: o valor não é
/// endereçado isoladamente, só através do <c>FatoCandidato</c> pai).
/// </summary>
/// <remarks>
/// Assim como <c>FatoCandidatoErrorCodes</c>, estes erros são alcançáveis apenas
/// pela semeadura em desenvolvimento — o catálogo é seed-governado, sem CRUD.
/// </remarks>
public static class FatoValorDominioErrorCodes
{
    /// <summary>O fato pai não é categórico — só um categórico admite valores de domínio.</summary>
    public const string NaoPermitidoForaDeCategorico = "FatoValorDominio.NaoPermitidoForaDeCategorico";

    public const string CodigoObrigatorio = "FatoValorDominio.CodigoObrigatorio";
    public const string CodigoTamanho = "FatoValorDominio.CodigoTamanho";

    /// <summary>Código já usado por um irmão do mesmo <c>FatoCandidato</c> (normalizado, comparação ordinal).</summary>
    public const string CodigoDuplicado = "FatoValorDominio.CodigoDuplicado";

    /// <summary>Descrição ausente quando o fato pai é <see cref="Enums.OrigemFato.Declarado"/>.</summary>
    public const string DescricaoObrigatoria = "FatoValorDominio.DescricaoObrigatoria";
    public const string DescricaoTamanho = "FatoValorDominio.DescricaoTamanho";
    public const string OrdemInvalida = "FatoValorDominio.OrdemInvalida";
}
