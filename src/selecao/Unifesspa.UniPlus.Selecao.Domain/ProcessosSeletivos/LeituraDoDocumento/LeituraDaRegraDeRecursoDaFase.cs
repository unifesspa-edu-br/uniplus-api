namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A regra de recurso da fase. A regra que o catálogo não tem, ou que não é a de prazo ancorado em ato, e os parâmetros do prazo que o
/// domínio recusa recusam a regra inteira: a fase fica sem ela e a parte recusada suspende o que depende dela (a âncora, os
/// procedimentos e a pendência da contagem de prazos). O procedimento cuja regra o catálogo não tem, ou cujo alvo a fase não tem,
/// é omitido: a regra é adotada com os procedimentos que ficaram, e a lista de procedimentos suspende a cobertura. Não confere a
/// âncora nem a coerência dos procedimentos.
/// </summary>
internal static class LeituraDaRegraDeRecursoDaFase
{
    internal static ValorLido<RegraDeRecursoLida> Ler(
        FaseNoDocumento fase, IReadOnlyList<ProdutoDaFase> produtos, CadastrosCitadosNoDocumento cadastros)
    {
        if (fase.RegraDeRecurso is not { } declarada)
        {
            return new(RegraDeRecursoLida.Nenhuma, []);
        }

        RegraDoCatalogoCitada? regra = cadastros.Regra(declarada.Regra.Codigo, declarada.Regra.Versao);
        ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> procedimentos =
            LeituraDosProcedimentosDeRecursoDaFase.Ler(fase, declarada.Procedimentos, cadastros);
        RecusaDaParte[] recusasDaRegra = [.. RegraInvalida(fase, regra)];
        return recusasDaRegra.Length > 0
            ? RegraRecusada(fase, declarada, recusasDaRegra, procedimentos.Recusas)
            : RegraMontada(fase, declarada, regra!, produtos, procedimentos);
    }

    private static ValorLido<RegraDeRecursoLida> RegraRecusada(
        FaseNoDocumento fase, RegraDeRecursoNoDocumento declarada, RecusaDaParte[] recusasDaRegra, IReadOnlyList<RecusaDaParte> procedimentos)
    {
        RecusaDaParte[] parametros = [.. RegraRecursoFase.RecusasDosParametros(declarada.Parametros)
            .Select(erro => Recusa(fase, FaseDeclarada.CampoDaRegraDeRecurso, erro))];
        return new(RegraDeRecursoLida.Nenhuma, [.. recusasDaRegra, .. parametros, .. procedimentos]);
    }

    private static ValorLido<RegraDeRecursoLida> RegraMontada(
        FaseNoDocumento fase, RegraDeRecursoNoDocumento declarada, RegraDoCatalogoCitada regra,
        IReadOnlyList<ProdutoDaFase> produtos, ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> procedimentos)
    {
        ValorLido<RegraRecursoFase?> montada = Montada(fase, declarada, regra, produtos, procedimentos.Valor);
        return new(montada.Valor is null ? RegraDeRecursoLida.Nenhuma : new(montada.Valor, procedimentos.Valor), [.. montada.Recusas, .. procedimentos.Recusas]);
    }

    private static ValorLido<RegraRecursoFase?> Montada(
        FaseNoDocumento fase, RegraDeRecursoNoDocumento declarada, RegraDoCatalogoCitada regra, IReadOnlyList<ProdutoDaFase> produtos, IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)> procedimentos)
    {
        Guid ancora = produtos.FirstOrDefault(produto => EhOPreliminarAncora(produto, declarada))?.Id ?? Guid.Empty;
        Result<RegraRecursoFase> criada = RegraRecursoFase.Criar(regra.Referencia, declarada.Parametros, ancora, ProcedimentosDeRecurso.De(procedimentos.Select(static par => par.Item)));

        return criada.IsSuccess ? new(criada.Value, []) : new(null, [.. criada.Errors.Select(erro => Recusa(fase, FaseDeclarada.CampoDaRegraDeRecurso, erro))]);
    }

    // A âncora é um preliminar da própria fase: o mesmo ato pode ser publicado nos dois papéis, e o prazo conta do preliminar.
    private static bool EhOPreliminarAncora(ProdutoDaFase produto, RegraDeRecursoNoDocumento declarada) =>
        produto.Papel == PapelDaPublicacaoPrevista.Preliminar && string.Equals(produto.AtoCodigo, declarada.AtoAncoraCodigo, StringComparison.Ordinal);

    private static IEnumerable<RecusaDaParte> RegraInvalida(FaseNoDocumento fase, RegraDoCatalogoCitada? regra) =>
        regra is { Tipo: TipoRegra.RegraPrazoRecurso } && regra.Referencia.Codigo == RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo()
            ? []
            : [Recusa(fase, FaseDeclarada.CampoDaRegraDeRecurso, $"{FaseDeclarada.CampoDaRegraDeRecurso}.regraCodigo", new DomainError(
                FaseCronogramaErrorCodes.RegraDeRecursoInvalida,
                "A regra indicada não permite contar o prazo de recurso a partir de uma publicação."))];

    private static RecusaDaParte Recusa(FaseNoDocumento fase, string parte, string campo, DomainError erro) =>
        new(parte, campo, fase.PosicaoNoDocumento, null, erro);

    private static RecusaDaParte Recusa(FaseNoDocumento fase, string parte, FieldError erro) =>
        Recusa(fase, parte, erro.NoCaminhoDe(parte).Field ?? parte, erro.Error);
}
