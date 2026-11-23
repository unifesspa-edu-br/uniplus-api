namespace Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>Dados declarados de um item de avaliação.</summary>
public sealed record ItemDeAvaliacaoDeclarado(Guid? Id, string Rotulo, decimal PontuacaoMaxima)
{
    public string RotuloNormalizado => HashCanonicalComputer.NormalizeNfc(Rotulo?.Trim() ?? string.Empty);

    public FieldError? RecusaDoRotulo(string campo) =>
        RotuloNormalizado.Length is 0 or > ItemDeAvaliacao.RotuloMaxLength
            ? new(campo, new DomainError(ItemDeAvaliacao.RotuloInvalido,
                $"O rótulo do item de avaliação é obrigatório e deve ter no máximo {ItemDeAvaliacao.RotuloMaxLength} caracteres."))
            : null;

    public IEnumerable<FieldError> RecusasDaPontuacao(string campo, decimal? passoDoLancamento)
    {
        if (PrecisaoDecimal.Recusa(PontuacaoMaxima, 18, 4, campo, "A pontuação máxima do item deve ter no máximo 14 dígitos inteiros e 4 casas decimais.") is { } recusa)
        {
            yield return recusa;
        }

        if (PontuacaoMaxima <= 0)
        {
            yield return new(campo, new DomainError(ItemDeAvaliacao.PontuacaoMaximaInvalida, "A pontuação máxima do item de avaliação deve ser maior que zero."));
        }
        else if (passoDoLancamento > 0 && PontuacaoMaxima % passoDoLancamento != 0)
        {
            yield return new(campo, new DomainError(ItemDeAvaliacao.PontuacaoForaDoPasso, "A pontuação máxima do item deve ser múltipla do passo do lançamento da etapa."));
        }
    }
}
