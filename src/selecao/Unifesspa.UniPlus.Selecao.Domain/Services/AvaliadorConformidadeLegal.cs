namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Diagnostics;
using System.Text;
using System.Text.Json;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Avalia um conjunto de <see cref="ObrigatoriedadeLegal"/> já resolvido
/// (vigente para o tipo do processo, na data de corte) contra o estado vivo
/// de um <see cref="ProcessoSeletivo"/> (Story #853 §3.1/§3.2). Domain
/// service <b>puro</b>: recebe a lista de regras já filtrada pelo chamador
/// (Application, que tem o repositório — ADR-0042) e nunca lê relógio
/// (ADR-0068) nem I/O.
/// </summary>
/// <remarks>
/// O switch cobre as 7 variantes de <see cref="PredicadoObrigatoriedade"/>
/// explicitamente por tipo — <c>BonusObrigatorio</c>, oitava variante
/// original, foi descartada (ADR-0114, executado por esta story):
/// <c>ConfiguracaoBonusRegional</c> é global ao processo, sem lista de
/// modalidades, tornando a variante incompatível com o agregado real.
/// <b>Correção sobre o xmldoc de <see cref="PredicadoObrigatoriedade"/></b>:
/// o padrão "CS8509 sem catch-all" que o projeto usa para <c>enum</c> (ex.
/// <see cref="Unifesspa.UniPlus.Selecao.Domain.Enums.TipoDominioFatoCodigo"/>)
/// não se aplica aqui — o Roslyn não prova exaustividade de switch sobre uma
/// hierarquia de classes/records aberta (mesmo com todo derivado
/// <c>sealed</c>), só sobre o conjunto fechado de valores de um <c>enum</c>.
/// O braço final é um discard que lança <see cref="UnreachableException"/>: uma
/// 8ª variante ainda compilaria, mas falharia alto em runtime em vez de ser
/// silenciosamente ignorada.
/// </remarks>
/// <remarks>
/// Nenhum ramo compara <see cref="ProcessoSeletivo.Tipo"/> nem qualquer
/// rótulo institucional — a flexibilidade entre tipos de processo vem
/// inteiramente de quais regras o cadastro tem vigentes para
/// <paramref name="tipoProcessoCodigoAvaliado"/>, nunca de um <c>if</c>
/// aqui dentro.
/// </remarks>
public static class AvaliadorConformidadeLegal
{
    public static ResultadoConformidade Avaliar(
        ProcessoSeletivo processo,
        string tipoProcessoCodigoAvaliado,
        IReadOnlyList<ObrigatoriedadeLegal> regras)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoProcessoCodigoAvaliado);
        ArgumentNullException.ThrowIfNull(regras);

        List<RegraAvaliada> avaliadas = new(regras.Count);
        List<string> avisos = [];

        foreach (ObrigatoriedadeLegal regra in regras)
        {
            (bool aprovada, string? motivo, string? aviso) = AvaliarPredicado(processo, regra.Predicado);

            avaliadas.Add(new RegraAvaliada(
                regra.Id,
                regra.RegraCodigo,
                regra.Categoria,
                tipoProcessoCodigoAvaliado,
                regra.Predicado,
                aprovada,
                motivo,
                regra.BaseLegal,
                regra.AtoNormativoUrl,
                regra.PortariaInternaCodigo,
                regra.DescricaoHumana,
                regra.VigenciaInicio,
                regra.VigenciaFim,
                regra.Hash));

            if (aviso is not null)
            {
                avisos.Add($"{regra.RegraCodigo}: {aviso}");
            }
        }

        return new ResultadoConformidade(avaliadas, avisos);
    }

    /// <summary>
    /// Devolve o veredicto, um motivo nomeado quando reprova (CA-02/CA-03/CA-09 — a razão
    /// específica, não só um booleano) e, quando aplicável, uma mensagem de aviso informativo
    /// independente da aprovação — nunca lança, mesmo para payload malformado de
    /// <see cref="Customizado"/>.
    /// </summary>
    private static (bool Aprovada, string? Motivo, string? Aviso) AvaliarPredicado(
        ProcessoSeletivo processo, PredicadoObrigatoriedade predicado) => predicado switch
    {
        EtapaObrigatoria p => AvaliarEtapaObrigatoria(processo, p),
        ModalidadesMinimas p => AvaliarModalidadesMinimas(processo, p),
        DesempateDeveIncluir p => AvaliarDesempateDeveIncluir(processo, p),
        DocumentoObrigatorioParaModalidade p => AvaliarDocumentoObrigatorioParaModalidade(processo, p),
        AtendimentoDisponivel p => AvaliarAtendimentoDisponivel(processo, p),
        ConcorrenciaDuplaObrigatoria => AvaliarConcorrenciaDuplaObrigatoria(processo),
        Customizado => (true, null, "predicado customizado — aprovado por padrão, sem verificação automática"),
        _ => throw new UnreachableException(
            $"Predicado {predicado.GetType().Name} não é uma das 7 variantes reconhecidas por este avaliador."),
    };

    private static (bool, string?, string?) AvaliarEtapaObrigatoria(ProcessoSeletivo processo, EtapaObrigatoria predicado)
    {
        bool aprovada = processo.Etapas.Any(
            e => string.Equals(e.Nome, predicado.TipoEtapaCodigo, StringComparison.OrdinalIgnoreCase));
        return (aprovada, aprovada ? null : $"etapa '{predicado.TipoEtapaCodigo}' ausente", null);
    }

    /// <summary>
    /// §3.1: avalia POR OFERTA — aprova sse TODA <c>ConfiguracaoDistribuicaoVagas</c>
    /// contém todas as modalidades exigidas. Sem nenhuma oferta cadastrada, não há
    /// o que reprovar (contraprova de indistinguibilidade do processo de
    /// importação externa, Story #851 §3.4) — aprova vazio.
    /// </summary>
    private static (bool, string?, string?) AvaliarModalidadesMinimas(ProcessoSeletivo processo, ModalidadesMinimas predicado)
    {
        foreach (ConfiguracaoDistribuicaoVagas oferta in processo.DistribuicaoVagas)
        {
            HashSet<string> codigosDaOferta = new(
                oferta.Modalidades.Select(static m => m.Codigo), StringComparer.Ordinal);

            string[] ausentes = [.. predicado.Codigos.Where(c => !codigosDaOferta.Contains(c))];
            if (ausentes.Length > 0)
            {
                return (false, $"oferta {oferta.Id} não contém a(s) modalidade(s) {string.Join(", ", ausentes)}", null);
            }
        }

        return (true, null, null);
    }

    /// <summary>
    /// Story #554 (PR #903, issue #548): gate real, substitui a reprovação conservadora que
    /// vigorou enquanto o bloco <c>documentosExigidos.exigencias</c> era stub (guarda
    /// B-01, removida junto desta task). Aprova sse existir uma <see cref="DocumentoExigido"/>
    /// do tipo pedido que cubra a modalidade INCONDICIONALMENTE.
    /// </summary>
    /// <remarks>
    /// <para>
    /// "Incondicionalmente" é a parte que faz este gate diferente do resolvedor de
    /// exigências documentais (que avalia contra um candidato REAL, com todos os fatos
    /// dele resolvidos): aqui não há candidato — só a modalidade em si. Uma exigência
    /// GERAL cobre qualquer modalidade, por definição. Uma CONDICIONAL só cobre a
    /// modalidade avaliada se o predicado DNF casar usando <b>somente</b> o fato sintético
    /// <c>MODALIDADE = predicado.Modalidade</c> — se a exigência também depender de outro
    /// fato (ex.: <c>FAIXA_ETARIA</c>), <see cref="PredicadoDnf.Avaliar"/> trata esse fato
    /// como ausente e reprova a cláusula (conservador, nunca lança): nem todo candidato da
    /// modalidade seria coberto, e é exatamente essa parcialidade que a obrigação legal —
    /// "a modalidade X DEVE exigir o documento Y", sem exceção — não admite.
    /// </para>
    /// <para>
    /// Modalidade não ofertada em nenhuma oferta do processo: nada a exigir, aprova vazio
    /// (mesmo espírito de <see cref="AvaliarModalidadesMinimas"/> sem nenhuma oferta
    /// cadastrada).
    /// </para>
    /// </remarks>
    private static (bool, string?, string?) AvaliarDocumentoObrigatorioParaModalidade(
        ProcessoSeletivo processo, DocumentoObrigatorioParaModalidade predicado)
    {
        bool modalidadeOfertada = processo.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Any(m => string.Equals(m.Codigo, predicado.Modalidade, StringComparison.Ordinal));
        if (!modalidadeOfertada)
        {
            return (true, null, null);
        }

        Dictionary<string, JsonElement> fatoDaModalidade = new()
        {
            ["MODALIDADE"] = JsonSerializer.SerializeToElement(predicado.Modalidade),
        };

        // Achado de revisão (Story #554, PR #903): uma exigência que casa por tipo e cobre a
        // modalidade incondicionalmente, mas não DeterminaResultado() (não é obrigatória
        // nem tem consequência de indeferimento), é meramente opcional — não satisfaz a
        // obrigação legal "a modalidade X DEVE exigir o documento Y".
        bool cobertaIncondicionalmente = processo.DocumentosExigidos.Any(e =>
            string.Equals(e.TipoDocumentoCodigo, predicado.TipoDocumento, StringComparison.Ordinal)
            && e.DeterminaResultado()
            && e.AplicavelPara(fatoDaModalidade));

        return cobertaIncondicionalmente
            ? (true, null, null)
            : (false, $"nenhuma exigência documental do tipo '{predicado.TipoDocumento}' cobre incondicionalmente a modalidade '{predicado.Modalidade}'", null);
    }

    private static (bool, string?, string?) AvaliarDesempateDeveIncluir(ProcessoSeletivo processo, DesempateDeveIncluir predicado)
    {
        bool aprovada = processo.CriteriosDesempate.Any(
            c => string.Equals(c.Regra.Codigo, predicado.Criterio, StringComparison.Ordinal));
        return (aprovada, aprovada ? null : $"critério de desempate '{predicado.Criterio}' ausente", null);
    }

    private static (bool, string?, string?) AvaliarAtendimentoDisponivel(ProcessoSeletivo processo, AtendimentoDisponivel predicado)
    {
        if (processo.OfertaAtendimento is null)
        {
            return (false, "nenhuma oferta de atendimento especializado cadastrada", null);
        }

        HashSet<string> ofertados = new(
            processo.OfertaAtendimento.TiposDeficiencia.Select(
                static t => t.TipoDeficienciaNome.Normalize(NormalizationForm.FormC)),
            StringComparer.OrdinalIgnoreCase);

        string[] ausentes = [.. predicado.Necessidades
            .Where(necessidade => !ofertados.Contains(necessidade.Normalize(NormalizationForm.FormC)))];

        return ausentes.Length == 0
            ? (true, null, null)
            : (false, $"necessidade(s) de atendimento não ofertada(s): {string.Join(", ", ausentes)}", null);
    }

    private static (bool, string?, string?) AvaliarConcorrenciaDuplaObrigatoria(ProcessoSeletivo processo)
    {
        bool aprovada = processo.ConcorrenciaDuplaAplicavel();
        return (aprovada, aprovada ? null : "nenhuma modalidade de cota reservada (CotaReservada) cadastrada", null);
    }
}
