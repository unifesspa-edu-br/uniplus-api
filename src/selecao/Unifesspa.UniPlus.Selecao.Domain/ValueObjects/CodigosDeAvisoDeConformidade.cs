namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O conjunto fechado dos códigos de <see cref="AvisoConformidade.Codigo"/>. Um cliente liga a
/// apresentação do aviso ao código, nunca à mensagem.
/// </summary>
public static class CodigosDeAvisoDeConformidade
{
    /// <summary>
    /// O mesmo tipo de ato é declarado como definitivo em mais de uma fase ou etapa, isto é, fecha
    /// mais de um ciclo recursal por declarações distintas.
    /// </summary>
    public const string CronogramaTipoDefinitivoEmMaisDeUmLugar = "cronograma_tipo_definitivo_em_mais_de_um_lugar";
}
