namespace Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;

using System.Globalization;
using System.Text;

/// <summary>
/// A forma normalizada de um texto para comparação: sem acento e em minúsculas.
/// É o que a coluna gerada <c>nome_ordenacao</c> guarda, e o que a busca compara
/// contra ela.
/// </summary>
/// <remarks>
/// <para>A tabela de substituição e a expressão SQL vivem aqui, num lugar só,
/// porque as duas pontas precisam concordar caractere a caractere: a coluna é
/// gerada pelo banco, e o termo pesquisado é normalizado pela aplicação antes de
/// virar um <c>LIKE</c> contra ela. Duas tabelas separadas divergiriam em silêncio,
/// e o sintoma seria uma busca que não acha o que existe.</para>
/// <para>A cobertura é a das letras acentuadas do português, não a do Unicode
/// inteiro: o que está fora da tabela passa intacto. É suficiente para nome e
/// código de curso, que é o alcance desta normalização.</para>
/// </remarks>
internal static class NormalizacaoTextual
{
    /// <summary>Letras acentuadas reconhecidas, na ordem em que se substituem.</summary>
    internal const string Acentuadas =
        "ÁÀÂÃÄÅÉÈÊËÍÌÎÏÓÒÔÕÖÚÙÛÜÇÑÝáàâãäåéèêëíìîïóòôõöúùûüçñý";

    /// <summary>Equivalentes sem acento, posição a posição.</summary>
    internal const string SemAcento =
        "AAAAAAEEEEIIIIOOOOOUUUUCNYaaaaaaeeeeiiiiooooouuuucny";

    /// <summary>Nome da função do banco que aplica esta normalização.</summary>
    internal const string FuncaoSql = "configuracao.normalizar_para_comparacao";

    /// <summary>
    /// Corpo da função do banco. Só usa funções imutáveis — requisito do Postgres
    /// para que a função possa ser declarada <c>IMMUTABLE</c> e, com isso, servir a
    /// uma coluna gerada e a um índice. A collation é declarada na conversão para
    /// minúsculas, em vez de herdada do banco.
    /// </summary>
    internal static string CorpoDaFuncaoSql =>
        $"lower(translate(normalize($1, NFC), '{Acentuadas}', '{SemAcento}') COLLATE \"C\")";

    /// <summary>Chamada da função sobre a coluna informada.</summary>
    internal static string ExpressaoSql(string coluna) => $"{FuncaoSql}({coluna})";

    /// <summary>
    /// A mesma normalização, em memória, para o termo pesquisado. Espelha a
    /// expressão acima na ordem das operações: forma Unicode composta, troca das
    /// acentuadas, minúsculas invariantes.
    /// </summary>
    internal static string Normalizar(string texto)
    {
        ArgumentNullException.ThrowIfNull(texto);

        string composto = texto.Normalize(NormalizationForm.FormC);
        StringBuilder trocado = new(composto.Length);

        foreach (char caractere in composto)
        {
            int posicao = Acentuadas.IndexOf(caractere, StringComparison.Ordinal);
            trocado.Append(posicao >= 0 ? SemAcento[posicao] : caractere);
        }

        return trocado.ToString().ToLowerInvariant();
    }

    /// <summary>
    /// Escapa os curingas do <c>LIKE</c> para que o que a pessoa digitou seja
    /// comparado como texto. Sem isso, um <c>%</c> digitado casaria com quase tudo.
    /// A barra invertida vai primeiro, para não escapar as que a própria função
    /// insere em seguida — e é ela mesma o caractere de escape que o Postgres
    /// assume no <c>LIKE</c> quando nenhum outro é declarado.
    /// </summary>
    internal static string EscaparCuringas(string termo)
    {
        ArgumentNullException.ThrowIfNull(termo);

        return termo
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal);
    }

    /// <summary>
    /// Termo pronto para o <c>LIKE</c>: normalizado e com curingas escapados, ou
    /// <see langword="null"/> quando não há busca. Um termo em branco é o mesmo que
    /// nenhum — é o que faz uma consulta com <c>q=</c> vazio ter a mesma
    /// representação canônica de uma sem <c>q</c>, inclusive na assinatura do cursor.
    /// </summary>
    /// <remarks>
    /// O caractere nulo é descartado antes de tudo: texto no Postgres não admite
    /// byte zero, e mandá-lo num padrão de <c>LIKE</c> derruba a consulta no
    /// provider — um <c>q=%00</c> vindo da borda viraria 500. Descartar é o
    /// bastante porque ele não representa nada que alguém queira procurar.
    /// </remarks>
    internal static string? PrepararTermoDeBusca(string? termo)
    {
        if (termo is null)
        {
            return null;
        }

        string semNulo = termo.Replace("\0", string.Empty, StringComparison.Ordinal);

        return string.IsNullOrWhiteSpace(semNulo)
            ? null
            : EscaparCuringas(Normalizar(semNulo.Trim()));
    }
}
