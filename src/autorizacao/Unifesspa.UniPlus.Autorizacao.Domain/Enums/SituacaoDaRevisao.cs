namespace Unifesspa.UniPlus.Autorizacao.Domain.Enums;

/// <summary>
/// Situação da revisão periódica dos acessos, derivada dos itens a cada leitura: em andamento enquanto há item sem decisão
/// sobre concessão ainda ativa, e concluída quando não há.
/// </summary>
public enum SituacaoDaRevisao
{
    EmAndamento = 1,
    Concluida = 2,
}

/// <summary>Código textual canônico de <see cref="SituacaoDaRevisao"/>, o valor exposto e aceito no filtro.</summary>
public static class SituacaoDaRevisaoCodigo
{
    public const string EmAndamento = "em_andamento";
    public const string Concluida = "concluida";

    public static string ToCodigo(this SituacaoDaRevisao situacao) => situacao switch
    {
        SituacaoDaRevisao.EmAndamento => EmAndamento,
        SituacaoDaRevisao.Concluida => Concluida,
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, null),
    };

    /// <summary>A situação do código, sem distinguir maiúsculas nem espaços nas pontas; nula quando o código não existe.</summary>
    public static SituacaoDaRevisao? DoCodigo(string? codigo) => codigo?.Trim().ToLowerInvariant() switch
    {
        EmAndamento => SituacaoDaRevisao.EmAndamento,
        Concluida => SituacaoDaRevisao.Concluida,
        _ => null,
    };
}
