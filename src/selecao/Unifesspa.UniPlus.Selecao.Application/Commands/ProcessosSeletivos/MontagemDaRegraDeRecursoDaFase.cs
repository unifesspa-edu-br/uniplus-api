namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Monta a regra de recurso de uma fase. A regra referenciada é resolvida pelo mesmo
/// <see cref="IRegraCatalogoReader"/> das demais definições, e a referência sai dos valores RESOLVIDOS do
/// catálogo, nunca ecoados do payload: é o que faz o hash bater por construção.
/// </summary>
internal sealed class MontagemDaRegraDeRecursoDaFase(
    IRegraCatalogoReader regraCatalogoReader,
    ConferenciaDaAncoraDoRecursoDaFase conferenciaDaAncora)
{
    /// <summary>A regra montada, ou sucesso nulo quando a fase não declara recurso; falha é interrupção.</summary>
    public async Task<Result<RegraRecursoFase?>> MontarAsync(
        RegraRecursoFaseInput? declarada,
        int indice,
        string faseCodigo,
        IReadOnlyList<ProdutoDaFase> produtos,
        CancellationToken cancellationToken)
    {
        if (declarada is null)
        {
            return Result<RegraRecursoFase?>.Success(null);
        }

        string campo = $"fases[{indice}].regraRecurso";
        Result<ReferenciaRegra> referencia = await ReferenciaAsync(declarada, campo, cancellationToken).ConfigureAwait(false);
        if (referencia.IsFailure)
        {
            return Result<RegraRecursoFase?>.ValidationFailure(referencia.Errors);
        }

        // A âncora é resolvida DENTRO dos produtos desta fase: o mesmo tipo de ato pode ser publicado por
        // outra fase, e é a identidade da linha, não o código, que diz de qual publicação o prazo conta.
        // Quando o código não corresponde a produto algum, a âncora fica vazia e o domínio recusa nomeando
        // os preliminares disponíveis.
        ProdutoDaFase? ancora = ProdutoAncora(produtos, declarada.AtoAncoraCodigo);
        Result<RegraRecursoFase> regra = RegraRecursoFase.Criar(referencia.Value!, ArgumentosDoPrazo(declarada), ancora?.Id ?? Guid.Empty);
        if (regra.IsFailure)
        {
            return InterrupcaoDaFase.Com<RegraRecursoFase?>(campo, regra.Error!);
        }

        DomainError? recusaDaAncora = ancora is null
            ? null
            : await conferenciaDaAncora.ConferirAsync(faseCodigo, ancora, cancellationToken).ConfigureAwait(false);
        return recusaDaAncora is null
            ? Result<RegraRecursoFase?>.Success(regra.Value)
            : InterrupcaoDaFase.Com<RegraRecursoFase?>($"{campo}.atoAncoraCodigo", recusaDaAncora);
    }

    private async Task<Result<ReferenciaRegra>> ReferenciaAsync(
        RegraRecursoFaseInput declarada, string campo, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regraCatalogoReader
            .ObterAsync(declarada.RegraCodigo, declarada.RegraVersao, cancellationToken).ConfigureAwait(false);
        if (regra is null
            || regra.Tipo != TipoRegra.RegraPrazoRecurso
            || regra.Codigo != RegraPrazoRecursoCodigo.AncoradoEmAto)
        {
            return InterrupcaoDaFase.Com<ReferenciaRegra>($"{campo}.regraCodigo", new DomainError(
                "RegraRecursoFase.RegraCatalogoInvalida",
                $"A regra {declarada.RegraCodigo}/{declarada.RegraVersao} não é a regra {RegraPrazoRecursoCodigo.AncoradoEmAto} do tipo regra_prazo_recurso."));
        }

        Result<ReferenciaRegra> referencia = ReferenciaRegra.Criar(regra.Codigo, regra.Versao, regra.Hash);
        return referencia.IsFailure
            ? InterrupcaoDaFase.Com<ReferenciaRegra>(campo, referencia.Error!)
            : referencia;
    }

    // Entre os PRELIMINARES: a fase publica o mesmo ato em dois papéis, e é da publicação preliminar que o
    // prazo corre. Buscar só pelo código devolveria o definitivo quando ele viesse primeiro, e a fase seria
    // recusada por ancorar onde não se pode.
    private static ProdutoDaFase? ProdutoAncora(IReadOnlyList<ProdutoDaFase> produtos, string atoAncoraCodigo) =>
        produtos.FirstOrDefault(p => p.Papel == PapelProdutoFase.Preliminar
            && string.Equals(p.AtoCodigo, atoAncoraCodigo, StringComparison.Ordinal));

    private static ArgsRegraPrazoRecurso ArgumentosDoPrazo(RegraRecursoFaseInput declarada) =>
        new(
            declarada.PrazoValor,
            declarada.PrazoUnidade,
            declarada.SuspensividadePrimeiraInstanciaValor,
            declarada.SuspensividadePrimeiraInstanciaUnidade,
            declarada.AdmiteInstanciaSuperior,
            declarada.SuspensividadeSegundaInstanciaValor,
            declarada.SuspensividadeSegundaInstanciaUnidade);
}
