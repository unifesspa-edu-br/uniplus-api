namespace Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Campos que a vitrine aceita no parâmetro de ordenação. É contrato público: os nomes aqui são os
/// que aparecem na consulta, na mensagem de recusa e na documentação da API.
/// </summary>
/// <remarks>
/// <para>
/// A lista é fechada de propósito. Ordenar por coluna arbitrária transformaria uma rota anônima
/// numa consulta livre sobre o banco — sem índice previsível e sem limite de custo, e servindo de
/// sonda para descobrir quais colunas existem. Acrescentar um campo aqui é decisão, não configuração.
/// </para>
/// <para>
/// Nenhum deles precisa ser único: a ordenação termina sempre pelo identificador, que o motor de
/// paginação acrescenta.
/// </para>
/// </remarks>
public static class CamposOrdenacaoDaVitrine
{
    /// <summary>Encerramento da janela de inscrição.</summary>
    public const string InscricoesAte = "inscricoesAte";

    /// <summary>Abertura da janela de inscrição.</summary>
    public const string InscricoesDe = "inscricoesDe";

    /// <summary>Título do certame, em ordem alfabética real — sem acento e sem caixa.</summary>
    public const string Nome = "nome";

    /// <summary>Instante em que o certame passou a ser público.</summary>
    public const string DivulgadoEm = "divulgadoEm";

    /// <summary>Todos os campos aceitos, na ordem em que a documentação os apresenta.</summary>
    public static IReadOnlyList<string> Todos { get; } =
        [InscricoesAte, InscricoesDe, Nome, DivulgadoEm];
}
