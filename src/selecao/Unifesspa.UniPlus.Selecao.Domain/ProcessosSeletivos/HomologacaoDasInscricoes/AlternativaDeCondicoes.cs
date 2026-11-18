namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.HomologacaoDasInscricoes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;

/// <summary>
/// Um grupo de condições que valem juntas (E). A ordem declarada é preservada: é a ordem em que os
/// motivos das condições não cumpridas chegam à inscrição não homologada.
/// </summary>
public sealed record AlternativaDeCondicoes
{
    private const string CampoDasCondicoes = "condicoes";

    private AlternativaDeCondicoes(IReadOnlyList<CondicaoDeHomologacao> condicoes) => Condicoes = condicoes;

    public IReadOnlyList<CondicaoDeHomologacao> Condicoes { get; }

    /// <summary>Se a outra alternativa tem as mesmas condições, na mesma ordem.</summary>
    public bool MesmoConteudo(AlternativaDeCondicoes outra)
    {
        ArgumentNullException.ThrowIfNull(outra);
        return Condicoes.Count == outra.Condicoes.Count
            && Condicoes.Zip(outra.Condicoes).All(static par => par.First.MesmoConteudo(par.Second));
    }

    /// <summary>Confere que a alternativa tem ao menos uma condição; a recusa fica no campo das condições (ADR-0125).</summary>
    public static Result<AlternativaDeCondicoes> Declarar(IReadOnlyList<CondicaoDeHomologacao> condicoes)
    {
        ArgumentNullException.ThrowIfNull(condicoes);
        if (condicoes.Any(static condicao => condicao is null))
        {
            throw new ArgumentException("A alternativa não admite condição nula.", nameof(condicoes));
        }

        return condicoes.Count == 0
            ? Result<AlternativaDeCondicoes>.ValidationFailure([new FieldError(CampoDasCondicoes, new DomainError(
                HomologacaoDasInscricoesErrorCodes.AlternativaSemCondicao,
                "A alternativa precisa de ao menos uma condição."))])
            : Result<AlternativaDeCondicoes>.Success(new AlternativaDeCondicoes([.. condicoes]));
    }
}
