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
/// oferta — a condição de atendimento fora da oferta — não tem como valer e sai da cópia; o documento sem
/// nenhuma cláusula que valha sai com ela. O documento que o modelo multiplica pelas opções de um fato
/// entra uma vez por opção ofertada.
/// </summary>
internal sealed class CopiaDosDocumentosDoModelo(
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
        PrecondicaoIfMatch precondicao,
        CancellationToken cancellationToken)
    {
        if (documentos.Count == 0)
        {
            return Result.Success();
        }

        Result<List<NoExigenciaInput>> entradas = await EntradasAsync(documentos, catalogo, cancellationToken).ConfigureAwait(false);
        if (entradas.IsFailure)
        {
            return Result.ValidationFailure(entradas.Errors);
        }

        // A raiz cujas folhas são todas da finalidade do modelo é a que o modelo substitui.
        List<NoExigencia> mantidas = [.. processo.RaizesDeExigencia.Where(raiz => !raiz.AchatarComDescendentes()
            .Where(static n => n.Tipo == TipoNo.Folha)
            .All(n => n.DocumentoExigido!.Finalidade == finalidade))];
        return await DefinicaoDosDocumentosExigidos.DefinirAsync(
                processo, entradas.Value!, mantidas, precondicao, cadastros, cancellationToken)
            .ConfigureAwait(false);
    }

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
        DocumentoExigidoDoModeloInput documento, IReadOnlyDictionary<string, DominioDeValores> ofertados)
    {
        if (documento.PorOpcao is not { } porOpcao)
        {
            return [documento];
        }

        HashSet<string> excetuadas = [.. (porOpcao.Exceto ?? []).OfType<string>()];
        IEnumerable<string> opcoes = ofertados.TryGetValue(porOpcao.FatoCodigo, out DominioDeValores? dominio) && dominio.Valores is { } valores
            ? valores.Where(v => !excetuadas.Contains(v)).Order(StringComparer.Ordinal)
            : [];
        return opcoes.Select(opcao =>
        {
            CondicaoPrecondicaoInput declarou = new(porOpcao.FatoCodigo, "IGUAL", JsonSerializer.SerializeToElement(opcao));
            return documento with
            {
                Quando = documento.Quando is { Count: > 0 } quando
                    ? [.. quando.Select(clausula => (IReadOnlyList<CondicaoPrecondicaoInput>)[.. clausula, declarou])]
                    : [[declarou]],
                PorOpcao = null,
            };
        });
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

    private async Task<Result<List<NoExigenciaInput>>> EntradasAsync(
        IReadOnlyList<DocumentoExigidoDoModeloInput> documentos,
        IReadOnlyList<FatoCandidatoView> catalogo,
        CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        List<NoExigenciaInput> entradas = [];
        IReadOnlyDictionary<string, DominioDeValores> ofertados = VocabularioDeFatos.DominiosDinamicos(processo, catalogo);
        foreach ((int i, DocumentoExigidoDoModeloInput copia) in documentos.SelectMany((d, i) => PorOpcaoOfertada(d, ofertados).Select(c => (i, c))))
        {
            DocumentoExigidoDoModeloInput documento = copia with { Quando = ClausulasOfertadas(copia.Quando, ofertados) };
            if (copia.Quando is { Count: > 0 } && documento.Quando is not { Count: > 0 })
            {
                continue;
            }

            NoExigenciaInput? entrada = await EntradaAsync(documento, $"documentosExigidos[{i}]", erros, cancellationToken).ConfigureAwait(false);
            if (entrada is not null)
            {
                entradas.Add(entrada);
            }
        }

        // As cópias de um documento multiplicado pelas opções têm a mesma fase e o mesmo tipo: a recusa sai
        // uma vez, na posição do documento no modelo.
        return erros.Count > 0
            ? Result<List<NoExigenciaInput>>.ValidationFailure([.. erros.DistinctBy(static e => (e.Field, e.Error.Code))])
            : Result<List<NoExigenciaInput>>.Success(entradas);
    }

    private async Task<NoExigenciaInput?> EntradaAsync(
        DocumentoExigidoDoModeloInput documento, string campo, List<FieldError> erros, CancellationToken cancellationToken)
    {
        FaseCronograma? fase = processo.CronogramaFases.FirstOrDefault(f => string.Equals(f.Codigo, documento.FaseCodigo, StringComparison.Ordinal));
        if (fase is null)
        {
            erros.Add(new($"{campo}.faseCodigo", new DomainError(
                AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente,
                $"O modelo exige o documento '{documento.TipoDocumentoCodigo}' na fase '{documento.FaseCodigo}', que o cronograma do processo não tem; defina o cronograma antes.")));
        }

        TipoDocumentoView? tipo = await _tipos.ObterAsync(documento.TipoDocumentoCodigo, cancellationToken).ConfigureAwait(false);
        if (tipo is null)
        {
            erros.Add(new($"{campo}.tipoDocumentoCodigo", new DomainError(
                AplicacaoDeModeloErrorCodes.TipoDeDocumentoInexistente,
                $"O modelo exige o tipo de documento '{documento.TipoDocumentoCodigo}', que o catálogo não tem.")));
        }

        return fase is null || tipo is null ? null : FolhaDoModelo(documento, fase, tipo);
    }

    private NoExigenciaInput FolhaDoModelo(DocumentoExigidoDoModeloInput documento, FaseCronograma fase, TipoDocumentoView tipo) =>
        new(
            "FOLHA",
            new ItemDocumentoExigidoInput(
                fase.Id, tipo.Id, documento.Quando is { Count: > 0 } ? "CONDICIONAL" : "GERAL", Obrigatorio: documento.Obrigatorio,
                documento.ConsequenciaIndeferimento, CondicoesDoGatilho(documento.Quando),
                [.. (documento.BasesLegais ?? []).OfType<BaseLegalDoModeloInput>().Select(static b => new BaseLegalInput(b.Referencia, b.Abrangencia, b.Status, b.Observacao))],
                IdadeMaximaEmissao: null, FormatosPermitidos: JsonSerializer.SerializeToElement("QUALQUER"), TamanhoMaximoBytes: null,
                Finalidade: EstruturaFormulario.ParaToken(finalidade)),
            QuantidadeMinima: null, Consequencia: null, BasesLegais: null, Filhos: null);

    // As cláusulas do predicado do modelo como linhas do gatilho: uma cláusula por índice, o valor como JSON.
    private static List<CondicaoGatilhoInput> CondicoesDoGatilho(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? quando) =>
        [.. (quando ?? []).SelectMany(static (clausula, indice) => clausula.Select(
            c => new CondicaoGatilhoInput(indice, c.Fato, c.Operador, c.Valor.GetRawText())))];
}
