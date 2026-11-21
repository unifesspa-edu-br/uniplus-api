namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.MotivosDeDecisao;
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

        if (recusas.Length > 0 || regra is null || procedimento.TipoDeDecisao is not { } tipo)
        {
            return new(null, recusas);
        }

        AlvoDoProcedimentoDeRecurso? alvo = procedimento.Alvo is { AreaDoEnem: { } area } ? AlvoDoProcedimentoDeRecurso.DaArea(area) : null;
        return new(ProcedimentoDeRecursoDeclarado.De(tipo, alvo, regra.Referencia), []);
    }

    private static IEnumerable<RecusaDaParte> Recusas(FaseNoDocumento fase, ProcedimentoNoDocumento procedimento, RegraDoCatalogoCitada? regra)
    {
        string campo = $"{FaseDeclarada.ListaDosProcedimentosDeRecurso}[{procedimento.PosicaoNoDocumento}]";

        return [.. RecusaDoTipoDeDecisao(fase, procedimento, campo), .. RecusaDoAlvo(fase, procedimento, campo), .. RecusaDaRegra(fase, regra, campo)];
    }

    private static IEnumerable<RecusaDaParte> RecusaDoTipoDeDecisao(FaseNoDocumento fase, ProcedimentoNoDocumento procedimento, string campo) =>
        procedimento.TipoDeDecisao is null
            ? [Recusa(fase, $"{campo}.tipoDeDecisao", TipoDoMotivoInformado.TipoForaDoVocabulario())]
            : [];

    private static IEnumerable<RecusaDaParte> RecusaDoAlvo(FaseNoDocumento fase, ProcedimentoNoDocumento procedimento, string campo) =>
        procedimento.Alvo is { ItemChave: not null }
            ? [Recusa(fase, $"{campo}.alvo", ConferenciaDosProcedimentosDeRecurso.AlvoInexistente(CodigosDoProcedimentoDeRecurso.DaFase))]
            : [];

    private static IEnumerable<RecusaDaParte> RecusaDaRegra(FaseNoDocumento fase, RegraDoCatalogoCitada? regra, string campo) =>
        regra is { Tipo: TipoRegra.RegraProcedimentoRecurso }
            ? []
            : [Recusa(fase, $"{campo}.regra.codigo", ConferenciaDosProcedimentosDeRecurso.ProcedimentoForaDoCatalogo(CodigosDoProcedimentoDeRecurso.DaFase))];

    private static RecusaDaParte Recusa(FaseNoDocumento fase, string campo, DomainError erro) =>
        new(FaseDeclarada.ListaDosProcedimentosDeRecurso, campo, fase.PosicaoNoDocumento, null, erro);
}
