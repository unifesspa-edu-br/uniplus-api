namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.MotivosDeDecisao;
using Domain.ProcessosSeletivos.Recursos;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Monta a regra de recurso de uma fase. A regra referenciada é resolvida pelo mesmo
/// <see cref="IRegraCatalogoReader"/> das demais definições, e a referência sai dos valores RESOLVIDOS do
/// catálogo, nunca ecoados do payload: é o que faz o hash bater por construção.
/// </summary>
internal sealed class MontagemDaRegraDeRecursoDaFase(
    IRegraCatalogoReader regraCatalogoReader,
    AtosConsultadosNaEdicao atos,
    IReadOnlySet<(string Fase, string Ato)> ancorasJaDeclaradas,
    bool processoTemNotaDoEnem)
{
    private readonly ConferenciaDaAncoraDoRecurso _conferenciaDaAncora = new(atos);

    private readonly DeclaracaoDosProcedimentosDeRecurso _procedimentos = new(
        regraCatalogoReader.ObterAsync,
        CodigosDoProcedimentoDeRecurso.DaFase);

    /// <summary>A regra de recurso que a fase declara; a recusa se acumula com as das demais partes da fase.</summary>
    public async Task<Result<RegraRecursoFase>> MontarAsync(
        RegraRecursoFaseInput declarada,
        int indice,
        string faseCodigo,
        IReadOnlyList<ProdutoDaFase> produtos,
        IReadOnlyList<string?>? tiposDeDecisao,
        CancellationToken cancellationToken)
    {
        string campo = $"fases[{indice}].regraRecurso";
        ProdutoDaFase? ancora = ProdutoAncora(produtos, declarada.AtoAncoraCodigo);
        Result<RegraRecursoFase> regra = await RegraDeclaradaAsync(declarada, campo, ancora, tiposDeDecisao, cancellationToken)
            .ConfigureAwait(false);
        if (regra.IsFailure || ancora is null)
        {
            return regra;
        }

        return await ConferirAncoraAsync(regra, ancora, campo, faseCodigo, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Result<RegraRecursoFase>> RegraDeclaradaAsync(
        RegraRecursoFaseInput declarada,
        string campo,
        ProdutoDaFase? ancora,
        IReadOnlyList<string?>? tiposDeDecisao,
        CancellationToken cancellationToken)
    {
        Result<ReferenciaRegra> referencia = await ReferenciaAsync(declarada, campo, cancellationToken).ConfigureAwait(false);
        return referencia.IsFailure
            ? Result<RegraRecursoFase>.ValidationFailure(referencia.Errors)
            : await RegraComReferenciaAsync(declarada, campo, referencia.Value!, ancora, tiposDeDecisao, cancellationToken)
                .ConfigureAwait(false);
    }

    private async Task<Result<RegraRecursoFase>> RegraComReferenciaAsync(
        RegraRecursoFaseInput declarada,
        string campo,
        ReferenciaRegra referencia,
        ProdutoDaFase? ancora,
        IReadOnlyList<string?>? tiposDeDecisao,
        CancellationToken cancellationToken)
    {
        Result<ProcedimentosDeRecurso> procedimentos = await _procedimentos
            .DeclararAsync(declarada.Procedimentos, ContextoDosProcedimentos(tiposDeDecisao), $"{campo}.procedimentos", cancellationToken)
            .ConfigureAwait(false);
        if (procedimentos.IsFailure)
        {
            return Result<RegraRecursoFase>.ValidationFailure(procedimentos.Errors);
        }

        Result<RegraRecursoFase> regra = RegraRecursoFase.Criar(referencia, ParametrosDoPrazo(declarada), ancora?.Id ?? Guid.Empty, procedimentos.Value);
        return regra.IsFailure
            ? Result<RegraRecursoFase>.ValidationFailure([.. regra.Errors.Select(erro => new FieldError(campo, erro.Error))])
            : regra;
    }

    private async Task<Result<RegraRecursoFase>> ConferirAncoraAsync(Result<RegraRecursoFase> regra,
        ProdutoDaFase ancora, string campo, string faseCodigo, CancellationToken cancellationToken)
    {
        DomainError? recusa = await _conferenciaDaAncora.ConferirAsync(
            ancora.AtoCodigo, RotuloDoDonoDaAncora.DaFase,
            atoCodigo => ancorasJaDeclaradas.Contains((faseCodigo, atoCodigo)),
            (atoCodigo, token) => atos.Congelados.AncoraDaFaseJaPublicadaAsync(faseCodigo, atoCodigo, token),
            cancellationToken).ConfigureAwait(false);
        return recusa is null ? regra : InterrupcaoDaFase.Com<RegraRecursoFase>($"{campo}.atoAncoraCodigo", recusa);
    }

    private static Result<T> Recusa<T>(string campo, DomainError erro) =>
        Result<T>.ValidationFailure([new FieldError(campo, erro)]);

    // O lugar do período de recurso é a própria fase: ela declara os tipos de decisão que produz, e o produtor fase não tem itens de
    // avaliação. A área do ENEM vale em qualquer período de recurso do processo que importa a nota do ENEM.
    private ContextoDosProcedimentosDeRecurso ContextoDosProcedimentos(IReadOnlyList<string?>? tiposDeDecisao) => new(
        TiposDeDecisaoDeclarados.Aceitos(tiposDeDecisao),
        new HashSet<Guid>(),
        processoTemNotaDoEnem);

    private async Task<Result<ReferenciaRegra>> ReferenciaAsync(
        RegraRecursoFaseInput declarada, string campo, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(declarada.RegraCodigo, declarada.RegraVersao, cancellationToken).ConfigureAwait(false);
        if (regra is null
            || regra.Tipo != TipoRegra.RegraPrazoRecurso
            || regra.Codigo != RegraPrazoRecursoCodigo.AncoradoEmAto)
        {
            return Recusa<ReferenciaRegra>($"{campo}.regraCodigo", new DomainError(
                "RegraRecursoFase.RegraCatalogoInvalida",
                $"A regra {declarada.RegraCodigo}/{declarada.RegraVersao} não é a regra {RegraPrazoRecursoCodigo.AncoradoEmAto} do tipo regra_prazo_recurso."));
        }

        return Result<ReferenciaRegra>.Success(regra.Referencia());
    }

    // A âncora é resolvida DENTRO dos produtos desta fase: o mesmo tipo de ato pode ser publicado por outra fase, e é a
    // identidade da linha, não o código, que diz de qual publicação o prazo conta. Quando o código não corresponde a
    // produto algum, a âncora fica vazia e o domínio recusa nomeando os preliminares disponíveis.
    // Entre os PRELIMINARES: a fase publica o mesmo ato em dois papéis, e é da publicação preliminar que o
    // prazo corre. Buscar só pelo código devolveria o definitivo quando ele viesse primeiro, e a fase seria
    // recusada por ancorar onde não se pode.
    private static ProdutoDaFase? ProdutoAncora(IReadOnlyList<ProdutoDaFase> produtos, string atoAncoraCodigo) =>
        produtos.FirstOrDefault(p => p.Papel == PapelProdutoFase.Preliminar
            && string.Equals(p.AtoCodigo, atoAncoraCodigo, StringComparison.Ordinal));

    private static ParametrosRegraPrazoRecurso ParametrosDoPrazo(RegraRecursoFaseInput declarada) =>
        new(
            declarada.PrazoValor,
            declarada.PrazoUnidade,
            declarada.SuspensividadePrimeiraInstanciaValor,
            declarada.SuspensividadePrimeiraInstanciaUnidade,
            declarada.AdmiteInstanciaSuperior,
            declarada.SuspensividadeSegundaInstanciaValor,
            declarada.SuspensividadeSegundaInstanciaUnidade);
}
