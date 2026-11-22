namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Copia o conjunto padrão de documentos exigidos do modelo para a árvore do processo, resolvendo a fase
/// pelo código no cronograma e o tipo pelo código no catálogo. Os documentos da finalidade do modelo que o
/// processo já tinha são trocados pelos do modelo, como o formulário da finalidade; os das outras
/// finalidades ficam. Modelo sem conjunto não mexe na árvore. A cláusula que cita valor que o processo não
/// oferta — a condição de atendimento fora das condições ofertadas — não tem como valer e sai da cópia; o documento sem
/// nenhuma cláusula que valha sai com ela. O documento que o modelo multiplica pelas opções de um fato
/// entra uma vez por opção ofertada.
/// </summary>
internal sealed class SelecaoDosDocumentosDoModelo(
    ProcessoSeletivo processo,
    FinalidadeFormulario finalidade,
    CadastrosDosDocumentosExigidos cadastros)
{
    private readonly LeituraMemoizada<string, TipoDocumentoView> _tipos = new(
        (codigo, cancellationToken) => cadastros.TiposDeDocumento.ObterVivoPorCodigoAsync(codigo, cancellationToken),
        StringComparer.Ordinal);

    public async Task<Result> CopiarAsync(
        IReadOnlyList<DocumentoExigidoDoModeloInput> documentos,
        IReadOnlyList<FatoCandidatoView> catalogo,
        CancellationToken cancellationToken)
    {
        if (documentos.Count == 0)
        {
            return Result.Success();
        }

        Result<List<RegraDeComprovacaoInformada>> entradas = await EntradasAsync(documentos, catalogo, cancellationToken).ConfigureAwait(false);
        if (entradas.IsFailure)
        {
            return Result.ValidationFailure(entradas.Errors);
        }

        return await DefinicaoDosDocumentosExigidos.DefinirAsync(
                processo, entradas.Value!, RaizesQueOModeloNaoSubstitui(), cadastros, cancellationToken)
            .ConfigureAwait(false);
    }

    // A raiz cujas folhas são todas da finalidade do modelo é a que o modelo substitui.
    private List<NoExigencia> RaizesQueOModeloNaoSubstitui() =>
        [.. processo.RaizesDeExigencia.Where(raiz => !raiz.AchatarComDescendentes()
            .Where(static n => n.Tipo == TipoNo.Folha)
            .All(n => n.DocumentoExigido!.Finalidade == finalidade))];

    /// <summary>
    /// O campo da recusa no conjunto de documentos do modelo: o da fase e o do tipo já trazem a posição; os
    /// da construção, não.
    /// </summary>
    public static string NoConjuntoDeDocumentos(string? campo) =>
        string.IsNullOrEmpty(campo) ? "documentosExigidos"
        : campo.StartsWith("documentosExigidos", StringComparison.Ordinal) ? campo
        : $"documentosExigidos.{campo}";

    /// <summary>
    /// O documento uma vez por opção que o processo oferece para o fato, salvo as excetuadas: cada cópia
    /// acrescenta a cada cláusula a declaração da opção, e vale só para quem a declarou. A opção que o
    /// processo não oferece não gera cópia. O documento que não se multiplica segue como está.
    /// </summary>
    public static IEnumerable<DocumentoExigidoDoModeloInput> PorOpcaoOfertada(
        DocumentoExigidoDoModeloInput documento, IReadOnlyDictionary<string, DominioDeValores> ofertados) =>
        documento.PorOpcao is { } porOpcao
            ? OpcoesOfertadas(porOpcao, ofertados).Select(opcao => ParaAOpcao(documento, porOpcao.FatoCodigo, opcao))
            : [documento];

    private static IEnumerable<string> OpcoesOfertadas(ExigenciaPorOpcaoInput porOpcao, IReadOnlyDictionary<string, DominioDeValores> ofertados)
    {
        HashSet<string> excetuadas = [.. (porOpcao.Exceto ?? []).OfType<string>()];
        return ofertados.TryGetValue(porOpcao.FatoCodigo, out DominioDeValores? dominio) && dominio.Valores is { } valores
            ? valores.Where(v => !excetuadas.Contains(v)).Order(StringComparer.Ordinal)
            : [];
    }

    private static DocumentoExigidoDoModeloInput ParaAOpcao(DocumentoExigidoDoModeloInput documento, string fatoCodigo, string opcao)
    {
        CondicaoPrecondicaoInput declarou = new(fatoCodigo, "IGUAL", JsonSerializer.SerializeToElement(opcao));
        return documento with
        {
            Quando = documento.Quando is { Count: > 0 } quando
                ? [.. quando.Select(clausula => (IReadOnlyList<CondicaoPrecondicaoInput>)[.. clausula, declarou])]
                : [[declarou]],
            PorOpcao = null,
        };
    }

    /// <summary>
    /// As cláusulas cujos valores o processo oferta: a que afirma (IGUAL) valor fora do que ele oferece para
    /// o fato não vale. As de outro operador seguem intactas para o validador do gatilho: negar um valor não
    /// ofertado não é o mesmo que ele não existir.
    /// </summary>
    public static List<IReadOnlyList<CondicaoPrecondicaoInput>>? ClausulasOfertadas(
        IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? quando, IReadOnlyDictionary<string, DominioDeValores> ofertados) =>
        quando is null
            ? null
            : [.. quando.Where(clausula => clausula.All(c =>
                !ofertados.TryGetValue(c.Fato, out DominioDeValores? dominio)
                || dominio.Valores is null
                || c.Operador != "IGUAL"
                || c.Valor.ValueKind != JsonValueKind.String
                || dominio.Valores.Contains(c.Valor.GetString()!)))];

    private async Task<Result<List<RegraDeComprovacaoInformada>>> EntradasAsync(
        IReadOnlyList<DocumentoExigidoDoModeloInput> documentos,
        IReadOnlyList<FatoCandidatoView> catalogo,
        CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        List<RegraDeComprovacaoInformada> entradas = [];
        IReadOnlyDictionary<string, DominioDeValores> ofertados = VocabularioDeFatos.DominiosDinamicos(processo, catalogo);
        foreach ((int i, DocumentoExigidoDoModeloInput copia) in documentos.SelectMany((d, i) => PorOpcaoOfertada(d, ofertados).Select(c => (i, c))))
        {
            if (ComClausulasOfertadas(copia, ofertados) is { } documento
                && await EntradaAsync(documento, $"documentosExigidos[{i}]", erros, cancellationToken).ConfigureAwait(false) is { } entrada)
            {
                entradas.Add(entrada);
            }
        }

        return ResultadoDasEntradas(erros, entradas);
    }

    // O documento sem nenhuma cláusula que valha sai da cópia, junto com elas.
    private static DocumentoExigidoDoModeloInput? ComClausulasOfertadas(
        DocumentoExigidoDoModeloInput copia, IReadOnlyDictionary<string, DominioDeValores> ofertados)
    {
        DocumentoExigidoDoModeloInput documento = copia with { Quando = ClausulasOfertadas(copia.Quando, ofertados) };
        return copia.Quando is { Count: > 0 } && documento.Quando is not { Count: > 0 } ? null : documento;
    }

    // As cópias de um documento multiplicado pelas opções têm a mesma fase e o mesmo tipo: a recusa sai
    // uma vez, na posição do documento no modelo.
    private static Result<List<RegraDeComprovacaoInformada>> ResultadoDasEntradas(List<FieldError> erros, List<RegraDeComprovacaoInformada> entradas) =>
        erros.Count > 0
            ? Result<List<RegraDeComprovacaoInformada>>.ValidationFailure([.. erros.DistinctBy(static e => (e.Field, e.Error.Code))])
            : Result<List<RegraDeComprovacaoInformada>>.Success(entradas);

    private async Task<RegraDeComprovacaoInformada?> EntradaAsync(
        DocumentoExigidoDoModeloInput documento, string campo, List<FieldError> erros, CancellationToken cancellationToken)
    {
        FaseCronograma? fase = processo.CronogramaFases.FirstOrDefault(f => string.Equals(f.Codigo, documento.FaseCodigo, StringComparison.Ordinal));
        TipoDocumentoView? tipo = await _tipos.ObterAsync(documento.TipoDocumentoCodigo, cancellationToken).ConfigureAwait(false);
        if (fase is null)
        {
            erros.Add(RecusaDaFase(documento, campo));
        }

        if (tipo is null)
        {
            erros.Add(RecusaDoTipo(documento, campo));
        }

        return fase is null || tipo is null ? null : FolhaDoModelo(documento, fase, tipo);
    }

    private static FieldError RecusaDaFase(DocumentoExigidoDoModeloInput documento, string campo) =>
        new($"{campo}.faseCodigo", new DomainError(
            AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente,
            $"O modelo exige o documento '{documento.TipoDocumentoCodigo}' na fase '{documento.FaseCodigo}', que o cronograma do processo não tem; defina o cronograma antes."));

    private static FieldError RecusaDoTipo(DocumentoExigidoDoModeloInput documento, string campo) =>
        new($"{campo}.tipoDocumentoCodigo", new DomainError(
            AplicacaoDeModeloErrorCodes.TipoDeDocumentoInexistente,
            $"O modelo exige o tipo de documento '{documento.TipoDocumentoCodigo}', que o catálogo não tem."));

    private RegraDeComprovacaoInformada FolhaDoModelo(DocumentoExigidoDoModeloInput documento, FaseCronograma fase, TipoDocumentoView tipo) =>
        new("FOLHA", DocumentoDaFolha(documento, fase, tipo), Cardinalidade: null, Indeferimento: null, Filhos: null);

    private ItemDocumentoExigidoInput DocumentoDaFolha(DocumentoExigidoDoModeloInput documento, FaseCronograma fase, TipoDocumentoView tipo) =>
        new(
            new TipoDocumentoDaExigenciaInput(tipo.Id),
            new MomentoDaExigenciaInput(fase.Id, Finalidade: EstruturaFormulario.ParaToken(finalidade)),
            new AplicabilidadeDaExigenciaInput(
                documento.Quando is { Count: > 0 } ? "CONDICIONAL" : "GERAL", CondicoesDoGatilho(documento.Quando)),
            new IndeferimentoDoDocumentoInput(documento.Obrigatorio, documento.ConsequenciaIndeferimento, BasesLegaisDoModelo(documento)),
            new RequisitosDoDocumentoInput(JsonSerializer.SerializeToElement("QUALQUER"), null, null, null));

    private static List<BaseLegalInput> BasesLegaisDoModelo(DocumentoExigidoDoModeloInput documento) =>
        [.. (documento.BasesLegais ?? []).OfType<BaseLegalDoModeloInput>()
            .Select(static b => new BaseLegalInput(b.Referencia, b.Abrangencia, b.Status, b.Observacao))];

    // As cláusulas do predicado do modelo como linhas do gatilho: uma cláusula por índice, o valor como JSON.
    private static List<CondicaoGatilhoInput> CondicoesDoGatilho(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? quando) =>
        [.. (quando ?? []).SelectMany(static (clausula, indice) => clausula.Select(
            c => new CondicaoGatilhoInput(indice, c.Fato, c.Operador, c.Valor.GetRawText())))];
}
