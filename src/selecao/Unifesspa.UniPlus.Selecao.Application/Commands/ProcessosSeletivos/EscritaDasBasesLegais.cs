namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Kernel.Results;

/// <summary>Confere e constrói as bases legais informadas, acumulando as recusas por posição.</summary>
internal static class EscritaDasBasesLegais
{
    public static Result<IReadOnlyList<TBase>> Resolver<TBase>(
        IReadOnlyList<BaseLegalInput> declaradas,
        Func<BaseLegalInput, List<FieldError>> conferirForma,
        Func<BaseLegalInput, TBase> criar)
    {
        List<FieldError> erros = [];
        for (int indice = 0; indice < declaradas.Count; indice++)
        {
            erros.AddRange(ComIndice(conferirForma(declaradas[indice]), indice));
        }

        return erros.Count > 0
            ? Result<IReadOnlyList<TBase>>.ValidationFailure(erros)
            : Result<IReadOnlyList<TBase>>.Success([.. declaradas.Select(criar)]);
    }

    public static TipoAbrangencia Abrangencia(BaseLegalInput declarada) => TipoAbrangenciaCodigo.FromCodigo(declarada.Abrangencia);

    public static StatusBaseLegal Situacao(BaseLegalInput declarada) => StatusBaseLegalCodigo.FromCodigo(declarada.Situacao);

    public static IEnumerable<FieldError> ComIndice(IEnumerable<FieldError> erros, int indice) =>
        erros.Select(erro => erro with { Field = $"{BaseLegalInput.CaminhoDaLista}[{indice}].{erro.Field}" });
}
