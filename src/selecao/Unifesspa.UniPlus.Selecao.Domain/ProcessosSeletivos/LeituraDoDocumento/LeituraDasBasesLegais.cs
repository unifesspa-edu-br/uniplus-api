namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;

/// <summary>
/// As bases legais da exigência ou da regra de comprovação, cada uma criada pela fábrica do domínio do tipo dela. As recusas de todas
/// se acumulam, cada uma no campo da base, pela posição em que ela veio na lista enviada.
/// </summary>
internal static class LeituraDasBasesLegais
{
    private const string Campo = "basesLegais";

    internal static ParteLida<IReadOnlyList<DocumentoExigidoBaseLegal>> DoDocumento(IReadOnlyList<BaseLegalNoDocumento> bases) =>
        Ler(bases, static baseLegal => DocumentoExigidoBaseLegal.Criar(baseLegal.Referencia!, baseLegal.Abrangencia, baseLegal.Situacao, baseLegal.Observacao));

    internal static ParteLida<IReadOnlyList<BaseLegalDaComprovacao>> DoGrupo(IReadOnlyList<BaseLegalNoDocumento> bases) =>
        Ler(bases, static baseLegal => BaseLegalDaComprovacao.Criar(baseLegal.Referencia!, baseLegal.Abrangencia, baseLegal.Situacao, baseLegal.Observacao));

    private static ParteLida<IReadOnlyList<T>> Ler<T>(IReadOnlyList<BaseLegalNoDocumento> bases, Func<BaseLegalNoDocumento, Result<T>> criar)
        where T : class
    {
        (BaseLegalNoDocumento Citada, Result<T> Lida)[] lidas = [.. bases.Select(citada => (citada, criar(citada)))];
        FieldError[] recusas =
        [
            .. lidas.SelectMany(static par => par.Lida.Errors.Select(recusa => recusa.NoCaminhoDoItem($"{Campo}[{par.Citada.PosicaoNoDocumento}]"))),
        ];

        return recusas.Length > 0
            ? ParteLida<IReadOnlyList<T>>.Recusada(recusas)
            : ParteLida<IReadOnlyList<T>>.Montada([.. lidas.Select(static par => par.Lida.Value!)]);
    }
}
