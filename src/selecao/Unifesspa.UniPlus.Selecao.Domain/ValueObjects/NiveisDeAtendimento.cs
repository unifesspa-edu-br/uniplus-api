namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// A escala de atendimento da etapa: o conjunto de níveis entre os quais a banca escolhe, em vez
/// de digitar a nota de cada item. Uma escala só é válida quando algum nível vale a pontuação
/// inteira do item, senão nenhum candidato poderia tirar a nota máxima.
/// </summary>
public static class NiveisDeAtendimento
{
    public const string RotuloInvalido = "NivelDeAtendimento.RotuloInvalido";
    public const string FracaoInvalida = "NivelDeAtendimento.FracaoInvalida";
    public const string NivelDuplicado = "NivelDeAtendimento.Duplicado";
    public const string SemNivelCompleto = "NivelDeAtendimento.SemNivelCompleto";

    /// <summary>Acumula toda violação da escala (ADR-0125); a escala ausente ou vazia não declara nada.</summary>
    public static List<FieldError> Validar(IReadOnlyList<NivelDeAtendimento?>? niveis)
    {
        List<FieldError> erros = [];
        if (niveis is null || niveis.Count == 0)
        {
            return erros;
        }

        HashSet<string> rotulos = new(StringComparer.Ordinal);
        for (int i = 0; i < niveis.Count; i++)
        {
            if (niveis[i] is not { } nivel)
            {
                continue;
            }

            string campo = $"escalaDeAtendimento[{i}]";
            string rotulo = nivel.Rotulo?.Trim() ?? string.Empty;
            if (rotulo.Length is 0 or > NivelDeAtendimento.RotuloMaxLength)
            {
                erros.Add(new($"{campo}.rotulo", new DomainError(
                    RotuloInvalido,
                    $"O rótulo do nível de atendimento é obrigatório e deve ter no máximo {NivelDeAtendimento.RotuloMaxLength} caracteres.")));
            }
            else if (!rotulos.Add(HashCanonicalComputer.NormalizeNfc(rotulo)))
            {
                erros.Add(new($"{campo}.rotulo", new DomainError(
                    NivelDuplicado,
                    $"O nível '{rotulo}' aparece mais de uma vez na escala de atendimento.")));
            }

            if (nivel.Fracao is < 0m or > 1m)
            {
                erros.Add(new($"{campo}.fracao", new DomainError(
                    FracaoInvalida,
                    "A fração do nível de atendimento deve estar entre 0 e 1.")));
            }
        }

        if (!niveis.Any(static n => n?.Fracao == 1m))
        {
            erros.Add(new("escalaDeAtendimento", new DomainError(
                SemNivelCompleto,
                "A escala de atendimento precisa de um nível que valha a pontuação inteira do item (fração 1).")));
        }

        return erros;
    }

    /// <summary>
    /// Do nível que mais vale ao que menos vale, desempatando pelo rótulo: a posição depende do
    /// conteúdo e não da ordem em que o cliente enviou, então regravar a mesma escala não muda o
    /// documento congelado.
    /// </summary>
    public static IReadOnlyList<NivelDeAtendimento>? EmOrdemCanonica(IEnumerable<NivelDeAtendimento>? niveis)
    {
        List<NivelDeAtendimento> ordenados = [.. (niveis ?? [])
            .Select(static n => new NivelDeAtendimento(n.Rotulo.Trim(), n.Fracao))
            .OrderByDescending(static n => n.Fracao)
            .ThenBy(static n => HashCanonicalComputer.NormalizeNfc(n.Rotulo), StringComparer.Ordinal)];

        return ordenados.Count == 0 ? null : ordenados;
    }
}
