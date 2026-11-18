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

    /// <summary>Acumula toda violação da escala (ADR-0125), inclusive o elemento nulo; a escala ausente ou vazia não declara nada.</summary>
    public static List<FieldError> Validar(IReadOnlyList<NivelDeAtendimento?>? niveis)
    {
        if (niveis is null || niveis.Count == 0)
        {
            return [];
        }

        HashSet<string> rotulos = new(StringComparer.Ordinal);
        List<FieldError> erros =
            [.. Enumerable.Range(0, niveis.Count).SelectMany(i => ValidarNivel($"escalaDeAtendimento[{i}]", niveis[i], rotulos))];
        erros.AddRange(FaltaDoNivelCompleto(niveis));
        return erros;
    }

    private static IEnumerable<FieldError> FaltaDoNivelCompleto(IReadOnlyList<NivelDeAtendimento?> niveis)
    {
        if (!niveis.Any(static n => n?.Fracao == 1m))
        {
            yield return new("escalaDeAtendimento", new DomainError(
                SemNivelCompleto,
                "A escala de atendimento precisa de um nível que valha a pontuação inteira do item (fração 1)."));
        }
    }

    private static IEnumerable<FieldError> ValidarNivel(string campo, NivelDeAtendimento? nivel, HashSet<string> rotulos)
    {
        if (nivel is null)
        {
            yield return new(campo, new DomainError(
                RotuloInvalido,
                "O nível de atendimento não pode ser nulo: informe o rótulo e a fração."));
            yield break;
        }

        foreach (FieldError erro in ValidarRotulo(campo, nivel.Rotulo, rotulos).Concat(ValidarFracao(campo, nivel.Fracao)))
        {
            yield return erro;
        }
    }

    private static IEnumerable<FieldError> ValidarFracao(string campo, decimal fracao)
    {
        if (fracao is < 0m or > 1m)
        {
            yield return new($"{campo}.fracao", new DomainError(
                FracaoInvalida,
                "A fração do nível de atendimento deve estar entre 0 e 1."));
        }
    }

    private static IEnumerable<FieldError> ValidarRotulo(string campo, string? rotuloInformado, HashSet<string> rotulos)
    {
        string rotulo = rotuloInformado?.Trim() ?? string.Empty;
        if (rotulo.Length is 0 or > NivelDeAtendimento.RotuloMaxLength)
        {
            yield return new($"{campo}.rotulo", new DomainError(
                RotuloInvalido,
                $"O rótulo do nível de atendimento é obrigatório e deve ter no máximo {NivelDeAtendimento.RotuloMaxLength} caracteres."));
        }
        else if (!rotulos.Add(HashCanonicalComputer.NormalizeNfc(rotulo)))
        {
            yield return new($"{campo}.rotulo", new DomainError(
                NivelDuplicado,
                $"O nível '{rotulo}' aparece mais de uma vez na escala de atendimento."));
        }
    }

    /// <summary>
    /// Do nível que mais vale ao que menos vale, desempatando pelo rótulo: a posição depende do
    /// conteúdo e não da ordem em que o cliente enviou, então regravar a mesma escala não muda o
    /// documento congelado.
    /// </summary>
    public static IReadOnlyList<NivelDeAtendimento>? EmOrdemCanonica(IEnumerable<NivelDeAtendimento?>? niveis)
    {
        List<NivelDeAtendimento> ordenados = [.. (niveis ?? [])
            .Select(static n => new NivelDeAtendimento(n!.Rotulo.Trim(), n.Fracao))
            .OrderByDescending(static n => n.Fracao)
            .ThenBy(static n => HashCanonicalComputer.NormalizeNfc(n.Rotulo), StringComparer.Ordinal)];

        return ordenados.Count == 0 ? null : ordenados;
    }
}
