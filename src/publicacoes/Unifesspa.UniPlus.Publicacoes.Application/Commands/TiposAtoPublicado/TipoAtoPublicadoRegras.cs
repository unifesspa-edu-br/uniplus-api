namespace Unifesspa.UniPlus.Publicacoes.Application.Commands.TiposAtoPublicado;

/// <summary>
/// Limites e formato compartilhados pelos validators de criação e atualização.
/// O agregado é a autoridade — o validator apenas antecipa a recusa, devolvendo
/// 400 antes de o handler tocar no repositório.
/// </summary>
internal static class TipoAtoPublicadoRegras
{
    public const int CodigoMaxLength = 60;
    public const int NomeMinLength = 2;
    public const int NomeMaxLength = 200;
    public const int BaseLegalMaxLength = 500;

    /// <summary>Caixa alta sem acento, palavras separadas por um único underscore.</summary>
    public const string CodigoPattern = "^[A-Z]+(_[A-Z]+)*$";

    public const string CodigoObrigatorio = "Código do tipo de ato é obrigatório.";
    public const string CodigoTamanho = "Código do tipo de ato deve ter no máximo 60 caracteres.";
    public const string CodigoFormato =
        "Código do tipo de ato deve usar apenas letras maiúsculas sem acento, separadas por underscore (ex.: EDITAL_ABERTURA).";
    public const string NomeObrigatorio = "Nome do tipo de ato é obrigatório.";
    public const string NomeTamanho = "Nome do tipo de ato deve ter entre 2 e 200 caracteres.";
    public const string BaseLegalTamanho = "Base legal deve ter no máximo 500 caracteres.";

    /// <summary>O fim da vigência é exclusivo: uma janela cujo fim iguala o início não contém dia algum.</summary>
    public const string VigenciaFim = "Fim da vigência é exclusivo e deve ser posterior ao início.";

    public const string IdObrigatorio = "Identificador do tipo de ato é obrigatório.";

    /// <summary>
    /// Mesma normalização do agregado. O validator precisa avaliar o valor que será
    /// persistido, não o que veio no payload: sem isto, "  EDITAL_ABERTURA  " seria
    /// recusado com 400 embora o domínio o aceite, e " E" passaria o comprimento
    /// mínimo aqui para ser recusado adiante com outro status.
    /// </summary>
    public static string? Normalizar(string? valor) => valor?.Trim();
}
