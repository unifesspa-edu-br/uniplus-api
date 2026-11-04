namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>Conversão dos tokens de uma exigência do payload para o vocabulário do domínio.</summary>
internal static class EscritaDaExigencia
{
    public static Aplicabilidade AplicabilidadeDoToken(string? token) => token switch
    {
        "GERAL" => Aplicabilidade.Geral,
        "CONDICIONAL" => Aplicabilidade.Condicional,
        _ => Aplicabilidade.Nenhuma,
    };

    /// <summary>
    /// A finalidade do token, nula quando o cliente não a declarou. O token desconhecido vira o sentinela,
    /// que a forma do documento recusa.
    /// </summary>
    public static FinalidadeFormulario? FinalidadeDoToken(string? token) =>
        token is null ? null : EstruturaFormulario.FinalidadeDoToken(token);
}
