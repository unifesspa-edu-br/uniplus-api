namespace Unifesspa.UniPlus.Authorization.Catalogo;

using System.Globalization;
using System.Security.Cryptography;
using System.Text;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// O que o catálogo registra de cada permissão para decidir se o acesso dela mudou: a classificação, o escopo e a reserva
/// à governança. É a linha de uma aplicação do catálogo e a unidade da impressão.
/// </summary>
/// <param name="Codigo">Código <c>{módulo}:{resource}:{ação}</c>.</param>
/// <param name="Modulo">Módulo dono da permissão.</param>
/// <param name="Classificacao">Classificação de proteção que o catálogo atribui à permissão.</param>
/// <param name="DeUnidade">A permissão exige a unidade proprietária; as demais são institucionais.</param>
/// <param name="Reservada">Só perfis de governança podem reunir a permissão.</param>
public sealed record LinhaDoCatalogo(string Codigo, string Modulo, ClassificacaoDeProtecao Classificacao, bool DeUnidade, bool Reservada)
{
    /// <summary>A classificação como texto estável em minúsculas, o mesmo formato gravado nas versões dos perfis.</summary>
    public string ClassificacaoEmTexto => Classificacao.ToString().ToLowerInvariant();

    /// <summary>
    /// A impressão do catálogo: SHA-256 em hexadecimal das linhas <c>codigo|modulo|classificacao|de_unidade|reservada</c>,
    /// em ordem ordinal de código, uma por linha. Não depende da ordem em que as linhas chegam.
    /// </summary>
    public static string ImpressaoDe(IEnumerable<LinhaDoCatalogo> linhas)
    {
        ArgumentNullException.ThrowIfNull(linhas);

        StringBuilder texto = new();
        foreach (LinhaDoCatalogo linha in linhas.OrderBy(static l => l.Codigo, StringComparer.Ordinal))
        {
            texto.Append(CultureInfo.InvariantCulture, $"{linha.Codigo}|{linha.Modulo}|{linha.ClassificacaoEmTexto}|{Booleano(linha.DeUnidade)}|{Booleano(linha.Reservada)}\n");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(texto.ToString())));
    }

    /// <summary>O booleano como texto estável, o mesmo gravado no banco.</summary>
    public static string Booleano(bool valor) => valor ? "true" : "false";
}
