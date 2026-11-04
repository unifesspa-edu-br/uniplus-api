namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura das modalidades de concorrência de cada oferta e do vocabulário que elas obedecem.
/// </summary>
internal static class LeituraDasModalidades
{
    private static readonly string[] ChavesDaModalidade =
    [
        "ofertaCursoOrigemId", "modalidadeOrigemId", "codigo", "descricao", "naturezaLegal", "composicaoVagas",
        "composicaoOrigemCodigo", "regraRemanejamento", "remanejamentoDestino", "remanejamentoPar",
        "remanejamentoFallback", "regraDeAplicabilidade", "acaoQuandoIndeferido", "baseLegal", "quantidadeDeclarada",
    ];

    /// <summary>
    /// As ações ao indeferir que o <b>cadastro</b> admite — domínio fechado
    /// (<c>AcoesQuandoIndeferido</c>, módulo Configuração). O comando nunca produz outra
    /// coisa: ele <b>copia</b> o token da view do cadastro.
    /// </summary>
    /// <remarks>
    /// O envelope o congela por valor (ADR-0061), e o decoder não passa pelo cadastro — sem
    /// esta lista, um token inventado faria round-trip perfeito e restauraria uma
    /// <b>instrução que o motor de homologação não sabe executar</b>. A lista está aqui, e
    /// não numa referência ao módulo Configuração, porque ela é a do vocabulário
    /// <b>congelado</b> nesta forma do envelope: um token novo no cadastro exige atualizar
    /// esta lista.
    /// </remarks>
    internal static readonly string[] AcoesQuandoIndeferido =
        ["RECLASSIFICAR_AC", "RECLASSIFICAR_REGRA_EDITAL"];

