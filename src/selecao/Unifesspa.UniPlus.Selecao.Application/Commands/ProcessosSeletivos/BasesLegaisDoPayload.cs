namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Enums;

using Kernel.Results;

/// <summary>
/// Resolve as bases legais declaradas em um nó da árvore de exigências — da folha ou do grupo, que são
/// entidades diferentes com a mesma forma de wire. Só a forma de cada base é conferida aqui (referência não
/// vazia, abrangência e status no domínio fechado): o gate de pelo menos uma base resolvida por exigência
/// que determina resultado é da publicação, nunca da escrita.
/// </summary>
/// <remarks>
/// Acumula a forma de TODAS as bases do nó numa primeira passada (ADR-0125), indexando o campo, e só se ela
/// vier limpa constrói as instâncias. O escopo da acumulação é o nó: entre nós a árvore falha no primeiro,
/// porque acumulá-la inteira exigiria um caminho posicional sem convenção estabelecida.
/// </remarks>
internal static class BasesLegaisDoPayload
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

    public static StatusBaseLegal Status(BaseLegalInput declarada) => StatusBaseLegalCodigo.FromCodigo(declarada.Status);

    public static IEnumerable<FieldError> ComIndice(IEnumerable<FieldError> erros, int indice) =>
        erros.Select(erro => erro with { Field = $"basesLegais[{indice}].{erro.Field}" });
}
