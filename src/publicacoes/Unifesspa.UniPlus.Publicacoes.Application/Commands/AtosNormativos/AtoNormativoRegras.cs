namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;

/// <summary>
/// Limites, padrões e mensagens do registro de ato normativo, compartilhados
/// entre o validator e (por espelhamento) as invariantes do agregado. As regras
/// avaliam o valor <b>normalizado</b> (trim), como a factory faz.
/// </summary>
internal static class AtoNormativoRegras
{
    public const int OrgaoMaxLength = 200;
    public const int SerieMaxLength = 100;
    public const int NumeroMaxLength = 60;
    public const int TipoCodigoMaxLength = 60;
    public const int AssinanteMaxLength = 200;

    /// <summary>SHA-256 em hexadecimal minúsculo, 64 caracteres.</summary>
    public const string HashPattern = "^[0-9a-f]{64}$";

    public const string OrgaoObrigatorio = "Órgão publicador é obrigatório.";
    public const string OrgaoTamanho = "Órgão publicador deve ter no máximo 200 caracteres.";
    public const string SerieObrigatoria = "Série é obrigatória.";
    public const string SerieTamanho = "Série deve ter no máximo 100 caracteres.";
    public const string AnoInvalido = "Ano deve ser um inteiro positivo.";
    public const string NumeroTamanho = "Número deve ter no máximo 60 caracteres.";
    public const string TipoCodigoObrigatorio = "Código do tipo de ato é obrigatório.";
    public const string TipoCodigoTamanho = "Código do tipo de ato deve ter no máximo 60 caracteres.";
    public const string DocumentoHashObrigatorio = "Hash do documento é obrigatório.";
    public const string DocumentoHashFormato = "Hash do documento deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).";
    public const string AssinanteObrigatorio = "Assinante é obrigatório.";
    public const string AssinanteTamanho = "Assinante deve ter no máximo 200 caracteres.";
    public const string VersaoInvocadaIncompleta = "A versão invocada é o par (id, hash) completo, ou nenhum dos dois — um identificador sem hash não prova nada.";
    public const string VersaoInvocadaIdObrigatorio = "Identificador da versão invocada não pode ser vazio.";
    public const string VersaoInvocadaHashFormato = "Hash da versão invocada deve ser um SHA-256 em hexadecimal minúsculo (64 caracteres).";

    /// <summary>Código do aviso de número duplicado (AC4).</summary>
    public const string AvisoNumeroDuplicado = "NumeroDuplicado";

    public static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
