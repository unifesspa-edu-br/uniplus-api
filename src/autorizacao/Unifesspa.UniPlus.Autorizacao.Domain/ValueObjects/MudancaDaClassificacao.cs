namespace Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>A classificação de proteção de uma permissão antes e depois da troca de catálogo; as duas são sempre diferentes.</summary>
public sealed record MudancaDaClassificacao
{
    public MudancaDaClassificacao(ClassificacaoDeProtecao anterior, ClassificacaoDeProtecao nova)
    {
        if (anterior == nova)
        {
            throw new ArgumentException("A classificação nova deve ser diferente da anterior.", nameof(nova));
        }

        Anterior = anterior;
        Nova = nova;
    }

    public ClassificacaoDeProtecao Anterior { get; }

    public ClassificacaoDeProtecao Nova { get; }
}
