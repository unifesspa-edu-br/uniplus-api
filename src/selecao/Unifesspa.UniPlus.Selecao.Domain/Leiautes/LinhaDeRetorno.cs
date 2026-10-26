namespace Unifesspa.UniPlus.Selecao.Domain.Leiautes;

using System.Globalization;

using Unifesspa.UniPlus.Selecao.Domain.Enums;

/// <summary>A nota e a situação do candidato em uma área, como o arquivo as traz.</summary>
public sealed record ResultadoDaAreaNoRetorno(string Area, decimal Nota, SituacaoDaProvaNoEnem Situacao);

/// <summary>
/// Uma linha do arquivo de retorno já lida, com o mesmo conjunto fechado de campos qualquer que
/// seja o leiaute. O cruzamento com as inscrições e a avaliação só conhecem este tipo.
/// </summary>
/// <param name="NumeroDaLinha">Posição da linha no arquivo, a partir de 1.</param>
/// <param name="Cpf">Os onze dígitos; existe só em memória durante a importação.</param>
/// <param name="NumeroDeInscricaoNoEnem">Os doze dígitos do INEP.</param>
/// <param name="Areas">Uma entrada por área, na ordem canônica de <c>AreaDoEnem.Todas</c>.</param>
public sealed record LinhaDeRetorno(
    int NumeroDaLinha,
    string Cpf,
    string NumeroDeInscricaoNoEnem,
    IReadOnlyList<ResultadoDaAreaNoRetorno> Areas)
{
    /// <summary>
    /// Duas linhas do mesmo CPF com a mesma chave dizem a mesma coisa: contam como uma. Só os
    /// campos que o Uni+ usa participam, então linhas que diferem apenas em colunas ignoradas
    /// (nome, dados cadastrais) são iguais.
    /// </summary>
    public string ChaveDeIgualdade =>
        string.Join(
            '|',
            [
                NumeroDeInscricaoNoEnem,
                .. Areas.Select(a => string.Create(
                    CultureInfo.InvariantCulture, $"{a.Area}:{a.Nota}:{(int)a.Situacao}")),
            ]);
}

/// <summary>O conteúdo de um arquivo de retorno que passou pela conferência do leiaute.</summary>
public sealed record ArquivoDeRetornoLido(IReadOnlyList<LinhaDeRetorno> Linhas);
