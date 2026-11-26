namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os procedimentos de recurso de uma regra de recurso, da fase ou da etapa. O procedimento com tipo de decisão fora do vocabulário,
/// regra fora do catálogo ou alvo que a mãe não tem é omitido, e cada causa se acumula. O alvo que é item de avaliação se acha pela
/// chave que o documento declarou para o item na própria etapa; a fase não tem itens.
/// </summary>
internal static class LeituraDosProcedimentosDeRecurso
{
    /// <param name="procedimentos">Os procedimentos como o documento os traz, cada um com a posição enviada.</param>
    /// <param name="lista">O caminho da lista de procedimentos relativo à mãe, de onde sai o campo de cada causa.</param>
    /// <param name="posicaoDaMae">A posição que a fase ou a etapa mãe tem no documento, que as causas apontam.</param>
    /// <param name="codigos">Os códigos de erro da mãe: o da fase e o da etapa diferem só no prefixo.</param>
    /// <param name="itensPelaChave">O identificador de cada item de avaliação da mãe, pela chave declarada; vazio na fase.</param>
    /// <param name="cadastros">Os cadastros citados no documento.</param>
    internal static ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> Ler(
        IReadOnlyList<ProcedimentoNoDocumento> procedimentos,
        string lista,
        int posicaoDaMae,
        CodigosDoProcedimentoDeRecurso codigos,
        IReadOnlyDictionary<string, Guid> itensPelaChave,
        CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(procedimentos);
        ArgumentNullException.ThrowIfNull(itensPelaChave);
        ArgumentNullException.ThrowIfNull(cadastros);

        Contexto contexto = new(lista, posicaoDaMae, codigos, itensPelaChave, cadastros);
        (int PosicaoNoDocumento, ValorLido<ProcedimentoDeRecursoDeclarado?> Lido)[] lidos =
            [.. procedimentos.Select(procedimento => (procedimento.PosicaoNoDocumento, Lido: LerProcedimento(procedimento, contexto)))];

        return new(
            [.. lidos.Where(static item => item.Lido.Valor is not null).Select(static item => (item.Lido.Valor!, item.PosicaoNoDocumento))],
            [.. lidos.SelectMany(static item => item.Lido.Recusas)]);
    }

    private static ValorLido<ProcedimentoDeRecursoDeclarado?> LerProcedimento(ProcedimentoNoDocumento procedimento, Contexto contexto)
    {
        RegraDoCatalogoCitada? regra = contexto.Cadastros.Regra(procedimento.Regra.Codigo, procedimento.Regra.Versao);
        RecusaDaParte[] recusas = [.. Recusas(procedimento, regra, contexto)];

        if (recusas.Length > 0 || regra is null || procedimento.TipoDeDecisao is not { } tipo)
        {
            return new(null, recusas);
        }

        return new(ProcedimentoDeRecursoDeclarado.De(tipo, Alvo(procedimento.Alvo, contexto.ItensPelaChave), regra.Referencia), []);
    }

    private static AlvoDoProcedimentoDeRecurso? Alvo(AlvoNoDocumento? alvo, IReadOnlyDictionary<string, Guid> itensPelaChave) => alvo switch
    {
        { ItemChave: { } chave } => itensPelaChave.TryGetValue(chave, out Guid id) ? AlvoDoProcedimentoDeRecurso.DoItem(id) : null,
        { AreaDoEnem: { } area } => AlvoDoProcedimentoDeRecurso.DaArea(area),
        _ => null,
    };

    private static IEnumerable<RecusaDaParte> Recusas(ProcedimentoNoDocumento procedimento, RegraDoCatalogoCitada? regra, Contexto contexto)
    {
        string campo = $"{contexto.Lista}[{procedimento.PosicaoNoDocumento}]";

        return [.. RecusaDoTipoDeDecisao(procedimento, campo, contexto), .. RecusaDoAlvo(procedimento, campo, contexto), .. RecusaDaRegra(regra, campo, contexto)];
    }

    private static IEnumerable<RecusaDaParte> RecusaDoTipoDeDecisao(ProcedimentoNoDocumento procedimento, string campo, Contexto contexto) =>
        procedimento.TipoDeDecisao is null
            ? [contexto.Recusa($"{campo}.tipoDeDecisao", TipoDoMotivoInformado.TipoForaDoVocabulario())]
            : [];

    private static IEnumerable<RecusaDaParte> RecusaDoAlvo(ProcedimentoNoDocumento procedimento, string campo, Contexto contexto) =>
        procedimento.Alvo is { ItemChave: { } chave } && !contexto.ItensPelaChave.ContainsKey(chave)
            ? [contexto.Recusa($"{campo}.alvo", ConferenciaDosProcedimentosDeRecurso.AlvoInexistente(contexto.Codigos))]
            : [];

    private static IEnumerable<RecusaDaParte> RecusaDaRegra(RegraDoCatalogoCitada? regra, string campo, Contexto contexto) =>
        regra is { Tipo: TipoRegra.RegraProcedimentoRecurso }
            ? []
            : [contexto.Recusa($"{campo}.regra.codigo", ConferenciaDosProcedimentosDeRecurso.ProcedimentoForaDoCatalogo(contexto.Codigos))];

    private sealed record Contexto(
        string Lista,
        int PosicaoDaMae,
        CodigosDoProcedimentoDeRecurso Codigos,
        IReadOnlyDictionary<string, Guid> ItensPelaChave,
        CadastrosCitadosNoDocumento Cadastros)
    {
        internal RecusaDaParte Recusa(string campo, DomainError erro) => new(Lista, campo, PosicaoDaMae, null, erro);
    }
}
