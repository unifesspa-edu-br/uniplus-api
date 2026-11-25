namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Text.Json;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

/// <summary>
/// Copia o conjunto padrão de documentos exigidos do modelo para as regras do processo, resolvendo a fase
/// pelo código no cronograma e o tipo pelo código no catálogo. Cada grupo de documentos vira uma regra de
/// quantidade mínima de itens, com os documentos do grupo como itens; o grupo sem nenhum documento que valha
/// sai da cópia, e que a quantidade caiba nos itens e que eles sejam da mesma fase quem confere é o processo. Os documentos da finalidade do modelo que o
/// processo já tinha são trocados pelos do modelo, como o formulário da finalidade; os das outras
/// finalidades ficam. Modelo sem conjunto não altera as regras. A cláusula que cita valor que o processo não
/// oferta — a condição de atendimento fora das condições ofertadas — não tem como valer e sai da cópia; o documento sem
/// nenhuma cláusula que valha sai com ela. O documento que o modelo multiplica pelas opções de um fato
/// entra uma vez por opção ofertada. O prazo de emissão e a validade que o modelo escolheu vão junto, com a
/// fase de cada referência trocada pela do cronograma do processo (ver <see cref="CopiaDoPrazoEValidadeDoModelo"/>).
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
        ConjuntoDeDocumentosDoModeloInput conjunto, IReadOnlyList<FatoCandidatoView> catalogo, CancellationToken cancellationToken)
    {
        if (!conjunto.TodosOsDocumentos().Any())
        {
            return Result.Success();
        }

        Result<List<RegraCopiadaDoModelo>> copiadas = await CopiasAsync(conjunto, catalogo, cancellationToken).ConfigureAwait(false);
        return copiadas.IsFailure
            ? Result.ValidationFailure(copiadas.Errors)
            : await DefinirAsync(copiadas.Value!, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result> DefinirAsync(List<RegraCopiadaDoModelo> copiadas, CancellationToken cancellationToken)
    {
        Result definida = await DefinicaoDosDocumentosExigidos.DefinirAsync(
                processo, [.. copiadas.Select(static c => c.Regra)], RegrasSemGrupoQueOModeloNaoSubstitui(), cadastros, cancellationToken)
            .ConfigureAwait(false);
        return definida.IsSuccess
            ? definida
            : Result.ValidationFailure([.. definida.Errors.Select(erro => RegraCopiadaDoModelo.NoModelo(erro, copiadas))]);
    }

    // O modelo substitui as regras sem grupo cujos documentos pertencem à sua finalidade.
    private List<RegraDeComprovacao> RegrasSemGrupoQueOModeloNaoSubstitui() =>
        [.. processo.RegrasDeComprovacaoSemGrupo.Where(regraSemGrupo => !regraSemGrupo.EnumerarRegraEItens()
            .Where(static regraDeDocumento => regraDeDocumento.Forma == FormaDeComprovacao.Documento)
            .All(regraDeDocumento => regraDeDocumento.DocumentoExigido!.Finalidade == finalidade))];

    /// <summary>
    /// O campo da recusa no conjunto de documentos do modelo: o da fase, o do tipo e o que o processo
    /// recusa na cópia já trazem a posição; o que não a traz fica sob o conjunto.
    /// </summary>
    public static FieldError NoConjuntoDeDocumentos(FieldError causa) =>
        causa.Field?.StartsWith(ConjuntoDeDocumentosDoModeloInput.Caminho, StringComparison.Ordinal) == true
            ? causa
            : causa.NoCaminhoDoItem(ConjuntoDeDocumentosDoModeloInput.Caminho);

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

    private async Task<Result<List<RegraCopiadaDoModelo>>> CopiasAsync(
        ConjuntoDeDocumentosDoModeloInput conjunto,
        IReadOnlyList<FatoCandidatoView> catalogo,
        CancellationToken cancellationToken)
    {
        List<FieldError> erros = [];
        List<RegraCopiadaDoModelo> copiadas = [];
        IReadOnlyDictionary<string, DominioDeValores> ofertados = VocabularioDeFatos.DominiosDinamicos(processo, catalogo);
        foreach ((int i, DocumentoExigidoDoModeloInput documento) in (conjunto.DocumentosExigidos ?? []).Index())
        {
            copiadas.AddRange(await DocumentoAsync(documento, ConjuntoDeDocumentosDoModeloInput.CaminhoDoDocumento(i), ofertados, erros, cancellationToken).ConfigureAwait(false));
        }

        foreach ((int g, GrupoDeDocumentosDoModeloInput grupo) in (conjunto.GruposDeDocumentos ?? []).Index())
        {
            copiadas.AddRange(await GrupoAsync(grupo, g, ofertados, erros, cancellationToken).ConfigureAwait(false));
        }

        return ResultadoDasCopias(erros, copiadas);
    }

    // O documento do modelo vira uma regra por opção ofertada; as que não valem para o processo saem.
    private async Task<List<RegraCopiadaDoModelo>> DocumentoAsync(
        DocumentoExigidoDoModeloInput documento,
        string caminho,
        IReadOnlyDictionary<string, DominioDeValores> ofertados,
        List<FieldError> erros,
        CancellationToken cancellationToken)
    {
        List<RegraCopiadaDoModelo> regras = [];
        foreach (DocumentoExigidoDoModeloInput copia in PorOpcaoOfertada(documento, ofertados))
        {
            if (ComClausulasOfertadas(copia, ofertados) is { } comClausulas
                && await EntradaAsync(comClausulas, caminho, erros, cancellationToken).ConfigureAwait(false) is { } entrada)
            {
                regras.Add(new RegraCopiadaDoModelo(entrada, caminho, []));
            }
        }

        return regras;
    }

    // O grupo vira uma regra de quantidade mínima de itens; sem nenhum documento que valha, sai da cópia.
    private async Task<List<RegraCopiadaDoModelo>> GrupoAsync(
        GrupoDeDocumentosDoModeloInput grupo,
        int posicao,
        IReadOnlyDictionary<string, DominioDeValores> ofertados,
        List<FieldError> erros,
        CancellationToken cancellationToken)
    {
        List<RegraCopiadaDoModelo> itens = [];
        foreach ((int j, DocumentoExigidoDoModeloInput documento) in (grupo.Documentos ?? []).Index())
        {
            string caminho = ConjuntoDeDocumentosDoModeloInput.CaminhoDoDocumentoDoGrupo(posicao, j);
            itens.AddRange(await DocumentoAsync(documento, caminho, ofertados, erros, cancellationToken).ConfigureAwait(false));
        }

        return itens.Count == 0
            ? []
            : [new RegraCopiadaDoModelo(RegraDoGrupo(grupo, itens), ConjuntoDeDocumentosDoModeloInput.CaminhoDoGrupo(posicao), [.. itens.Select(static i => i.Caminho)])];
    }

    private static RegraDeComprovacaoInformada RegraDoGrupo(GrupoDeDocumentosDoModeloInput grupo, List<RegraCopiadaDoModelo> itens) =>
        new("QUANTIDADE_MINIMA_DE_ITENS",
            Cardinalidade: new CardinalidadeQualificadaInput(grupo.QuantidadeMinima),
            Indeferimento: IndeferimentoDoGrupo(grupo.Indeferimento),
            Itens: [.. itens.Select(static i => i.Regra)]);

    private static IndeferimentoDoGrupoInput? IndeferimentoDoGrupo(IndeferimentoDoGrupoDeDocumentosDoModeloInput? indeferimento) =>
        indeferimento is null
            ? null
            : new IndeferimentoDoGrupoInput(
                indeferimento.Consequencia,
                [.. (indeferimento.BasesLegais ?? []).OfType<BaseLegalDoModeloInput>().Select(static b => new BaseLegalInput(b.Referencia, b.Abrangencia, b.Situacao, b.Observacao))]);

    // O documento sem nenhuma cláusula que valha sai da cópia, junto com elas.
    private static DocumentoExigidoDoModeloInput? ComClausulasOfertadas(
        DocumentoExigidoDoModeloInput copia, IReadOnlyDictionary<string, DominioDeValores> ofertados)
    {
        DocumentoExigidoDoModeloInput documento = copia with { Quando = ClausulasOfertadas(copia.Quando, ofertados) };
        return copia.Quando is { Count: > 0 } && documento.Quando is not { Count: > 0 } ? null : documento;
    }

    // As cópias de um documento multiplicado pelas opções têm a mesma fase e o mesmo tipo: a recusa sai
    // uma vez, na posição do documento no modelo.
    private static Result<List<RegraCopiadaDoModelo>> ResultadoDasCopias(List<FieldError> erros, List<RegraCopiadaDoModelo> copiadas) =>
        erros.Count > 0
            ? Result<List<RegraCopiadaDoModelo>>.ValidationFailure([.. erros.DistinctBy(static e => (e.Field, e.Error.Code))])
            : Result<List<RegraCopiadaDoModelo>>.Success(copiadas);

    private async Task<RegraDeComprovacaoInformada?> EntradaAsync(
        DocumentoExigidoDoModeloInput documento, string campo, List<FieldError> erros, CancellationToken cancellationToken)
    {
        FaseCronograma? fase = processo.CronogramaFases.FirstOrDefault(f => string.Equals(f.Codigo, documento.FaseCodigo, StringComparison.Ordinal));
        TipoDocumentoView? tipo = await _tipos.ObterAsync(documento.TipoDocumentoCodigo, cancellationToken).ConfigureAwait(false);
        Result<PrazoEValidadeCopiados> requisitos = CopiaDoPrazoEValidadeDoModelo.Copiar(documento.Requisitos, processo.CronogramaFases, campo);
        erros.AddRange(RecusasDaFaseEDoTipo(documento, campo, fase, tipo).Concat(requisitos.Errors));
        return fase is not null && tipo is not null && requisitos.IsSuccess
            ? RegraDeDocumentoDoModelo(documento, fase, tipo, requisitos.Value!)
            : null;
    }

    private static IEnumerable<FieldError> RecusasDaFaseEDoTipo(
        DocumentoExigidoDoModeloInput documento, string campo, FaseCronograma? fase, TipoDocumentoView? tipo)
    {
        if (fase is null)
        {
            yield return RecusaDaFase(documento, campo);
        }

        if (tipo is null)
        {
            yield return RecusaDoTipo(documento, campo);
        }
    }

    private static FieldError RecusaDaFase(DocumentoExigidoDoModeloInput documento, string campo) =>
        new($"{campo}.faseCodigo", new DomainError(
            AplicacaoDeModeloErrorCodes.FaseDoDocumentoInexistente,
            $"O modelo exige o documento '{documento.TipoDocumentoCodigo}' na fase '{documento.FaseCodigo}', que o cronograma do processo não tem; defina o cronograma antes."));

    private static FieldError RecusaDoTipo(DocumentoExigidoDoModeloInput documento, string campo) =>
        new($"{campo}.tipoDocumentoCodigo", new DomainError(
            AplicacaoDeModeloErrorCodes.TipoDeDocumentoInexistente,
            $"O modelo exige o tipo de documento '{documento.TipoDocumentoCodigo}', que o catálogo não tem."));

    private RegraDeComprovacaoInformada RegraDeDocumentoDoModelo(
        DocumentoExigidoDoModeloInput documento, FaseCronograma fase, TipoDocumentoView tipo, PrazoEValidadeCopiados prazoEValidade) =>
        new("DOCUMENTO", DocumentoInformadoDoModelo(documento, fase, tipo, prazoEValidade), Cardinalidade: null, Indeferimento: null, Itens: null);

    private ItemDocumentoExigidoInput DocumentoInformadoDoModelo(
        DocumentoExigidoDoModeloInput documento, FaseCronograma fase, TipoDocumentoView tipo, PrazoEValidadeCopiados prazoEValidade) =>
        new(
            new TipoDocumentoDaExigenciaInput(tipo.Id),
            new MomentoDaExigenciaInput(fase.Id, Finalidade: finalidade.Codigo()),
            new AplicabilidadeDaExigenciaInput(
                documento.Quando is { Count: > 0 } ? "CONDICIONAL" : "GERAL", CondicoesDoGatilho(documento.Quando)),
            IndeferimentoDoModelo(documento.Indeferimento),
            new RequisitosDoDocumentoInput(
                JsonSerializer.SerializeToElement("QUALQUER"), null, prazoEValidade.Prazo, prazoEValidade.Validade, null));

    private static IndeferimentoDoDocumentoInput IndeferimentoDoModelo(IndeferimentoDoDocumentoDoModeloInput? indeferimento) =>
        new(indeferimento?.Obrigatorio ?? false, indeferimento?.Consequencia, BasesLegaisDoModelo(indeferimento));

    private static List<BaseLegalInput> BasesLegaisDoModelo(IndeferimentoDoDocumentoDoModeloInput? indeferimento) =>
        [.. (indeferimento?.BasesLegais ?? []).OfType<BaseLegalDoModeloInput>()
            .Select(static b => new BaseLegalInput(b.Referencia, b.Abrangencia, b.Situacao, b.Observacao))];

    // As cláusulas do predicado do modelo como linhas do gatilho: uma cláusula por índice, o valor como JSON.
    private static List<CondicaoGatilhoInput> CondicoesDoGatilho(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? quando) =>
        [.. (quando ?? []).SelectMany(static (clausula, indice) => clausula.Select(
            c => new CondicaoGatilhoInput(indice, c.Fato, c.Operador, c.Valor.GetRawText())))];
}
