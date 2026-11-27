namespace Unifesspa.UniPlus.Infrastructure.Core.Pagination;

using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Resumo SHA-256 em Base64Url (43 caracteres) de uma forma canônica. É a única função que reduz a
/// consulta e a conta de quem consulta a um valor que viaja no cursor sem expor o texto de origem.
/// </summary>
internal static class ResumoCanonico
{
    public static string De(params string[] partes) =>
        Base64Url.EncodeToString(SHA256.HashData(Encoding.UTF8.GetBytes(CompositeSortKey.Serialize(partes))));
}
