namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Os prazos de recurso da etapa. A regra que o catálogo não tem, ou que não é de prazo de recurso, e a âncora e os parâmetros que o
/// domínio recusa deixam o recurso de fora inteiro e vira parte recusada da lista de recursos da etapa. O procedimento que a leitura
/// omite também deixa a lista com parte recusada, porque a cobertura dos procedimentos lê o recurso inteiro. A publicação de
/// referência se acha entre os atos preliminares da própria etapa; a que não se acha fica sem referência, e a conferência da âncora
/// é da declaração da etapa.
/// </summary>
internal static class LeituraDosRecursosDaEtapa
{
    internal static ValorLido<IReadOnlyList<RecursoLido>> Ler(
        EtapaNoDocumento etapa,
        IReadOnlyList<ProdutoDaEtapa> produtos,
        IReadOnlyDictionary<string, Guid> itensPelaChave,
        CadastrosCitadosNoDocumento cadastros)
    {
        ArgumentNullException.ThrowIfNull(etapa);
        ArgumentNullException.ThrowIfNull(produtos);
        ArgumentNullException.ThrowIfNull(itensPelaChave);
        ArgumentNullException.ThrowIfNull(cadastros);

        (RecursoDaEtapaNoDocumento Citado, ValorLido<RecursoLido?> Lido)[] lidos =
            [.. etapa.Recursos.Select(recurso => (recurso, Recurso(recurso, produtos, itensPelaChave, cadastros)))];

        return new(
            [.. lidos.Where(static par => par.Lido.Valor is not null).Select(static par => par.Lido.Valor!)],
            [.. lidos.SelectMany(static par => par.Lido.Recusas)]);
    }

    private static ValorLido<RecursoLido?> Recurso(
        RecursoDaEtapaNoDocumento recurso,
        IReadOnlyList<ProdutoDaEtapa> produtos,
        IReadOnlyDictionary<string, Guid> itensPelaChave,
        CadastrosCitadosNoDocumento cadastros)
    {
        string campo = $"{EtapaDeclarada.ListaDosRecursos}[{recurso.PosicaoNoDocumento}]";
        ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> procedimentos = Procedimentos(recurso, campo, itensPelaChave, cadastros);
        RegraDoCatalogoCitada? regra = cadastros.Regra(recurso.Regra.Regra.Codigo, recurso.Regra.Regra.Versao);

        return regra is { Tipo: TipoRegra.RegraPrazoRecurso }
            ? Montado(recurso, campo, regra.Referencia, produtos, procedimentos)
            : new(null, [.. RecusasSemARegra(recurso, campo), .. procedimentos.Recusas]);
    }

    // A recusa do procedimento fica na lista de recursos da etapa, que é o que as conferências por etapa sabem consultar.
    private static ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> Procedimentos(
        RecursoDaEtapaNoDocumento recurso, string campo, IReadOnlyDictionary<string, Guid> itensPelaChave, CadastrosCitadosNoDocumento cadastros)
    {
        ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> lidos = LeituraDosProcedimentosDeRecurso.Ler(
            recurso.Regra.Procedimentos, $"{campo}.procedimentos", recurso.PosicaoNoDocumento, CodigosDoProcedimentoDeRecurso.DaEtapa, itensPelaChave, cadastros);

        return lidos with { Recusas = [.. lidos.Recusas.Select(static recusa => recusa with { Parte = EtapaDeclarada.ListaDosRecursos })] };
    }

    private static ValorLido<RecursoLido?> Montado(
        RecursoDaEtapaNoDocumento recurso,
        string campo,
        ReferenciaRegra regra,
        IReadOnlyList<ProdutoDaEtapa> produtos,
        ValorLido<IReadOnlyList<(ProcedimentoDeRecursoDeclarado Item, int PosicaoNoDocumento)>> procedimentos)
    {
        Result<RecursoDaEtapa> criado = RecursoDaEtapa.Criar(
            recurso.EventoInicialDoPrazo, regra, recurso.Regra.Parametros, ProdutoDeReferencia(recurso, produtos), ProcedimentosDeRecurso.De(procedimentos.Valor.Select(static par => par.Item)));

        return criado.IsSuccess
            ? new(new RecursoLido(criado.Value!, recurso.PosicaoNoDocumento, procedimentos.Valor), procedimentos.Recusas)
            : new(null, [.. criado.Errors.Select(erro => Recusa(recurso, erro.NoCaminhoDe(campo))), .. procedimentos.Recusas]);
    }

    private static IEnumerable<RecusaDaParte> RecusasSemARegra(RecursoDaEtapaNoDocumento recurso, string campo) =>
    [
        Recusa(recurso, new FieldError($"{campo}.regraCodigo", RecursoDaEtapa.RegraInvalida())),
        .. RecursoDaEtapa.RecusasDaAncoraEDosParametros(recurso.EventoInicialDoPrazo, recurso.Regra.Parametros).Select(erro => Recusa(recurso, erro.NoCaminhoDe(campo))),
    ];

    // O prazo que corre da publicação do ato se ancora num preliminar da própria etapa, e o prazo que corre da ciência não tem
    // publicação a referenciar.
    private static Guid ProdutoDeReferencia(RecursoDaEtapaNoDocumento recurso, IReadOnlyList<ProdutoDaEtapa> produtos) =>
        recurso.EventoInicialDoPrazo == EventoInicialDoPrazo.PublicacaoDoAto
            ? produtos.FirstOrDefault(produto => EhOPreliminarDeReferencia(produto, recurso))?.Id ?? Guid.Empty
            : Guid.Empty;

    private static bool EhOPreliminarDeReferencia(ProdutoDaEtapa produto, RecursoDaEtapaNoDocumento recurso) =>
        produto.Papel == PapelDaPublicacaoPrevista.Preliminar
        && string.Equals(produto.AtoCodigo, recurso.Regra.AtoAncoraCodigo, StringComparison.Ordinal);

    private static RecusaDaParte Recusa(RecursoDaEtapaNoDocumento recurso, FieldError erro) =>
        new(EtapaDeclarada.ListaDosRecursos, erro.Field!, recurso.PosicaoNoDocumento, null, erro.Error);
}
