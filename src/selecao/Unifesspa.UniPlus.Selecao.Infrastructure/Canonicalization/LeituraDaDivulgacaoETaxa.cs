namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Publicacao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Leitura dos blocos de divulgação dos resultados e de taxa de inscrição.
/// </summary>
internal static class LeituraDaDivulgacaoETaxa
{
    /// <summary>
    /// Divulgação pública do certame (UNI-REQ-0050), em forma fechada. A exigência das chaves roda antes de
    /// qualquer retorno antecipado, o que fecha a gramática do bloco: chave desconhecida ou ausente é recusada.
    /// </summary>
    /// <remarks>
    /// O decodificador é tão estrito quanto o encoder: vocabulário fechado, piso
    /// <c>numero_inscricao</c> sempre presente e <c>nome</c>/<c>nome_abreviado</c> nunca juntos
    /// são reconferidos por <see cref="ConfiguracaoDivulgacao.Criar"/>, que este método chama
    /// como fonte única daquelas invariantes. O que só a LEITURA pode conferir — porque o
    /// congelamento nunca as violaria pelo caminho normal de escrita — fica aqui: repetição,
    /// ordem canônica (pela MESMA política do encoder,
    /// <see cref="EscritaCanonica.OrdenarPorConteudo(IEnumerable{JsonValue})"/>,
    /// nunca um <see cref="StringComparer.Ordinal"/> reimplementado), a bicondicional entre
    /// <c>nome_abreviado</c> e <c>regraNomeAbreviado</c>, o identificador de regra conhecido, e a
    /// forma canônica (Trim + NFC) da justificativa. Um bloco cujo conteúdo é exatamente o
    /// default minimizado reidrata como <see langword="null"/> — a restauração não fabrica
    /// entidade para um processo que nunca configurou divulgação.
    /// </remarks>
    internal static ConfiguracaoDivulgacao? LerDivulgacao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "divulgacao", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        leitor.ExigirChaves(bloco, "divulgacao", "camposPublicos", "regraNomeAbreviado", "justificativa");

        IReadOnlyList<string> camposPublicos = leitor.Textos(bloco, "camposPublicos", "divulgacao");
        string? regraNomeAbreviado = leitor.TextoOpcional(bloco, "regraNomeAbreviado", "divulgacao");
        string? justificativa = leitor.TextoOpcional(bloco, "justificativa", "divulgacao", LimitesDoEnvelope.Justificativa);
        if (leitor.Falhou)
        {
            return null;
        }

        if (FormaNaoCanonica(camposPublicos, regraNomeAbreviado, justificativa) is { } mensagem)
        {
            return leitor.Recusar<ConfiguracaoDivulgacao?>(mensagem);
        }

        if (ConfiguracaoDivulgacao.EhDefaultMinimizado(camposPublicos, justificativa))
        {
            return null;
        }

