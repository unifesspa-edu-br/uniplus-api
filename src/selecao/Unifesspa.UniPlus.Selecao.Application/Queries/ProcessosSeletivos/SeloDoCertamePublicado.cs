namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using DTOs;

/// <summary>
/// O selo da representação do certame publicado: identifica o conteúdo servido para a
/// revalidação condicional.
/// </summary>
/// <remarks>
/// A versão da projeção e o hash da configuração cobrem o documento divulgado; a situação cobre o
/// que a leitura acrescenta. Ela muda com o relógio sem mudar o documento, e sem ela no selo uma
/// revalidação responderia 304 com a situação de quando o cliente leu pela última vez.
/// </remarks>
public static class SeloDoCertamePublicado
{
    /// <summary>O selo, já entre aspas, da representação de <paramref name="certame"/>.</summary>
    public static string De(CertamePublicadoDto certame)
    {
        ArgumentNullException.ThrowIfNull(certame);

        return $"\"{certame.VersaoProjecao}:{certame.HashConfiguracao}:{certame.Situacao}\"";
    }
}
