namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;

/// <summary>
/// Leitura dos fatos derivados congelados.
/// </summary>
internal static class LeituraDosFatosDerivados
{
    /// <summary>
    /// Os fatos derivados do catálogo, um por código, com o nome, a descrição, o domínio, a cardinalidade e a
    /// proteção sempre presentes: o bloco é a fonte da conferência da proteção das exigências e do que a
    /// consulta da inscrição diz de cada derivado, e a ausência de um campo não pode virar valor padrão.
    /// </summary>
    internal static Dictionary<string, FatoDerivadoCongelado> LerFatosDerivados(LeitorEnvelope leitor, JsonObject payload)
    {
        Dictionary<string, FatoDerivadoCongelado> fatos = new(StringComparer.Ordinal);
        if (leitor.Falhou)
        {
            return fatos;
        }

        JsonArray array = leitor.Array(payload, "fatosDerivados", "$");
        for (int i = 0; !leitor.Falhou && i < array.Count; i++)
        {
            FatoDerivadoCongelado? fato = LerFatoDerivado(leitor, leitor.ItemObjeto(array, i, "fatosDerivados"), $"fatosDerivados[{i}]");
            if (fato is null)
            {
                break;
            }

            if (!fatos.TryAdd(fato.Codigo, fato))
            {
                leitor.Recusar<object>($"Envelope malformado em 'fatosDerivados': o fato '{fato.Codigo}' aparece mais de uma vez.");
                break;
            }
        }

        return fatos;
    }

    private static FatoDerivadoCongelado? LerFatoDerivado(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(
            item, path, "codigo", "nome", "descricao", "componenteExibicao", "dominio", "cardinalidade", "classificacaoProtecao", "hipoteseLegal",
            "finalidadeTratamento");
        string codigo = leitor.TextoNaoVazio(item, "codigo", path, LimitesDoEnvelope.Fato);
        string nome = leitor.TextoNaoVazio(item, "nome", path, LimitesDoEnvelope.NomeDeCadastro);
        string descricao = leitor.TextoNaoVazio(item, "descricao", path, LimitesDoEnvelope.DescricaoDoFato);
        string? componente = leitor.TextoOpcional(item, "componenteExibicao", path, LimitesDoEnvelope.ComponenteExibicao);
        string dominio = leitor.TextoNaoVazio(item, "dominio", path, LimitesDoEnvelope.Token);
        string cardinalidade = leitor.TextoNaoVazio(item, "cardinalidade", path, LimitesDoEnvelope.Token);
        string classificacao = leitor.TextoNaoVazio(item, "classificacaoProtecao", path, LimitesDoEnvelope.Token);
        string hipoteseLegal = leitor.TextoNaoVazio(item, "hipoteseLegal", path, LimitesDoEnvelope.HipoteseLegal);
        string finalidade = leitor.TextoNaoVazio(item, "finalidadeTratamento", path, LimitesDoEnvelope.FinalidadeTratamento);
        if (leitor.Falhou)
        {
            return null;
        }

        string? foraDoVocabulario =
            !ClassificacoesDeProtecao.TryAnalisar(classificacao, out _) ? "classificacaoProtecao"
            : !DominioDoCatalogo.Dominios.Contains(dominio) ? "dominio"
            : !DominioDoCatalogo.Cardinalidades.Contains(cardinalidade) ? "cardinalidade"
            : !DominioDoCatalogo.HipotesesLegais.Contains(hipoteseLegal) ? "hipoteseLegal"
            : componente is not null && !DominioDoCatalogo.ComponentesDeExibicao.Contains(componente) ? "componenteExibicao"
            : null;

        return foraDoVocabulario is null
            ? new FatoDerivadoCongelado(codigo, nome, descricao, componente, dominio, cardinalidade, classificacao, hipoteseLegal, finalidade)
            : leitor.Recusar<FatoDerivadoCongelado?>($"'{path}.{foraDoVocabulario}' fora do vocabulário fechado.");
    }
}