    /// <summary>
    /// O formato que o <c>CodigoModalidade</c> do cadastro impõe (<c>^[A-Z0-9_]+$</c>). Um
    /// código fora dele nunca sai do cadastro — mas é <b>chave</b> de composição e de
    /// remanejamento dentro da oferta, e um envelope adulterado o usaria como tal.
    /// </summary>
    internal static readonly Regex FormatoCodigoModalidade =
        new("^[A-Z0-9_]+$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <summary>
    /// O formato que o <c>CodigoCondicao</c> do cadastro impõe
    /// (<c>^[A-Z][A-Z0-9_]{1,49}$</c>). O código da condição é <b>chave natural</b> — é por
    /// ele que a invariante ADR-0067 reconhece a condição PcD (<c>CodigoCondicaoPcd</c>) —,
    /// e um <c>"pcd"</c> minúsculo restaurado do envelope seria uma condição que o cadastro
    /// não produz e que o resto do código trata como chave.
    /// </summary>
    internal static readonly Regex FormatoCodigoCondicao =
        new("^[A-Z][A-Z0-9_]{1,49}$", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    internal static ModalidadeSelecionada? LerModalidade(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDaModalidade);

        DeclaracaoDaModalidade declaracao = LerDeclaracao(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (VocabularioDaModalidade(declaracao.Codigo, declaracao.AcaoQuandoIndeferido, declaracao.ComposicaoOrigem, declaracao.Destino, declaracao.Par, declaracao.Fallback) is { } vocabulario)
        {
            return leitor.Propagar<ModalidadeSelecionada>(vocabulario);
        }

        return Reidratar(leitor, declaracao);
    }

    private static DeclaracaoDaModalidade LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string path) => new()
    {
        OrigemId = leitor.Identificador(item, "modalidadeOrigemId", path),
        Codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.ModalidadeCodigo),
        Descricao = leitor.TextoOpcional(item, "descricao", path, LimitesDoEnvelope.ModalidadeDescricao),
        Natureza = leitor.Enumeracao<NaturezaLegalModalidade>(item, "naturezaLegal", path),
        Composicao = leitor.Enumeracao<ComposicaoVagasModalidade>(item, "composicaoVagas", path),
        ComposicaoOrigem = leitor.TextoOpcional(item, "composicaoOrigemCodigo", path, LimitesDoEnvelope.ModalidadeCodigo),
        Remanejamento = leitor.Enumeracao<RegraRemanejamentoModalidade>(item, "regraRemanejamento", path),
        Destino = leitor.TextoOpcional(item, "remanejamentoDestino", path, LimitesDoEnvelope.ModalidadeCodigo),
        Par = leitor.TextoOpcional(item, "remanejamentoPar", path, LimitesDoEnvelope.ModalidadeCodigo),
        Fallback = leitor.TextoOpcional(item, "remanejamentoFallback", path, LimitesDoEnvelope.ModalidadeCodigo),

        // A modalidade incondicional não tem cláusula, e o encoder a grava nula.
        Regra = LeituraDasCondicoes.LerDnf(leitor, item, "regraDeAplicabilidade", path),
        AcaoQuandoIndeferido = leitor.TextoOpcional(item, "acaoQuandoIndeferido", path, LimitesDoEnvelope.Token),
        BaseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegal),
        QuantidadeDeclarada = leitor.InteiroOpcional(item, "quantidadeDeclarada", path),
    };

    private static ModalidadeSelecionada? Reidratar(LeitorEnvelope leitor, DeclaracaoDaModalidade d)
    {
        Result<PredicadoDnf?> regraLida = LeituraDasCondicoes.PredicadoOpcional(d.Regra);
        if (regraLida.IsFailure)
        {
            return leitor.Propagar<ModalidadeSelecionada>(regraLida.Error!);
        }

        Result<ModalidadeSelecionada> modalidade = ModalidadeSelecionada.Criar(
            d.OrigemId,
            d.Codigo,
            d.Descricao,
            d.Natureza,
            d.Composicao,
            d.ComposicaoOrigem,
            d.Remanejamento,
            d.Destino,
            d.Par,
            d.Fallback,
            regraLida.Value ?? PredicadoDnf.SemClausula,
            d.AcaoQuandoIndeferido,
            d.BaseLegal,
            d.QuantidadeDeclarada);

        if (modalidade.IsFailure)
        {
            return leitor.Propagar<ModalidadeSelecionada>(modalidade.Error!);
        }

        // A coerência entre natureza e remanejamento é do cadastro, e a cópia congelada não passa por
        // ele: um envelope adulterado restauraria, por exemplo, uma ampla concorrência que remaneja as
        // próprias vagas ociosas. A regra é a mesma que a publicação aplica.
        return modalidade.Value!.RemanejamentoCoerenteComANatureza()
            ? modalidade.Value
            : leitor.Malformado<ModalidadeSelecionada?>("modalidades", $"a regra de remanejamento de {d.Codigo} não é a que a natureza dela pede.");
    }

    private sealed class DeclaracaoDaModalidade
    {
        public Guid OrigemId { get; init; }

        public string Codigo { get; init; } = string.Empty;

        public string? Descricao { get; init; }

        public NaturezaLegalModalidade Natureza { get; init; }

        public ComposicaoVagasModalidade Composicao { get; init; }

        public string? ComposicaoOrigem { get; init; }

        public RegraRemanejamentoModalidade Remanejamento { get; init; }

        public string? Destino { get; init; }

        public string? Par { get; init; }

        public string? Fallback { get; init; }

        public IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> Regra { get; init; } = [];

        public string? AcaoQuandoIndeferido { get; init; }

        public string BaseLegal { get; init; } = string.Empty;

        public int? QuantidadeDeclarada { get; init; }
    }

    /// <summary>
    /// O <b>vocabulário</b> da modalidade — os tokens que só o cadastro produz.
    /// </summary>
    /// <remarks>
    /// O código é <b>chave</b>: a composição (<c>RETIRA_DE</c>) e o remanejamento
    /// (<c>DESTINO_UNICO</c>, <c>CRUZADO</c>) apontam para códigos de outras modalidades da
    /// mesma oferta. Um código fora do formato do cadastro (<c>^[A-Z0-9_]+$</c>) nunca sai
    /// de lá — mas um envelope adulterado o usaria como chave, e o motor de vagas do certame
    /// receberia um grafo de remanejamento cujos nós não existem no cadastro.
    /// </remarks>
    internal static DomainError? VocabularioDaModalidade(
        string codigo,
        string? acaoQuandoIndeferido,
        string? composicaoOrigem,
        string? destino,
        string? par,
        string? fallback)
    {
        IEnumerable<string> foraDoFormato = new[] { codigo, composicaoOrigem, destino, par, fallback }
            .Where(static c => c is not null)
            .Select(static c => c!)
            .Where(c => !FormatoCodigoModalidade.IsMatch(c));

        foreach (string cruzamento in foraDoFormato)
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Envelope malformado em 'modalidades': o código '{cruzamento}' não tem o formato do cadastro " +
                "(A-Z, 0-9 e underscore) — e códigos são chave de composição e de remanejamento dentro da oferta.");
        }

        if (acaoQuandoIndeferido is not null && !AcoesQuandoIndeferido.Contains(acaoQuandoIndeferido, StringComparer.Ordinal))
        {
            return new DomainError(
                ErrosCodecEnvelope.EnvelopeMalformado,
                $"Envelope malformado em 'modalidades': a ação ao indeferir '{acaoQuandoIndeferido}' não pertence ao " +
                $"domínio fechado do cadastro ({string.Join(", ", AcoesQuandoIndeferido)}) — restaurá-la daria ao " +
                "motor de homologação uma instrução que ele não sabe executar.");
        }

        return null;
    }
}