        Result<ConfiguracaoDivulgacao> configuracao = ConfiguracaoDivulgacao.Criar(camposPublicos, justificativa);
        return configuracao.IsFailure ? leitor.Propagar<ConfiguracaoDivulgacao>(configuracao.Error!) : configuracao.Value;
    }

    private static string? FormaNaoCanonica(IReadOnlyList<string> camposPublicos, string? regraNomeAbreviado, string? justificativa)
    {
        if (camposPublicos.Distinct(StringComparer.Ordinal).Count() != camposPublicos.Count)
        {
            return "'divulgacao.camposPublicos' tem token repetido.";
        }

        // A mesma política do encoder, nunca um comparador próprio: ele coincidiria hoje e divergiria no
        // dia em que o vocabulário crescesse com um token não-ASCII. Não reordena: fora de ordem é malformado.
        if (!NaOrdemCanonica(camposPublicos))
        {
            return "'divulgacao.camposPublicos' não está na ordem canônica.";
        }

        if (camposPublicos.Contains(ConfiguracaoDivulgacao.NomeAbreviado, StringComparer.Ordinal) != (regraNomeAbreviado is not null))
        {
            return "'divulgacao.regraNomeAbreviado' tem de estar presente se, e somente se, 'camposPublicos' contém 'nome_abreviado'.";
        }

        if (regraNomeAbreviado is not null && !RegrasDeNomeAbreviado.EhConhecida(regraNomeAbreviado))
        {
            return $"'divulgacao.regraNomeAbreviado' não é uma regra conhecida: '{regraNomeAbreviado}'.";
        }

        return JustificativaForaDaForma(justificativa);
    }

    // O codificador nunca emite justificativa vazia nem só espaços: quando não há, emite null. A string
    // vazia é recusada antes da conferência de forma, porque já é a sua própria forma canônica.
    private static string? JustificativaForaDaForma(string? justificativa)
    {
        if (justificativa is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(justificativa))
        {
            return "'divulgacao.justificativa' não pode ser vazia nem só espaços — o codificador nunca emite essa forma, só null.";
        }

        return string.Equals(HashCanonicalComputer.NormalizeNfc(justificativa.Trim()), justificativa, StringComparison.Ordinal)
            ? null
            : "'divulgacao.justificativa' não está na forma canônica (sem espaço nas bordas, em NFC).";
    }

    /// <summary>
    /// Taxa de inscrição e isenção. Como o bloco de bônus regional, declara <c>presente</c>, mas
    /// <c>presente:false</c> nunca é um estado válido aqui: a publicação recusa a configuração sem taxa
    /// declarada, então nenhum envelope que passou por ela congela essa forma. Decodificá-la como "sem taxa
    /// declarada" repetiria, na leitura, o default silencioso que a escrita existe para banir.
    /// </summary>
    /// <remarks>
    /// O decodificador é tão estrito quanto o encoder (mesmo raciocínio de
    /// <see cref="LeituraDaDivulgacaoETaxa.LerDivulgacao"/>): vocabulário fechado de <c>fundamentos</c> (token desconhecido
    /// é malformado, nunca ignorado), sem repetição, na MESMA ordem canônica que
    /// <see cref="EscritaCanonica.OrdenarPorConteudo(IEnumerable{JsonValue})"/>
    /// usaria — o encoder nunca embaralha porque <see cref="ConfiguracaoTaxaInscricao.Criar"/> já
    /// deduplica e ordena antes de guardar; achar duplicata ou ordem diferente aqui é sinal de
    /// bytes que não vieram desse caminho. <c>cobra:true</c> com <c>fundamentos:[]</c> é a mesma
    /// classe de impossível: a fábrica recusa a combinação, então o encoder nunca
    /// a emite.
    /// </remarks>
    internal static ConfiguracaoTaxaInscricao? LerTaxaInscricao(LeitorEnvelope leitor, JsonObject payload)
    {
        JsonObject bloco = leitor.Objeto(payload, "taxaInscricao", "$");
        if (leitor.Falhou)
        {
            return null;
        }

        bool presente = leitor.Booleano(bloco, "presente", "taxaInscricao");
        if (leitor.Falhou)
        {
            return null;
        }

        if (!presente)
        {
            return leitor.Recusar<ConfiguracaoTaxaInscricao?>("'taxaInscricao.presente' não pode ser falso — CA-01 recusa publicar sem declarar taxa.");
        }

        return LerTaxaPresente(leitor, bloco);
    }

    private static ConfiguracaoTaxaInscricao? LerTaxaPresente(LeitorEnvelope leitor, JsonObject bloco)
    {
        leitor.ExigirChaves(bloco, "taxaInscricao", "presente", "cobra", "valor", "fundamentos");

        bool cobra = leitor.Booleano(bloco, "cobra", "taxaInscricao");
        decimal? valor = leitor.DecimalOpcional(bloco, "valor", ConfiguracaoTaxaInscricao.ValorEscala, "taxaInscricao", LimitesDoEnvelope.PrecisaoTaxaInscricao);
        IReadOnlyList<string> fundamentos = leitor.Textos(bloco, "fundamentos", "taxaInscricao");
        if (leitor.Falhou)
        {
            return null;
        }

        if (FundamentosForaDaForma(fundamentos) is { } mensagem)
        {
            return leitor.Recusar<ConfiguracaoTaxaInscricao?>(mensagem);
        }

        Result<ConfiguracaoTaxaInscricao> configuracao = ConfiguracaoTaxaInscricao.Criar(cobra, valor, fundamentos);
        return configuracao.IsFailure ? leitor.Propagar<ConfiguracaoTaxaInscricao>(configuracao.Error!) : configuracao.Value;
    }

    private static bool NaOrdemCanonica(IReadOnlyList<string> tokens) =>
        EscritaCanonica
            .OrdenarPorConteudo(tokens.Select(static t => JsonValue.Create(t)!))
            .Select(static n => n!.GetValue<string>())
            .SequenceEqual(tokens, StringComparer.Ordinal);

    private static string? FundamentosForaDaForma(IReadOnlyList<string> fundamentos)
    {
        if (fundamentos.Distinct(StringComparer.Ordinal).Count() != fundamentos.Count)
        {
            return "'taxaInscricao.fundamentos' tem token repetido.";
        }

        return NaOrdemCanonica(fundamentos)
            ? null
            : "'taxaInscricao.fundamentos' não está na ordem canônica.";
    }
}
