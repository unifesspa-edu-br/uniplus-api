namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class LeituraDosProcedimentosDeRecursoDaFase
{
    internal static ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> Ler(
        FaseNoDocumento fase, IReadOnlyList<ProcedimentoNoDocumento> procedimentos, CadastrosCitadosNoDocumento cadastros)
    {
        (int PosicaoNoDocumento, ValorLido<ProcedimentoDeRecursoDeclarado?> Lido)[] lidos = procedimentos.Select(procedimento => (procedimento.PosicaoNoDocumento, Lido: LerProcedimento(fase, procedimento, cadastros))).ToArray();
        return new(
            [.. lidos.Where(static item => item.Lido.Valor is not null).Select(static item => (item.Lido.Valor!, item.PosicaoNoDocumento))],
            [.. lidos.SelectMany(static item => item.Lido.Recusas)]);
    }

    private static ValorLido<ProcedimentoDeRecursoDeclarado?> LerProcedimento(
        FaseNoDocumento fase, ProcedimentoNoDocumento procedimento, CadastrosCitadosNoDocumento cadastros)
    {
        RegraDoCatalogoCitada? regra = cadastros.Regra(procedimento.Regra.Codigo, procedimento.Regra.Versao);
        RecusaDaParte[] recusas = [.. Recusas(fase, procedimento, regra)];

        if (recusas.Length > 0 || regra is null)
        {
            return new(null, recusas);
        }

        AlvoDoProcedimentoDeRecurso? alvo = procedimento.Alvo is { AreaDoEnem: { } area } ? AlvoDoProcedimentoDeRecurso.DaArea(area) : null;
        return new(ProcedimentoDeRecursoDeclarado.De(procedimento.TipoDeDecisao, alvo, regra.Referencia), []);
    }

    private static IEnumerable<RecusaDaParte> Recusas(FaseNoDocumento fase, ProcedimentoNoDocumento procedimento, RegraDoCatalogoCitada? regra)
    {
        string campo = $"{FaseDeclarada.ListaDosProcedimentosDeRecurso}[{procedimento.PosicaoNoDocumento}]";
        if (regra is not { Tipo: TipoRegra.RegraProcedimentoRecurso })
        {
            yield return Recusa(fase, $"{campo}.regra.codigo", new DomainError(
                CodigosDoProcedimentoDeRecurso.DaFase.Desconhecido,
                "A regra citada pelo procedimento de recurso não é um procedimento de recurso do catálogo."));
        }

        if (procedimento.Alvo is { ItemChave: not null })
        {
            yield return Recusa(fase, $"{campo}.alvo", ConferenciaDosProcedimentosDeRecurso.AlvoInexistente(CodigosDoProcedimentoDeRecurso.DaFase));
        }
    }

    private static RecusaDaParte Recusa(FaseNoDocumento fase, string campo, DomainError erro) =>
        new(FaseDeclarada.ListaDosProcedimentosDeRecurso, campo, fase.PosicaoNoDocumento, null, erro);
}
