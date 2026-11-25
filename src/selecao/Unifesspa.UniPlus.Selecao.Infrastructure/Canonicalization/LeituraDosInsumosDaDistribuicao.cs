namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Serializacao;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Leitura do bloco <c>distribuicao</c>: o que cada oferta declara, antes de a configuração ser remontada.</summary>
internal static class LeituraDosInsumosDaDistribuicao
{
    private static readonly string[] ChavesDoInsumo =
    [
        "ofertaCursoOrigemId", "vagasOfertadasBase", "percentualDaReservaLegal", "regraDistribuicao", "regraAjuste",
        "ajusteDoQuadro", "referenciaDemografica", "grupoAreaEnem",
    ];

    internal static List<InsumoDaDistribuicao> Ler(LeitorEnvelope leitor, JsonArray arrayDistribuicao)
    {
        List<InsumoDaDistribuicao> insumos = [];
        for (int i = 0; !leitor.Falhou && i < arrayDistribuicao.Count; i++)
        {
            InsumoDaDistribuicao? insumo = LerInsumo(leitor, leitor.ItemObjeto(arrayDistribuicao, i, "distribuicao"), $"distribuicao[{i}]");
            if (insumo is not { } lido)
            {
                break;
            }

            insumos.Add(lido);
        }

        return insumos;
    }

    private static InsumoDaDistribuicao? LerInsumo(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, ChavesDoInsumo);

        Guid oferta = leitor.Identificador(item, "ofertaCursoOrigemId", path);
        int vagasOfertadasBase = leitor.Inteiro(item, "vagasOfertadasBase", path);
        decimal? percentual = leitor.DecimalOpcional(item, "percentualDaReservaLegal", EscritaCanonica.EscalaPadrao, path, LimitesDoEnvelope.PrecisaoPr);
        RegrasDaDistribuicao regras = LerRegras(leitor, item, path);
        ReferenciaReservaDemograficaSnapshot? demografica = LeituraDaReferenciaDemografica.Ler(leitor, item, path);
        (string? Codigo, string? Rotulo)? grupoAreaEnem = LeituraDoGrupoDeAreaEnem.LerOpcional(leitor, item, path);

        return leitor.Falhou
            ? null
            : new InsumoDaDistribuicao(oferta, vagasOfertadasBase, percentual, regras, demografica, grupoAreaEnem);
    }

    // O rol deriva do catálogo de regras, não de literais: uma regra publicada sob qualquer código
    // do catálogo tem de reidratar, e uma lista escrita à mão deixaria de fora a que não a acompanhasse.
    private static RegrasDaDistribuicao LerRegras(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.Regra<RegraDeDistribuicaoDeVagas>(item, "regraDistribuicao", path),
        leitor.RegraOpcional<RegraDeAjusteDaDistribuicaoDeVagas>(item, "regraAjuste", path),
        LerAjusteDoQuadro(leitor, item, path));

    /// <summary>
    /// Os parâmetros da regra de ajuste do quadro que o edital declarou — <see langword="null"/> quando a distribuição não declara
    /// nenhum. A variante vem do discriminador <c>$tipo</c>, e a que o envelope não conhece é malformada.
    /// </summary>
    private static ParametrosRegraAjusteDistribuicao? LerAjusteDoQuadro(LeitorEnvelope leitor, JsonObject item, string pathPai)
    {
        JsonObject? ajuste = leitor.ObjetoOpcional(item, "ajusteDoQuadro", pathPai);
        return leitor.Falhou || ajuste is null ? null : LerVarianteDoAjuste(leitor, ajuste, $"{pathPai}.ajusteDoQuadro");
    }

    private static ParametrosRegraAjusteDistribuicao? LerVarianteDoAjuste(LeitorEnvelope leitor, JsonObject ajuste, string path)
    {
        string tipo = leitor.TextoNaoVazio(ajuste, "$tipo", path);
        return tipo switch
        {
            TiposDeAjusteDoQuadro.ReduzirDe => LerReduzirDe(leitor, ajuste, path),
            TiposDeAjusteDoQuadro.ReduzirProporcionalEm => LerReduzirProporcionalEm(leitor, ajuste, path),
            _ => leitor.Falhou ? null : leitor.Malformado<ParametrosRegraAjusteDistribuicao?>(path, $"o tipo de ajuste '{tipo}' não pertence a esta versão do envelope."),
        };
    }

    private static ParametrosReduzirDe? LerReduzirDe(LeitorEnvelope leitor, JsonObject ajuste, string path)
    {
        leitor.ExigirChaves(ajuste, path, "$tipo", "modalidadeCodigo");
        string modalidade = leitor.TextoNaoVazio(ajuste, "modalidadeCodigo", path, LimitesDoEnvelope.ModalidadeCodigo);
        return leitor.Falhou ? null : new ParametrosReduzirDe(modalidade);
    }

    private static ParametrosReduzirProporcionalEm? LerReduzirProporcionalEm(LeitorEnvelope leitor, JsonObject ajuste, string path)
    {
        leitor.ExigirChaves(ajuste, path, "$tipo", "modalidadeCodigos");
        IReadOnlyList<string> modalidades = leitor.Textos(ajuste, "modalidadeCodigos", path);
        return leitor.Falhou ? null : new ParametrosReduzirProporcionalEm(modalidades);
    }
}
