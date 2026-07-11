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
    public const int MotivoRetificacaoMaxLength = 1000;
    public const int EntidadeTipoMaxLength = 60;

    /// <summary>SHA-256 em hexadecimal minúsculo, 64 caracteres.</summary>
    public const string HashPattern = "^[0-9a-f]{64}$";

    /// <summary>
    /// Forma canônica do rótulo da entidade vinculada. Verifica a <b>forma</b>, jamais o
    /// valor: não há lista de tipos permitidos, e não pode haver — o rótulo é opaco
    /// (ADR-0105). O que se exige é grafia única, sem a qual a mesma entidade se
    /// partiria em duas na consulta unificada.
    /// </summary>
    public const string EntidadeTipoPattern = "^[A-Z0-9]+(_[A-Z0-9]+)*$";

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
    public const string RetificacaoIncompleta = "A retificação é o par (ato retificado, motivo) completo, ou nenhum dos dois — um sem o outro não registra linhagem.";
    public const string AtoRetificadoIdObrigatorio = "Identificador do ato retificado não pode ser vazio.";
    public const string MotivoRetificacaoTamanho = "Motivo da retificação deve ter no máximo 1000 caracteres.";

    public const string EntidadeTipoObrigatorio = "Tipo da entidade vinculada é obrigatório.";
    public const string EntidadeTipoTamanho = "Tipo da entidade vinculada deve ter no máximo 60 caracteres.";
    public const string EntidadeTipoFormato = "Tipo da entidade vinculada deve ser um rótulo em maiúsculas, com grupos separados por sublinhado.";
    public const string EntidadeIdObrigatorio = "Identificador da entidade vinculada não pode ser vazio.";
    public const string VinculoDuplicado = "A mesma entidade não pode ser vinculada duas vezes ao mesmo ato.";
    public const string VinculoNulo = "A lista de vínculos não admite elemento nulo.";

    /// <summary>Código do aviso de número duplicado (AC4).</summary>
    public const string AvisoNumeroDuplicado = "NumeroDuplicado";

    public static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
