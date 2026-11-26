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

    // O envelope congela as ações aceitas para impedir a restauração de instruções desconhecidas.
    internal static readonly string[] AcoesQuandoIndeferido =
        ["RETIRA_A_MODALIDADE"];


    internal static ModalidadeSelecionada? LerModalidade(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDaModalidade);

        ModalidadeLidaDoEnvelope declaracao = LerDeclaracao(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
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
        ComposicaoOrigem = leitor.TextoOpcionalNaoVazio(item, "composicaoOrigemCodigo", path, LimitesDoEnvelope.ModalidadeCodigo),
        Remanejamento = leitor.RegraDeRemanejamento(item, "regraRemanejamento", path),
        Destino = leitor.TextoOpcionalNaoVazio(item, "remanejamentoDestino", path, LimitesDoEnvelope.ModalidadeCodigo),
        Par = leitor.TextoOpcionalNaoVazio(item, "remanejamentoPar", path, LimitesDoEnvelope.ModalidadeCodigo),
        DestinoFinal = leitor.TextoOpcionalNaoVazio(item, "remanejamentoDestinoFinal", path, LimitesDoEnvelope.ModalidadeCodigo),
    };

    private static ModalidadeSelecionada? Reidratar(LeitorEnvelope leitor, ModalidadeLidaDoEnvelope declaracao, string path)
    {
        Result<PredicadoDnf?> regraLida = LeituraDasCondicoes.PredicadoOpcional(declaracao.Regra);
        // A criação não depende da regra, e a modalidade é descartada se ela for recusada.
        Result<ModalidadeSelecionada> modalidade = ModalidadeSelecionada.Criar(
            declaracao.NoCadastro(regraLida.Value ?? PredicadoDnf.SemClausula), declaracao.QuantidadeDeclarada);
        IReadOnlyList<FieldError> causas = [.. declaracao.RecusasDoVocabulario(),
            .. regraLida.Errors.Select(causa => causa.NoCaminhoDoItem("regraDeAplicabilidade")),
            .. modalidade.Errors, .. declaracao.RecusaDoRemanejamentoIncoerente()];
        return causas.Count > 0 ? leitor.Propagar<ModalidadeSelecionada>(causas, path) : modalidade.Value;
    }

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

        public string? DestinoFinal { get; init; }

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
            RemanejamentoFallback = DestinoFinal,
            RegraDeAplicabilidade = regra,
            AcaoQuandoIndeferido = AcaoQuandoIndeferido,
            BaseLegal = BaseLegal,
        };

        internal IEnumerable<FieldError> RecusasDoVocabulario()
        {
            (string Chave, string? Valor)[] codigos = [("codigo", Codigo), ("composicaoOrigemCodigo", ComposicaoOrigem),
                ("remanejamentoDestino", Destino), ("remanejamentoPar", Par), ("remanejamentoDestinoFinal", DestinoFinal)];
            foreach ((string chave, string? valor) in codigos)
            {
                if (valor is not null && !FormatoDoCodigo.TemFormatoValido(valor))
                    yield return new FieldError(chave, RecusaDoFormatoDoCodigo());
            }

            if (AcaoQuandoIndeferido is { } acao && !AcoesQuandoIndeferido.Contains(acao, StringComparer.Ordinal))
                yield return new FieldError("acaoQuandoIndeferido", RecusaDaAcaoAoIndeferir());
        }

        internal IEnumerable<FieldError> RecusaDoRemanejamentoIncoerente() =>
            ModalidadeSelecionada.RemanejamentoCoerenteComANatureza(Natureza, Remanejamento) ? []
                : [new FieldError(null, LeitorEnvelope.Malformacao("modalidades",
                    "a regra de remanejamento não é a que a natureza da modalidade pede."))];

        private static DomainError RecusaDoFormatoDoCodigo() => LeitorEnvelope.Malformacao("modalidades",
            "o código não tem o formato do cadastro " +
            "(A-Z, 0-9 e underscore) — e códigos são chave de composição e de remanejamento dentro da oferta.");

        private static DomainError RecusaDaAcaoAoIndeferir() => LeitorEnvelope.Malformacao("modalidades",
            "a ação ao indeferir não pertence ao " +
            $"domínio fechado do cadastro ({string.Join(", ", AcoesQuandoIndeferido)}) — restaurá-la daria à " +
            "homologação das inscrições uma instrução que ela não sabe executar.");
    }

}
