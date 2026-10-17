namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

/// <summary>Configuração da consulta administrativa dos atos do processo.</summary>
public sealed class AtosDoProcessoOptions
{
    public const string SectionName = "Selecao:AtosDoProcesso";

    /// <summary>Menor limiar aceito: abaixo dele a confirmação normal, que leva segundos, apareceria como pendência.</summary>
    public static readonly TimeSpan LimiarMinimo = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Há quanto tempo um ato pode ficar pendente antes de a consulta destacá-lo. A confirmação chega
    /// pela fila em segundos, e a divulgação insiste em espaços de até quinze minutos antes de desistir:
    /// meia hora já separa o atraso da recusa que ninguém vai responder.
    /// </summary>
    public TimeSpan LimiarDePendencia { get; set; } = TimeSpan.FromMinutes(30);
}
