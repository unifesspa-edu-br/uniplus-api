namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

/// <summary>Se a permissão valia por unidade antes e depois da troca de catálogo; os dois valores são sempre diferentes.</summary>
public sealed record MudancaDoEscopo
{
    public MudancaDoEscopo(bool deUnidadeAnterior, bool deUnidadeNovo)
    {
        if (deUnidadeAnterior == deUnidadeNovo)
        {
            throw new ArgumentException("O escopo novo deve ser diferente do anterior.", nameof(deUnidadeNovo));
        }

        DeUnidadeAnterior = deUnidadeAnterior;
        DeUnidadeNovo = deUnidadeNovo;
    }

    public bool DeUnidadeAnterior { get; }

    public bool DeUnidadeNovo { get; }
}
