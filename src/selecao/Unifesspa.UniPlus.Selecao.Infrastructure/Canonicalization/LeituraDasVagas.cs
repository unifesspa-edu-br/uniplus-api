namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura da distribuição de vagas: reconstrói as configurações a partir dos insumos que o envelope congela e
/// confere o quadro congelado contra o que a montagem produz.
/// </summary>
internal static class LeituraDasVagas
{
    /// <summary>
    /// Recombina os <b>quatro blocos derivados</b> da mesma coleção — <c>distribuicao</c>,
    /// <c>modalidades</c>, <c>vagas</c> e <c>ofertas</c>. A exigência é <b>igualdade
    /// exata de conjuntos</b> de <c>ofertaCursoOrigemId</c>, não mera inclusão: recombinar
    /// em silêncio um envelope incoerente reconstruiria um agregado que <b>nunca
    /// existiu</b> — uma modalidade sem oferta, uma oferta sem vagas.
    /// </summary>
    internal static IReadOnlyList<ConfiguracaoDistribuicaoVagas> LerDistribuicao(LeitorEnvelope leitor, JsonObject payload)
    {
        BlocosDaDistribuicao blocos = LerBlocos(leitor, payload);
        if (!leitor.Falhou && blocos.Incoerencia() is { } incoerencia)
        {
            leitor.Propagar<object>(incoerencia);
        }

        return leitor.Falhou ? [] : Remontar(leitor, blocos);
    }

    private static BlocosDaDistribuicao LerBlocos(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray distribuicao = leitor.Array(payload, "distribuicao", "$");
        JsonArray modalidades = leitor.Array(payload, "modalidades", "$");
        JsonArray vagas = leitor.Array(payload, "vagas", "$");
        List<(Guid Id, OfertaCursoSnapshot Oferta)> ofertas = LeituraDasOfertas.LerOfertas(leitor, payload);
        List<Guid> ofertasEmVagas = LeituraDoQuadroDeVagas.LerOfertas(leitor, vagas);

        return new BlocosDaDistribuicao(
            ofertas, LeituraDosInsumosDaDistribuicao.Ler(leitor, distribuicao), LerModalidadesPorOferta(leitor, modalidades), vagas, ofertasEmVagas);
    }

    private static List<ConfiguracaoDistribuicaoVagas> Remontar(LeitorEnvelope leitor, BlocosDaDistribuicao blocos)
    {
        List<ConfiguracaoDistribuicaoVagas> distribuicao = [];
        for (int indice = 0; indice < blocos.Insumos.Count && !leitor.Falhou; indice++)
        {
            RemontarDaPosicao(leitor, blocos, indice, distribuicao);
        }

        if (!leitor.Falhou)
        {
            LeituraDoQuadroDeVagas.ConferirComAMontagem(leitor, blocos.Vagas, distribuicao);
        }
        return leitor.Falhou ? [] : distribuicao;
    }

    private static void RemontarDaPosicao(LeitorEnvelope leitor, BlocosDaDistribuicao blocos, int indice, List<ConfiguracaoDistribuicaoVagas> distribuicao)
    {
        Result<ConfiguracaoDistribuicaoVagas> configuracao = blocos.Remontar(blocos.Insumos[indice]);
        if (configuracao.IsFailure)
        {
            IReadOnlyList<FieldError> causas = [.. configuracao.Errors.Select(static causa => causa.Field is
                "vagasOfertadasBase" or "percentualDaReservaLegal" or "referenciaDemografica" or "ajusteDoQuadro" or "grupoAreaEnem"
                ? causa : LeitorEnvelope.NoProprioItem([causa])[0])];
            leitor.Propagar<ConfiguracaoDistribuicaoVagas>(causas, $"distribuicao[{indice}]");
            return;
        }

        distribuicao.Add(configuracao.Value!);
    }

    private static ModalidadesPorOferta LerModalidadesPorOferta(LeitorEnvelope leitor, JsonArray arrayModalidades)
    {
        ModalidadesPorOferta lidas = new();
        for (int i = 0; !leitor.Falhou && i < arrayModalidades.Count; i++)
        {
            JsonObject item = leitor.ItemObjeto(arrayModalidades, i, "modalidades");
            Guid oferta = leitor.Identificador(item, "ofertaCursoOrigemId", $"modalidades[{i}]");
            lidas.Acrescentar(oferta, LeituraDasModalidades.LerModalidade(leitor, item, $"modalidades[{i}]"));
        }

        return lidas;
    }

    private sealed class ModalidadesPorOferta
    {
        internal Dictionary<Guid, List<ModalidadeSelecionada>> PorOferta { get; } = [];

        internal List<Guid> Ofertas { get; } = [];

        internal void Acrescentar(Guid oferta, ModalidadeSelecionada? modalidade)
        {
            if (modalidade is null)
            {
                return;
            }

            Ofertas.Add(oferta);
            PorOferta.TryAdd(oferta, []);
            PorOferta[oferta].Add(modalidade);
        }
    }

    private readonly record struct BlocosDaDistribuicao(
        List<(Guid Id, OfertaCursoSnapshot Oferta)> Ofertas,
        List<InsumoDaDistribuicao> Insumos,
        ModalidadesPorOferta Modalidades,
        JsonArray Vagas,
        List<Guid> OfertasEmVagas)
    {
        internal DomainError? Incoerencia() => LeituraDasOfertas.VerificarBlocosDerivados(
            [.. Insumos.Select(static i => i.Oferta)], Modalidades.Ofertas, OfertasEmVagas, [.. Ofertas.Select(static o => o.Id)]);

        internal Result<ConfiguracaoDistribuicaoVagas> Remontar(InsumoDaDistribuicao insumo) =>
            ConfiguracaoDistribuicaoVagas.Criar(
                insumo.Oferta,
                insumo.Declarada(Modalidades.PorOferta[insumo.Oferta]),
                insumo.FatosDoCadastro(Ofertas.First(oferta => oferta.Id == insumo.Oferta).Oferta));
    }
}
