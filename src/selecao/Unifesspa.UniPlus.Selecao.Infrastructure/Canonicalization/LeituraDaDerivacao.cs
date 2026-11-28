namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura das regras de derivação de fatos e conferência do bloco de fatos contra o congelado.
/// </summary>
internal static class LeituraDaDerivacao
{
    /// <summary>
    /// As regras de derivação, reconstruídas nos três níveis por
    /// <see cref="ConfiguracaoDerivacaoFato.Criar"/>, <see cref="RegraDerivacaoConfigurada.Criar"/> e
    /// <see cref="CondicaoRegraDerivacao.Criar"/> (forma revalidada; Ids novos). A regra âncora tem
    /// <c>quando</c> nulo.
    /// </summary>
    internal static IReadOnlyList<ConfiguracaoDerivacaoFato> LerRegrasDerivacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "calculosDosDados", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        return LeituraDeItens.Ler(leitor, array, "calculosDosDados", LerConfiguracaoDeDerivacao) ?? [];
    }

    private static ConfiguracaoDerivacaoFato? LerConfiguracaoDeDerivacao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "codigoDoDado", "regras");

        string codigoFato = leitor.TextoNaoVazio(item, "codigoDoDado", path, LimitesDoEnvelope.Fato);
        if (leitor.Falhou)
        {
            return null;
        }

        return ConstruirConfiguracao(leitor, item, path, codigoFato);
    }

    private static ConfiguracaoDerivacaoFato? ConstruirConfiguracao(LeitorEnvelope leitor, JsonObject item, string path, string codigoFato)
    {
        IReadOnlyList<RegraDerivacaoConfigurada> regras = LerRegrasDaDerivacao(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<ConfiguracaoDerivacaoFato> config = ConfiguracaoDerivacaoFato.Criar(codigoFato, regras);
        return config.IsFailure ? leitor.Propagar<ConfiguracaoDerivacaoFato?>([.. config.Errors.Select(static causa => causa.Field == "codigoFato"
            ? LeitorEnvelope.NoProprioItem([causa])[0] : causa)], path) : config.Value;
    }

    internal static IReadOnlyList<RegraDerivacaoConfigurada> LerRegrasDaDerivacao(
        LeitorEnvelope leitor, JsonObject configItem, string pathPai)
    {
        JsonArray array = leitor.Array(configItem, "regras", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        return LeituraDeItens.Ler(leitor, array, $"{pathPai}.regras", LerRegraDeDerivacao) ?? [];
    }

    private static RegraDerivacaoConfigurada? LerRegraDeDerivacao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "ordem", "contribui", "quando");

        int ordem = leitor.Inteiro(item, "ordem", path);
        string? contribui = leitor.TextoOpcional(item, "contribui", path, LimitesDoEnvelope.Fato);
        if (leitor.Falhou)
        {
            return null;
        }

        return ConstruirRegraDeDerivacao(leitor, item, path, ordem, contribui);
    }

    private static RegraDerivacaoConfigurada? ConstruirRegraDeDerivacao(
        LeitorEnvelope leitor, JsonObject item, string path, int ordem, string? contribui)
    {
        IReadOnlyList<LinhaDoPredicado> linhas = LeituraDasCondicoes.LerDnf(leitor, item, "quando", path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<List<(int Clausula, CondicaoDnf Condicao)>> lidas = LeituraDasCondicoes.CondicoesDasLinhas(linhas);
        return lidas.IsFailure
            ? RecusarComCondicaoInvalida(leitor, lidas.Errors, path, ordem, contribui)
            : CriarRegraDeDerivacao(leitor, lidas.Value!, path, ordem, contribui);
    }

    private static RegraDerivacaoConfigurada? CriarRegraDeDerivacao(
        LeitorEnvelope leitor, IReadOnlyList<(int Clausula, CondicaoDnf Condicao)> linhas, string path, int ordem, string? contribui)
    {
        List<CondicaoRegraDerivacao>? condicoes = CriarCondicoesDaDerivacao(leitor, linhas, $"{path}.quando");
        if (condicoes is null)
        {
            return null;
        }

        Result<RegraDerivacaoConfigurada> regra = RegraDerivacaoConfigurada.Criar(ordem, contribui, condicoes);
        return regra.IsFailure ? leitor.Propagar<RegraDerivacaoConfigurada?>(regra.Errors, path) : regra.Value;
    }

    // A ordem e o campo que contribui não dependem das condições; a regra é conferida sem elas e descartada.
    private static RegraDerivacaoConfigurada? RecusarComCondicaoInvalida(
        LeitorEnvelope leitor, IReadOnlyList<FieldError> condicoesRecusadas, string path, int ordem, string? contribui)
    {
        Result<RegraDerivacaoConfigurada> regra = RegraDerivacaoConfigurada.Criar(ordem, contribui, []);
        IReadOnlyList<FieldError> causas = [.. condicoesRecusadas.Select(causa => causa.NoCaminhoDoItem("quando")), .. regra.Errors];
        return leitor.PropagarCausasDoItem<RegraDerivacaoConfigurada?>(causas, path);
    }

    private static List<CondicaoRegraDerivacao>? CriarCondicoesDaDerivacao(
        LeitorEnvelope leitor, IReadOnlyList<(int Clausula, CondicaoDnf Condicao)> linhas, string caminho)
    {
        List<CondicaoRegraDerivacao> condicoes = [];
        foreach ((int clausula, CondicaoDnf condicao) in linhas)
        {
            Result<CondicaoRegraDerivacao> criada = CondicaoRegraDerivacao.Criar(clausula, condicao.Fato, condicao.Operador, condicao.Valor);
            if (criada.IsFailure)
            {
                return leitor.Propagar<List<CondicaoRegraDerivacao>?>(criada.Errors, caminho);
            }
            condicoes.Add(criada.Value!);
        }
        return condicoes;
    }

    /// <summary>
    /// Recusa fail-closed do bloco de coleta/derivação (congelamento dos parâmetros na publicação, UNI-REQ-0019): um envelope válido nunca declara fato
    /// duplicado, grava derivação que a oferta define, cita fato inexistente, fecha ciclo, nem
    /// congela um grafo/modalidades que não reproduzem o recomputado. Um envelope adulterado que o
    /// faça é recusado como malformado — o grafo conjunto ignora deliberadamente referências ausentes
    /// (projeta só o que existe), então o testemunho por si só não prova a completude do vocabulário
    /// citado; estas checagens fecham o que o grafo não prova. Devolve <see langword="null"/> quando o
    /// bloco é íntegro.
    /// </summary>
    internal static DomainError? ValidarBlocoDeFatosEDerivacao(
        BlocoDeFatosEDerivacao bloco,
        IReadOnlyList<DocumentoExigido> documentosExigidos,
        IReadOnlyList<FormularioProcesso> formularios,
        IReadOnlyList<TermoExigidoFormulario> termos,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao,
        JsonObject payload)
    {
        HashSet<string> coletados = new(StringComparer.Ordinal);
        HashSet<string> codigosDeGrupo = new(StringComparer.Ordinal);

        // A derivação cita item ou derivado, nunca campo de grupo repetível, que existe por ocorrência.
        HashSet<string> universo = new(bloco.Fatos.Select(static f => f.FatoCodigo), StringComparer.Ordinal);

        // As regras da oferta não são conferidas aqui: no resultado importado elas não são avaliadas, e a
        // publicação já as conferiu.
        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas = DerivacoesDaOferta.Efetivas(bloco.RegrasDerivacao, distribuicao);
        return ConferirVersaoDoInterpretador(bloco.VersaoInterpretador)
            ?? ConferenciaDoVocabularioDeFatos.ConferirCodigosEOrdens(bloco, coletados, codigosDeGrupo)
            ?? ConferenciaDoVocabularioDeFatos.ConferirDerivados(bloco.RegrasDerivacao, coletados, codigosDeGrupo, universo)
            ?? ConferenciaDoVocabularioDeFatos.ConferirCitacoes(bloco.RegrasDerivacao, efetivas, universo)
            ?? ConferirTestemunhos(bloco, efetivas, documentosExigidos, formularios, termos, distribuicao, payload);
    }

    // A leitura exige a versão corrente da semântica de cálculo, a única que este sistema resolve.
    private static DomainError? ConferirVersaoDoInterpretador(string versaoInterpretador) =>
        string.Equals(versaoInterpretador, CalculoDosFatosDerivados.VersaoSemantica, StringComparison.Ordinal)
            ? null
            : Malformado(
                "'versaoDasRegrasDeCalculo' desconhecida — este sistema resolve a semântica "
                + $"'{CalculoDosFatosDerivados.VersaoSemantica}'.");

    private static DomainError Malformado(string mensagem) => new(ErrosCodecEnvelope.EnvelopeMalformado, mensagem);

    // Aciclicidade do grafo conjunto e testemunho: o grafo e as modalidades congelados têm de
    // reproduzir exatamente o recomputado das partes reidratadas, byte a byte, pela mesma projeção.
    private static DomainError? ConferirTestemunhos(
        BlocoDeFatosEDerivacao bloco,
        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas,
        IReadOnlyList<DocumentoExigido> documentosExigidos,
        IReadOnlyList<FormularioProcesso> formularios,
        IReadOnlyList<TermoExigidoFormulario> termos,
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao,
        JsonObject payload)
    {
        Result<GrafoDependenciaConjunta> grafo =
            GrafoDependenciaConjunta.Construir(bloco.Fatos, efetivas, documentosExigidos, formularios, termos, bloco.Grupos);
        if (grafo.IsFailure)
        {
            return grafo.Error;
        }

        return ConferirGrafoEModalidades(payload, grafo.Value!, distribuicao);
    }

    private static DomainError? ConferirGrafoEModalidades(
        JsonObject payload, GrafoDependenciaConjunta grafo, IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao)
    {
        if (DivergeDoCongelado(payload, "dependenciasEntreItens", SerializacaoDosFatos.SerializarGrafoDependencia(grafo)))
        {
            return Malformado("'dependenciasEntreItens' congelado não reproduz o grafo recomputado das partes reidratadas.");
        }

        return DivergeDoCongelado(payload, "modalidadesOfertadas", SerializacaoDosFatos.SerializarModalidadesOfertadas(distribuicao))
            ? Malformado("'modalidadesOfertadas' congelado não reproduz o conjunto recomputado da distribuição.")
            : null;
    }

    /// <summary>
    /// Compara o bloco congelado com o esperado recomputado — ambos pela MESMA projeção canônica
    /// (<see cref="PerfilCanonicoV1"/>), imune à ordem de chaves. É o testemunho: o congelado não é
    /// segunda fonte de verdade, e sim uma cópia verificável do que as partes reidratadas reproduzem.
    /// </summary>
    internal static bool DivergeDoCongelado(JsonObject payload, string chave, JsonNode esperado)
    {
        // Bloco ausente ou JSON `null` (uma coluna adulterada com hash recomputado consegue produzir
        // `"dependenciasEntreItens": null`): o recomputado nunca é nulo, então diverge — recusa fail-closed,
        // nunca um NullReferenceException (500).
        if (payload[chave] is not JsonNode congelado)
        {
            return true;
        }

        // O perfil serializa um JsonObject; envolve-se cada lado num wrapper para comparar array ou
        // objeto pela mesma projeção. O congelado é copiado antes de reparentar — mutá-lo tiraria o
        // bloco do payload que ainda está sendo lido. O contexto fica fora da comparação.
        byte[] bytesEsperados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = ContextoDosRotulos.SemContexto(esperado) });
        byte[] bytesCongelados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = ContextoDosRotulos.SemContexto(congelado) });
        return !bytesEsperados.AsSpan().SequenceEqual(bytesCongelados);
    }
}
