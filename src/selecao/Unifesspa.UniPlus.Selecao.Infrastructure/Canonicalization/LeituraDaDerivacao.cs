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
    /// As regras de derivação (§7.4) — reconstruídas nos três níveis por
    /// <see cref="ConfiguracaoDerivacaoFato.Criar"/>, <see cref="RegraDerivacaoConfigurada.Criar"/> e
    /// <see cref="CondicaoRegraDerivacao.Criar"/> (forma revalidada; Ids novos). A regra âncora tem
    /// <c>quando</c> nulo.
    /// </summary>
    internal static IReadOnlyList<ConfiguracaoDerivacaoFato> LerRegrasDerivacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonArray array = leitor.Array(payload, "regrasDerivacao", "$");
        if (leitor.Falhou)
        {
            return [];
        }

        List<ConfiguracaoDerivacaoFato> configuracoes = [];
        for (int i = 0; i < array.Count; i++)
        {
            ConfiguracaoDerivacaoFato? config = LerConfiguracaoDeDerivacao(
                leitor, leitor.ItemObjeto(array, i, "regrasDerivacao"), $"regrasDerivacao[{i}]");
            if (config is null)
            {
                return [];
            }

            configuracoes.Add(config);
        }

        return configuracoes;
    }

    private static ConfiguracaoDerivacaoFato? LerConfiguracaoDeDerivacao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        leitor.ExigirChaves(item, path, "codigoFato", "regras");

        string codigoFato = leitor.TextoNaoVazio(item, "codigoFato", path, LimitesDoEnvelope.Fato);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<RegraDerivacaoConfigurada> regras = LerRegrasDaDerivacao(leitor, item, path);
        if (leitor.Falhou)
        {
            return null;
        }

        Result<ConfiguracaoDerivacaoFato> config = ConfiguracaoDerivacaoFato.Criar(codigoFato, regras);
        return config.IsFailure ? leitor.Propagar<ConfiguracaoDerivacaoFato?>(config.Error!) : config.Value;
    }

    internal static IReadOnlyList<RegraDerivacaoConfigurada> LerRegrasDaDerivacao(
        LeitorEnvelope leitor, JsonObject configItem, string pathPai)
    {
        JsonArray array = leitor.Array(configItem, "regras", pathPai);
        if (leitor.Falhou)
        {
            return [];
        }

        List<RegraDerivacaoConfigurada> regras = [];
        for (int i = 0; i < array.Count; i++)
        {
            RegraDerivacaoConfigurada? regra = LerRegraDeDerivacao(
                leitor, leitor.ItemObjeto(array, i, $"{pathPai}.regras"), $"{pathPai}.regras[{i}]");
            if (regra is null)
            {
                return [];
            }

            regras.Add(regra);
        }

        return regras;
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

        IReadOnlyList<LinhaDoPredicado> linhas = LeituraDasCondicoes.LerDnf(leitor, item, "quando", path);
        if (leitor.Falhou)
        {
            return null;
        }

        List<CondicaoRegraDerivacao> condicoes = [];
        foreach ((int clausula, string fato, Operador operador, JsonElement valor) in linhas)
        {
            Result<CondicaoRegraDerivacao> condicao = CondicaoRegraDerivacao.Criar(clausula, fato, operador, valor);
            if (condicao.IsFailure)
            {
                return leitor.Propagar<RegraDerivacaoConfigurada?>(condicao.Error!);
            }

            condicoes.Add(condicao.Value!);
        }

        Result<RegraDerivacaoConfigurada> regra = RegraDerivacaoConfigurada.Criar(ordem, contribui, condicoes);
        return regra.IsFailure ? leitor.Propagar<RegraDerivacaoConfigurada?>(regra.Error!) : regra.Value;
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
            ?? ConferirCodigosEOrdens(bloco, coletados, codigosDeGrupo)
            ?? ConferirDerivados(bloco.RegrasDerivacao, coletados, codigosDeGrupo, universo)
            ?? ConferirCitacoes(bloco.RegrasDerivacao, efetivas, universo)
            ?? ConferirTestemunhos(bloco, efetivas, documentosExigidos, formularios, termos, distribuicao, payload);
    }

    // Há uma só semântica de motor vigente e nenhum snapshot congelado em banco: exigir a versão corrente é
    // a leitura honesta enquanto não existe versão legada a preservar.
    private static DomainError? ConferirVersaoDoInterpretador(string versaoInterpretador) =>
        string.Equals(versaoInterpretador, MotorDerivacao.VersaoSemantica, StringComparison.Ordinal)
            ? null
            : Malformado(
                $"'versaoInterpretador' desconhecida: '{versaoInterpretador}' — este sistema resolve a semântica "
                + $"'{MotorDerivacao.VersaoSemantica}'.");

    private static DomainError Malformado(string mensagem) => new(ErrosCodecEnvelope.EnvelopeMalformado, mensagem);

    // Produtor único no processo, contando os campos dos grupos; ordem única dentro de cada
    // formulário, entre itens e grupos, e dentro de cada grupo; código de grupo único no processo e
    // fora do espaço de códigos dos fatos.
    private static DomainError? ConferirCodigosEOrdens(
        BlocoDeFatosEDerivacao bloco,
        HashSet<string> coletados,
        HashSet<string> codigosDeGrupo)
    {
        HashSet<(FinalidadeFormulario, int)> ordens = [];
        foreach (FatoColetado fato in bloco.Fatos)
        {
            if (!coletados.Add(fato.FatoCodigo))
            {
                return Malformado($"'fatosColetados': o fato '{fato.FatoCodigo}' aparece mais de uma vez.");
            }

            if (!ordens.Add((fato.Finalidade, fato.Ordem)))
            {
                return Malformado($"'fatosColetados': a ordem {fato.Ordem} é usada por mais de um fato do mesmo formulário.");
            }
        }

        foreach (GrupoColetado grupo in bloco.Grupos)
        {
            if (ConferirGrupo(grupo, coletados, codigosDeGrupo, ordens) is { } incoerencia)
            {
                return incoerencia;
            }
        }

        // Depois de todos os campos: o código de grupo não coincide com fato coletado, item ou campo.
        return codigosDeGrupo.Overlaps(coletados)
            ? Malformado("'gruposColetados': um código de grupo coincide com o de um fato coletado.")
            : null;
    }

    private static DomainError? ConferirGrupo(
        GrupoColetado grupo,
        HashSet<string> coletados,
        HashSet<string> codigosDeGrupo,
        HashSet<(FinalidadeFormulario, int)> ordens)
    {
        if (!codigosDeGrupo.Add(grupo.Codigo))
        {
            return Malformado($"'gruposColetados': o código '{grupo.Codigo}' aparece em mais de um grupo.");
        }

        if (!ordens.Add((grupo.Finalidade, grupo.Ordem)))
        {
            return Malformado($"'gruposColetados': a ordem {grupo.Ordem} é usada por mais de um item ou grupo do mesmo formulário.");
        }

        HashSet<int> ordensNoGrupo = [];
        foreach (FatoColetado campo in grupo.Subitens)
        {
            if (!coletados.Add(campo.FatoCodigo))
            {
                return Malformado($"'gruposColetados': o fato '{campo.FatoCodigo}' aparece mais de uma vez.");
            }

            if (!ordensNoGrupo.Add(campo.Ordem))
            {
                return Malformado($"'gruposColetados': a ordem {campo.Ordem} é usada por mais de um campo do grupo '{grupo.Codigo}'.");
            }
        }

        return null;
    }

    private static DomainError? ConferirDerivados(
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao,
        HashSet<string> coletados,
        HashSet<string> codigosDeGrupo,
        HashSet<string> universo)
    {
        HashSet<string> derivados = new(StringComparer.Ordinal);
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            if (!derivados.Add(config.CodigoFato))
            {
                return Malformado($"'regrasDerivacao': o fato '{config.CodigoFato}' tem mais de uma configuração de derivação.");
            }

            // A modalidade do candidato e a opção pelas cotas saem da oferta, e o encoder não as grava.
            if (DerivacoesDaOferta.Deriva(config.CodigoFato))
            {
                return Malformado($"'regrasDerivacao': o fato '{config.CodigoFato}' é derivado da oferta de vagas, e não se grava.");
            }

            // O derivado não é fato coletado, item ou campo de grupo, nem grupo.
            if (coletados.Contains(config.CodigoFato) || codigosDeGrupo.Contains(config.CodigoFato))
            {
                return Malformado($"'regrasDerivacao': o código '{config.CodigoFato}' já é de fato coletado ou de grupo.");
            }

            universo.Add(config.CodigoFato);
        }

        return null;
    }

    // Toda citação de regra de derivação gravada existe em coletados e derivados, inclusive os que a
    // oferta define. A completude das citações de gatilho é recusa da fatia de dependência declarada, não
    // desta: o grafo conjunto ignora de propósito a citação a fato ausente.
    private static DomainError? ConferirCitacoes(
        IReadOnlyList<ConfiguracaoDerivacaoFato> regrasDerivacao,
        IReadOnlyList<ConfiguracaoDerivacaoFato> efetivas,
        HashSet<string> universo)
    {
        universo.UnionWith(efetivas.Select(static d => d.CodigoFato));
        foreach (ConfiguracaoDerivacaoFato config in regrasDerivacao)
        {
            CondicaoRegraDerivacao? ausente = config.Regras
                .SelectMany(static regra => regra.Condicoes)
                .FirstOrDefault(condicao => !universo.Contains(condicao.Fato));
            if (ausente is not null)
            {
                return Malformado(
                    $"'regrasDerivacao': a derivação de '{config.CodigoFato}' cita '{ausente.Fato}', que o processo não coleta nem deriva.");
            }
        }

        return null;
    }

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

        if (DivergeDoCongelado(payload, "grafoDependencia", SerializacaoDosFatos.SerializarGrafoDependencia(grafo.Value!)))
        {
            return Malformado("'grafoDependencia' congelado não reproduz o grafo recomputado das partes reidratadas.");
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
        // `"grafoDependencia": null`): o recomputado nunca é nulo, então diverge — recusa fail-closed,
        // nunca um NullReferenceException (500).
        if (payload[chave] is not JsonNode congelado)
        {
            return true;
        }

        // O perfil serializa um JsonObject; envolve-se cada lado num wrapper para comparar array ou
        // objeto pela mesma projeção. O congelado é clonado antes de reparentar — mutá-lo tiraria o
        // bloco do payload que ainda está sendo lido.
        byte[] bytesEsperados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = esperado });
        byte[] bytesCongelados = PerfilCanonicoV1.Instancia.Serializar(new JsonObject { ["v"] = congelado.DeepClone() });
        return !bytesEsperados.AsSpan().SequenceEqual(bytesCongelados);
    }
}
