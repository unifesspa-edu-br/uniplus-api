namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
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
        "remanejamentoDestinoFinal", "regraDeAplicabilidade", "acaoQuandoIndeferido", "baseLegal", "quantidadeDeclarada",
    ];

    /// <summary>
    /// As ações ao indeferir que o <b>cadastro</b> admite — domínio fechado
    /// (<c>AcoesQuandoIndeferido</c>, módulo Configuração). O comando nunca produz outra
    /// coisa: ele <b>copia</b> o token da view do cadastro.
    /// </summary>
    /// <remarks>
    /// O envelope o congela por valor (ADR-0061), e o decoder não passa pelo cadastro — sem
    /// esta lista, um token inventado faria round-trip perfeito e reidrataria uma
    /// <b>instrução que o motor de homologação não sabe executar</b>. A lista está aqui, e
    /// não numa referência ao módulo Configuração, porque ela é a do vocabulário
    /// <b>congelado</b> nesta forma do envelope: um token novo no cadastro exige atualizar
    /// esta lista.
    /// </remarks>
    internal static readonly string[] AcoesQuandoIndeferido =
        ["RECLASSIFICA_AC", "RECLASSIFICA_CONFORME_EDITAL"];


    internal static ModalidadeSelecionada? LerModalidade(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDaModalidade);

        ModalidadeLidaDoEnvelope declaracao = LerDeclaracao(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        if (declaracao.RecusaDoVocabulario() is { } vocabulario)
        {
            return leitor.Propagar<ModalidadeSelecionada>([new FieldError(null, vocabulario)], path);
        }

        return Reidratar(leitor, declaracao, path);
    }

    private static ModalidadeLidaDoEnvelope LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string path) =>
        LerEstruturaDeVagas(leitor, item, path) with
        {
            // A modalidade incondicional não tem cláusula, e o encoder a grava nula.
            Regra = LeituraDasCondicoes.LerDnf(leitor, item, "regraDeAplicabilidade", path),
            AcaoQuandoIndeferido = leitor.TextoOpcional(item, "acaoQuandoIndeferido", path, LimitesDoEnvelope.Token),
            BaseLegal = leitor.TextoNaoVazio(item, "baseLegal", path, LimitesDoEnvelope.BaseLegal),
            QuantidadeDeclarada = leitor.InteiroOpcional(item, "quantidadeDeclarada", path),
        };

    private static ModalidadeLidaDoEnvelope LerEstruturaDeVagas(LeitorEnvelope leitor, JsonObject item, string path) => new()
    {
        OrigemId = leitor.Identificador(item, "modalidadeOrigemId", path),
        Codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.ModalidadeCodigo),
        Descricao = leitor.TextoOpcional(item, "descricao", path, LimitesDoEnvelope.ModalidadeDescricao),
        Natureza = leitor.NaturezaLegal(item, "naturezaLegal", path),
        Composicao = leitor.ComposicaoDeVagas(item, "composicaoVagas", path),
        ComposicaoOrigem = leitor.TextoOpcional(item, "composicaoOrigemCodigo", path, LimitesDoEnvelope.ModalidadeCodigo),
        Remanejamento = leitor.RegraDeRemanejamento(item, "regraRemanejamento", path),
        Destino = leitor.TextoOpcional(item, "remanejamentoDestino", path, LimitesDoEnvelope.ModalidadeCodigo),
        Par = leitor.TextoOpcional(item, "remanejamentoPar", path, LimitesDoEnvelope.ModalidadeCodigo),
        Fallback = leitor.TextoOpcional(item, "remanejamentoDestinoFinal", path, LimitesDoEnvelope.ModalidadeCodigo),
    };

    private static ModalidadeSelecionada? Reidratar(LeitorEnvelope leitor, ModalidadeLidaDoEnvelope d, string path)
    {
        Result<PredicadoDnf?> regraLida = LeituraDasCondicoes.PredicadoOpcional(d.Regra);
        if (regraLida.IsFailure)
        {
            return leitor.Propagar<ModalidadeSelecionada>(regraLida.Errors, $"{path}.regraDeAplicabilidade");
        }

        return CriarModalidade(leitor, d, regraLida.Value, path);
    }

    private static ModalidadeSelecionada? CriarModalidade(
        LeitorEnvelope leitor, ModalidadeLidaDoEnvelope d, PredicadoDnf? regra, string path)
    {
        Result<ModalidadeSelecionada> modalidade = ModalidadeSelecionada.Criar(
            d.NoCadastro(regra ?? PredicadoDnf.SemClausula), d.QuantidadeDeclarada);
        if (modalidade.IsFailure)
        {
            return leitor.Propagar<ModalidadeSelecionada>(modalidade.Errors, path);
        }

        return ConferirRemanejamentoPelaNatureza(leitor, modalidade.Value!);
    }

    // O envelope não passa pelo cadastro; a coerência é a mesma exigida na publicação.
    private static ModalidadeSelecionada? ConferirRemanejamentoPelaNatureza(LeitorEnvelope leitor, ModalidadeSelecionada modalidade) =>
        modalidade.RemanejamentoCoerenteComANatureza() ? modalidade
            : leitor.Malformado<ModalidadeSelecionada?>("modalidades", "a regra de remanejamento não é a que a natureza da modalidade pede.");

    private sealed record ModalidadeLidaDoEnvelope
    {
        public Guid OrigemId { get; init; }

        public string Codigo { get; init; } = string.Empty;

        public string? Descricao { get; init; }

        public NaturezaLegalModalidade Natureza { get; init; }

        public ComposicaoVagasModalidade Composicao { get; init; }

        public string? ComposicaoOrigem { get; init; }

        public RegraRemanejamentoModalidade? Remanejamento { get; init; }

        public string? Destino { get; init; }

        public string? Par { get; init; }

        public string? Fallback { get; init; }

        public IReadOnlyList<(int Clausula, string Fato, Operador Operador, JsonElement Valor)> Regra { get; init; } = [];

        public string? AcaoQuandoIndeferido { get; init; }

        public string BaseLegal { get; init; } = string.Empty;

        public int? QuantidadeDeclarada { get; init; }

        internal ModalidadeNoCadastro NoCadastro(PredicadoDnf regra) => new()
        {
            Id = OrigemId,
            Codigo = Codigo,
            Descricao = Descricao,
            NaturezaLegal = Natureza,
            ComposicaoVagas = Composicao,
            ComposicaoOrigemCodigo = ComposicaoOrigem,
            RegraRemanejamento = Remanejamento,
            RemanejamentoDestino = Destino,
            RemanejamentoPar = Par,
            RemanejamentoFallback = Fallback,
            RegraDeAplicabilidade = regra,
            AcaoQuandoIndeferido = AcaoQuandoIndeferido,
            BaseLegal = BaseLegal,
        };

        internal DomainError? RecusaDoVocabulario() =>
            CodigoForaDoFormato() is not null ? RecusaDoFormatoDoCodigo()
            : AcaoQuandoIndeferido is { } acao && !AcoesQuandoIndeferido.Contains(acao, StringComparer.Ordinal)
                ? RecusaDaAcaoAoIndeferir() : null;

        private string? CodigoForaDoFormato() => new[] { Codigo, ComposicaoOrigem, Destino, Par, Fallback }
            .FirstOrDefault(codigo => codigo is not null && !FormatoDoCodigo.TemFormatoValido(codigo));

        private static DomainError RecusaDoFormatoDoCodigo() => new(
            ErrosCodecEnvelope.EnvelopeMalformado,
            "Envelope malformado em 'modalidades': o código não tem o formato do cadastro " +
            "(A-Z, 0-9 e underscore) — e códigos são chave de composição e de remanejamento dentro da oferta.");

        private static DomainError RecusaDaAcaoAoIndeferir() => new(
            ErrosCodecEnvelope.EnvelopeMalformado,
            "Envelope malformado em 'modalidades': a ação ao indeferir não pertence ao " +
            $"domínio fechado do cadastro ({string.Join(", ", AcoesQuandoIndeferido)}) — restaurá-la daria ao " +
            "motor de homologação uma instrução que ele não sabe executar.");
    }

}
