namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Leitura dos fatos derivados congelados.
/// </summary>
internal static class LeituraDosFatosDerivados
{
    /// <summary>
    /// Os fatos derivados do catálogo, um por código, com o nome, a descrição, o tipo do dado, a quantidade de
    /// valores e a proteção sempre presentes: o bloco é a fonte da conferência da proteção das exigências e do que
    /// a consulta da inscrição diz de cada derivado, e a ausência de um campo não pode virar valor padrão.
    /// </summary>
    internal static Dictionary<string, FatoDerivadoCongelado> LerFatosDerivados(LeitorEnvelope leitor, JsonObject payload)
    {
        Dictionary<string, FatoDerivadoCongelado> fatos = new(StringComparer.Ordinal);
        if (leitor.Falhou)
        {
            return fatos;
        }

        LerCatalogoDeFatos(leitor, leitor.Array(payload, "dadosCalculados", "$"), fatos);
        return fatos;
    }

    private static void LerCatalogoDeFatos(LeitorEnvelope leitor, JsonArray array, Dictionary<string, FatoDerivadoCongelado> fatos)
    {
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            FatoDerivadoCongelado? fato = LerFatoDerivado(leitor, leitor.ItemObjeto(array, i, "dadosCalculados"), $"dadosCalculados[{i}]");
            if (fato is null || !AdicionarFatoUnico(leitor, fatos, fato))
            {
                break;
            }
        }
    }

    private static bool AdicionarFatoUnico(LeitorEnvelope leitor, Dictionary<string, FatoDerivadoCongelado> fatos, FatoDerivadoCongelado fato)
    {
        if (fatos.TryAdd(fato.Codigo, fato))
        {
            return true;
        }

        leitor.Recusar<object>($"Envelope malformado em 'dadosCalculados': o fato '{fato.Codigo}' aparece mais de uma vez.");
        return false;
    }

    private static FatoDerivadoCongelado? LerFatoDerivado(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(
            item, path, "codigo", "nome", "descricao", "componenteExibicao", "tipo", "quantidadeDeValores", "classificacaoProtecao", "hipoteseLegal",
            "finalidadeTratamento");
        FatoDerivadoCongelado? fato = LerCampos(leitor, item, path);
        string? foraDoVocabulario = fato is null ? null : CampoForaDoVocabulario(fato);
        return foraDoVocabulario is null || fato is null
            ? fato
            : leitor.Recusar<FatoDerivadoCongelado?>($"'{path}.{foraDoVocabulario}' fora do vocabulário fechado.");
    }

    private static FatoDerivadoCongelado? LerCampos(LeitorEnvelope leitor, JsonObject item, string path)
    {
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.Fato);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.NomeDeCadastro);
        string descricao = leitor.TextoNaoVazio(item, "descricao", path, LimitesDoEnvelope.DescricaoDoFato);
        string? componente = leitor.TextoOpcional(item, "componenteExibicao", path, LimitesDoEnvelope.ComponenteExibicao);
        TipoDoDado dominio = leitor.CodigoDeContrato<TipoDoDado>(item, "tipo", path);
        QuantidadeDeValores cardinalidade = leitor.CodigoDeContrato<QuantidadeDeValores>(item, "quantidadeDeValores", path);
        string classificacao = leitor.TextoNaoVazio(item, "classificacaoProtecao", path, LimitesDoEnvelope.Token);
        string hipoteseLegal = leitor.TextoNaoVazio(item, "hipoteseLegal", path, LimitesDoEnvelope.HipoteseLegal);
        string finalidade = leitor.TextoNaoVazio(item, "finalidadeTratamento", path, LimitesDoEnvelope.FinalidadeTratamento);
        return leitor.Falhou
            ? null
            : new FatoDerivadoCongelado(codigo, nome, descricao, componente, dominio, cardinalidade, classificacao, hipoteseLegal, finalidade);
    }

    private static string? CampoForaDoVocabulario(FatoDerivadoCongelado fato) =>
        !ClassificacoesDeProtecao.TryAnalisar(fato.ClassificacaoProtecao, out _) ? "classificacaoProtecao"
        : !CatalogoDe<HipoteseLegalTratamento>.TentarObter(fato.HipoteseLegal, out _) ? "hipoteseLegal"
        : fato.ComponenteExibicao is not null && !CatalogoDe<ComponenteDeExibicao>.TentarObter(fato.ComponenteExibicao, out _) ? "componenteExibicao"
        : null;
}
