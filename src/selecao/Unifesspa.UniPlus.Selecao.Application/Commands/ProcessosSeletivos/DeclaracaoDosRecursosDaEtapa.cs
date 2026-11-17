namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Errors;
using Domain.MotivosDeDecisao;
using Domain.ProcessosSeletivos.Recursos;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Declara as janelas recursais de uma etapa. A âncora em ato é declarada pelo código e resolvida DENTRO
/// dos produtos desta etapa: ancorar na publicação de outra etapa deixa de ser exprimível.
/// </summary>
internal sealed class DeclaracaoDosRecursosDaEtapa(
    AtosConsultadosNaEdicao atos,
    LeituraMemoizada<(string Codigo, string Versao), RegraCatalogo> regras,
    bool processoTemNotaDoEnem)
{
    private readonly ConferenciaDaAncoraDoRecurso _conferenciaDaAncora = new(atos);

    private readonly DeclaracaoDosProcedimentosDeRecurso _procedimentos = new(
        (codigo, versao, cancellationToken) => regras.ObterAsync((codigo, versao), cancellationToken),
        CodigosDoProcedimentoDeRecurso.DaEtapa);

    public async Task<Result> DeclararAsync(
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken)
    {
        List<RecursoDaEtapa> recursos = [];
        for (int posicao = 0; posicao < input.Recursos.Count; posicao++)
        {
            Result<RecursoDaEtapa> recurso = await RecursoAsync(etapa, input, posicao, atosJaAncorados, cancellationToken)
                .ConfigureAwait(false);
            if (recurso.IsFailure)
            {
                return Result.ValidationFailure(recurso.Errors);
            }

            recursos.Add(recurso.Value!);
        }

        return etapa.DefinirRecursos(recursos);
    }

    private async Task<Result<RecursoDaEtapa>> RecursoAsync(
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        int posicao,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken)
    {
        RecursoDaEtapaInput declarado = input.Recursos[posicao];
        ProdutoDaEtapa? produtoAncora = ProdutoAncora(etapa, declarado);
        DomainError? recusaDaAncora = await RecusaDaAncoraAsync(etapa, input, produtoAncora, atosJaAncorados, cancellationToken)
            .ConfigureAwait(false);
        if (recusaDaAncora is not null)
        {
            return Result<RecursoDaEtapa>.Failure(recusaDaAncora);
        }

        Result<ReferenciaRegra> regra = await ReferenciaDaRegraAsync(declarado, cancellationToken).ConfigureAwait(false);
        return regra.IsFailure
            ? Result<RecursoDaEtapa>.ValidationFailure(regra.Errors)
            : await RecursoComRegraAsync(etapa, input, posicao, produtoAncora, regra.Value!, cancellationToken).ConfigureAwait(false);
    }

    // Conferido sobre o produto âncora JÁ RESOLVIDO, e não sobre o código cru do payload: um código
    // que não corresponde a produto preliminar algum da etapa é âncora inexistente, e quem sabe
    // dizer isso é o domínio, nomeando os preliminares disponíveis.
    private async Task<DomainError?> RecusaDaAncoraAsync(
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        ProdutoDaEtapa? produtoAncora,
        IReadOnlySet<string> atosJaAncorados,
        CancellationToken cancellationToken) =>
        produtoAncora is null
            ? null
            : await _conferenciaDaAncora
                .ConferirAsync(
                    produtoAncora.AtoCodigo,
                    RotuloDoDonoDaAncora.DaEtapa(input.Nome),
                    atosJaAncorados.Contains,
                    (atoCodigo, token) => atos.Congelados.AncoraDaEtapaJaPublicadaAsync(etapa.Id, atoCodigo, token),
                    cancellationToken)
                .ConfigureAwait(false);

    private async Task<Result<RecursoDaEtapa>> RecursoComRegraAsync(
        EtapaProcesso etapa,
        EtapaProcessoInput input,
        int posicao,
        ProdutoDaEtapa? produtoAncora,
        ReferenciaRegra regra,
        CancellationToken cancellationToken)
    {
        RecursoDaEtapaInput declarado = input.Recursos[posicao];
        Result<ProcedimentosDeRecurso> procedimentos = await _procedimentos
            .DeclararAsync(declarado.Procedimentos, ContextoDosProcedimentos(etapa, input), $"recursos[{posicao}].procedimentos", cancellationToken)
            .ConfigureAwait(false);
        return procedimentos.IsFailure
            ? Result<RecursoDaEtapa>.ValidationFailure(procedimentos.Errors)
            : RecursoDaEtapa.Criar(declarado.Ancora, regra, ArgumentosDoPrazo(declarado), produtoAncora?.Id ?? Guid.Empty, procedimentos.Value);
    }

    // O lugar da janela é a própria etapa: ela declara os tipos de decisão que produz e os itens de avaliação que o alvo
    // pode apontar. A área do ENEM vale em qualquer janela de processo que importa a nota do ENEM. Os tipos vêm do payload,
    // e não da etapa: recusada a declaração dos tipos, a etapa ainda guarda os anteriores.
    private ContextoDosProcedimentosDeRecurso ContextoDosProcedimentos(EtapaProcesso etapa, EtapaProcessoInput input) => new(
        TiposDeDecisaoDeclarados.Aceitos(input.TiposDeDecisao),
        etapa.ItensDeAvaliacao.Select(static item => item.Id).ToHashSet(),
        processoTemNotaDoEnem);

    // Entre os PRELIMINARES, e não entre todos os produtos: a etapa publica o mesmo ato duas vezes, e é do
    // preliminar que a janela corre. Resolver só pelo código devolveria o definitivo quando ele viesse
    // primeiro, e a etapa seria recusada por ancorar onde não se pode.
    private static ProdutoDaEtapa? ProdutoAncora(EtapaProcesso etapa, RecursoDaEtapaInput declarado) =>
        declarado.Ancora == AncoraDoRecurso.AtoPublicado
            ? etapa.Produtos.FirstOrDefault(p => p.Papel == PapelProdutoFase.Preliminar
                && string.Equals(p.AtoCodigo, declarado.AtoAncoraCodigo, StringComparison.Ordinal))
            : null;

    // A referência é montada a partir dos valores RESOLVIDOS do catálogo, nunca ecoados do payload, e a
    // recusa é de validação, nunca exceção: catálogo que não respondeu chega aqui com código em branco, e o
    // cliente precisa da mensagem, não de um 500.
    private async Task<Result<ReferenciaRegra>> ReferenciaDaRegraAsync(RecursoDaEtapaInput declarado, CancellationToken cancellationToken)
    {
        RegraCatalogo? regra = await regras.ObterAsync((declarado.RegraCodigo, declarado.RegraVersao), cancellationToken)
            .ConfigureAwait(false);
        return regra is null || regra.Tipo != TipoRegra.RegraPrazoRecurso
            ? Result<ReferenciaRegra>.Failure(new DomainError(
                "RecursoDaEtapa.RegraCatalogoInvalida",
                $"A regra {declarado.RegraCodigo}/{declarado.RegraVersao} não é uma regra de prazo de recurso do catálogo."))
            : Result<ReferenciaRegra>.Success(regra.Referencia());
    }

    private static ArgsRegraPrazoRecurso ArgumentosDoPrazo(RecursoDaEtapaInput declarado) =>
        new(
            declarado.PrazoValor,
            declarado.PrazoUnidade,
            declarado.SuspensividadePrimeiraInstanciaValor,
            declarado.SuspensividadePrimeiraInstanciaUnidade,
            declarado.AdmiteInstanciaSuperior,
            declarado.SuspensividadeSegundaInstanciaValor,
            declarado.SuspensividadeSegundaInstanciaUnidade);
}
