namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using DTOs;

/// <summary>
/// O selo da representação do certame publicado: identifica o conteúdo servido para a
/// revalidação condicional.
/// </summary>
/// <remarks>
/// A versão da projeção e o hash da configuração cobrem o documento divulgado; a situação e os
/// editais cobrem o que a leitura acrescenta. A situação muda com o relógio sem mudar o documento, e
/// sem ela no selo uma revalidação responderia 304 com a situação de quando o cliente leu pela última
/// vez. Os editais mudam quando um ato atrasado registra o seu sem avançar a versão — nem o hash nem a
/// versão da projeção se movem —, e sem um resumo deles uma revalidação serviria a lista antiga.
/// </remarks>
public static class SeloDoCertamePublicado
{
    /// <summary>O selo, já entre aspas, da representação de <paramref name="certame"/>.</summary>
    public static string De(CertamePublicadoDto certame)
    {
        ArgumentNullException.ThrowIfNull(certame);

        return $"\"{certame.VersaoProjecao}:{certame.HashConfiguracao}:{certame.Situacao}:{ResumoDosEditais(certame)}\"";
    }

    /// <summary>
    /// Resumo curto e estável da lista de editais, na ordem servida: qualquer ato ou endereço a mais,
    /// a menos ou diferente muda o resultado.
    /// </summary>
    private static string ResumoDosEditais(CertamePublicadoDto certame)
    {
        string pares = string.Join(
            ';',
            (certame.EditaisPublicados ?? []).Select(static e => string.Create(
                CultureInfo.InvariantCulture, $"{e.AtoId:N}={e.UrlDownload.OriginalString}")));

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pares)), 0, 4).ToLowerInvariant();
    }
}
