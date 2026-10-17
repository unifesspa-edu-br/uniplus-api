namespace Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Rótulos de entidade (ADR-0105) cujos atos só entram em Publicações pela requisição
/// durável do domínio dono do objeto, e nunca pelo endpoint administrativo.
/// </summary>
/// <remarks>
/// O endpoint administrativo recebe só os metadados e o hash do documento, não o arquivo.
/// Um ato de certame registrado por ali ficaria sem documento: quem guarda o arquivo e o
/// copia ao acervo público é o domínio que publica. O rótulo é o único conhecimento que
/// Publicações tem do domínio, e vive neste contrato para que o domínio que o emite e o
/// módulo que o recusa no HTTP leiam a mesma constante.
/// </remarks>
public static class EntidadesDeRegistroPeloDominio
{
    /// <summary>Rótulo do processo seletivo no vínculo genérico de Publicações.</summary>
    public const string ProcessoSeletivo = "PROCESSO_SELETIVO";

    /// <summary>Indica se o rótulo (já normalizado) só admite registro pelo domínio.</summary>
    public static bool Contem(string? entidadeTipo) =>
        string.Equals(entidadeTipo, ProcessoSeletivo, StringComparison.Ordinal);
}
