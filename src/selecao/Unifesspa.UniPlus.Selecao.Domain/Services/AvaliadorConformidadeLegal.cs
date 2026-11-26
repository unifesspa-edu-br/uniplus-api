namespace Unifesspa.UniPlus.Selecao.Domain.Services;

using System.Diagnostics;
using System.Text.Json;

using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Vagas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using Desfecho = (bool Aprovada, string? Motivo, string? Aviso);

/// <summary>
/// Avalia um conjunto de <see cref="ObrigatoriedadeLegal"/> já resolvido
/// (vigente para o tipo do processo, na data de corte) contra o estado vivo
/// de um <see cref="ProcessoSeletivo"/>. Domain
/// service <b>puro</b>: recebe a lista de regras já filtrada pelo chamador
/// (Application, que tem o repositório — ADR-0042) e nunca lê relógio
/// (ADR-0068) nem I/O.
/// </summary>
/// <remarks>
/// O switch cobre as 7 variantes de <see cref="PredicadoObrigatoriedade"/>
/// explicitamente por tipo — <c>FadaObrigatorio</c>, oitava variante
/// original, foi descartada (ADR-0114):
/// <c>ConfiguracaoDoFada</c> é global ao processo, sem lista de
/// modalidades, tornando a variante incompatível com o agregado real.
/// <b>Correção sobre o xmldoc de <see cref="PredicadoObrigatoriedade"/></b>:
/// o padrão "CS8509 sem catch-all" que o projeto usa para <c>enum</c>
/// não se aplica aqui — o Roslyn não prova exaustividade de switch sobre uma
/// hierarquia de classes/records aberta (mesmo com todo derivado
/// <c>sealed</c>), só sobre o conjunto fechado de valores de um <c>enum</c>.
/// O braço final é um discard que lança <see cref="UnreachableException"/>: uma
/// 8ª variante ainda compilaria, mas falharia alto em runtime em vez de ser
/// silenciosamente ignorada.
/// </remarks>
/// <remarks>
/// Nenhum ramo compara <see cref="ProcessoSeletivo.TipoProcesso"/> nem qualquer
/// rótulo institucional — a flexibilidade entre tipos de processo vem
/// inteiramente de quais regras o cadastro tem vigentes para
/// <paramref name="tipoProcessoCodigoAvaliado"/>, nunca de um <c>if</c>
/// aqui dentro.
/// </remarks>
public static class AvaliadorConformidadeLegal
{
    /// <param name="identidades">
    /// Identidade viva de cada código de cadastro, resolvida pela camada de aplicação.
    /// Chega como dado, não como leitor: este serviço é domínio puro e não consulta
    /// cadastro. Um código ausente do mapa não designa item vivo algum — a conferência de
    /// referências já recusa a regra antes de chegar aqui.
    /// </param>
    /// <param name="fatosDeModalidade">
    /// Os fatos cujos valores são modalidades: a cobertura de um documento por modalidade vale
    /// tanto para o gatilho sobre a modalidade de concorrência quanto para o gatilho sobre o grupo
    /// da convocação.
    /// </param>
    public static ResultadoConformidade Avaliar(
        ProcessoSeletivo processo,
        string tipoProcessoCodigoAvaliado,
        IReadOnlyList<ObrigatoriedadeLegal> regras,
        IdentidadesDeCadastro identidades,
        FatosDeModalidade fatosDeModalidade)
    {
        ArgumentNullException.ThrowIfNull(processo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoProcessoCodigoAvaliado);
        ArgumentNullException.ThrowIfNull(regras);
        ArgumentNullException.ThrowIfNull(identidades);
        ArgumentNullException.ThrowIfNull(fatosDeModalidade);

        List<(RegraAvaliada Avaliada, string? Aviso)> resultados =
            [.. regras.Select(regra => AvaliarRegra(processo, tipoProcessoCodigoAvaliado, identidades, fatosDeModalidade, regra))];

        return new ResultadoConformidade(
            [.. resultados.Select(static r => r.Avaliada)],
            [.. resultados.Select(static r => r.Aviso).OfType<string>()]);
    }

    private static (RegraAvaliada Avaliada, string? Aviso) AvaliarRegra(
        ProcessoSeletivo processo,
        string tipoProcessoCodigoAvaliado,
        IdentidadesDeCadastro identidades,
        FatosDeModalidade fatosDeModalidade,
        ObrigatoriedadeLegal regra)
    {
        (bool aprovada, string? motivo, string? aviso) = AvaliarPredicado(processo, identidades, fatosDeModalidade, regra.Predicado);
        RegraAvaliada avaliada = new(
            regra.Id, regra.RegraCodigo, regra.Categoria, tipoProcessoCodigoAvaliado, regra.Predicado,
            aprovada, motivo, regra.BaseLegal, regra.AtoNormativoUrl, regra.PortariaInternaCodigo,
            regra.DescricaoHumana, regra.VigenciaInicio, regra.VigenciaFim, regra.Hash);

        return (avaliada, aviso is null ? null : $"{regra.RegraCodigo}: {aviso}");
    }

    /// <summary>
    /// Devolve o veredicto, um motivo nomeado quando reprova (a razão
    /// específica, não só um booleano) e, quando aplicável, uma mensagem de aviso informativo
    /// independente da aprovação — nunca lança, mesmo para payload malformado de
    /// <see cref="Customizado"/>.
    /// </summary>
    private static Desfecho AvaliarPredicado(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        FatosDeModalidade fatosDeModalidade,
        PredicadoObrigatoriedade predicado) => predicado switch
        {
            EtapaObrigatoria p => AvaliarEtapaObrigatoria(processo, identidades, p),
            ModalidadesMinimas p => AvaliarModalidadesMinimas(processo, identidades, p),
            DesempateDeveIncluir p => AvaliarDesempateDeveIncluir(processo, p),
            DocumentoObrigatorioParaModalidade p => AvaliarDocumentoObrigatorioParaModalidade(processo, identidades, fatosDeModalidade, p),
            AtendimentoDisponivel p => AvaliarAtendimentoDisponivel(processo, identidades, p),
            ConcorrenciaDuplaObrigatoria => AvaliarConcorrenciaDuplaObrigatoria(processo),
            Customizado => (true, null, "predicado customizado — aprovado por padrão, sem verificação automática"),
            _ => throw new UnreachableException(
                $"Predicado {predicado.GetType().Name} não é uma das 7 variantes reconhecidas por este avaliador."),
        };

    private static Desfecho Aprovada => (true, null, null);

    private static Desfecho Reprovada(string motivo) => (false, motivo, null);

    private static string CodigoReatribuidoDepoisDaRegra => " — o código foi reatribuído depois que a regra foi escrita";

    private static Desfecho AvaliarEtapaObrigatoria(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        EtapaObrigatoria predicado)
    {
        Casamento casamento = CasamentoPorIdentidade.Casar(
            identidades.TiposEtapa, predicado.TipoEtapaCodigo, processo.Etapas, processo.Etapas,
            static e => e.TipoEtapa.OrigemId, static e => e.TipoEtapa.Codigo);

        return casamento switch
        {
            Casamento.Casa => Aprovada,
            Casamento.CodigoReatribuido => Reprovada(
                $"a etapa do tipo '{predicado.TipoEtapaCodigo}' é de outro tipo{CodigoReatribuidoDepoisDaRegra}"),
            _ => Reprovada($"etapa do tipo '{predicado.TipoEtapaCodigo}' ausente"),
        };
    }

    /// <summary>
    /// Avalia POR OFERTA — aprova sse TODA <c>ConfiguracaoDistribuicaoVagas</c>
    /// contém todas as modalidades exigidas. Sem nenhuma oferta cadastrada, não há
    /// o que reprovar (contraprova de indistinguibilidade do processo de
    /// importação externa) — aprova vazio.
    /// </summary>
    private static Desfecho AvaliarModalidadesMinimas(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        ModalidadesMinimas predicado) =>
        processo.DistribuicaoVagas
            .Select(oferta => AvaliarOferta(oferta, identidades, predicado))
            .FirstOrDefault(static desfecho => !desfecho.Aprovada, Aprovada);

    private static Desfecho AvaliarOferta(
        ConfiguracaoDistribuicaoVagas oferta,
        IdentidadesDeCadastro identidades,
        ModalidadesMinimas predicado)
    {
        (List<string> ausentes, List<string> reatribuidos) = CasamentoPorIdentidade.Classificar(
            predicado.Codigos, identidades.Modalidades, oferta.Modalidades,
            static m => m.ModalidadeOrigemId, static m => m.Codigo);
        if (reatribuidos.Count > 0)
        {
            return Reprovada(
                $"na oferta {oferta.Id}, a(s) modalidade(s) {string.Join(", ", reatribuidos)} designa(m) outra modalidade{CodigoReatribuidoDepoisDaRegra}");
        }

        return ausentes.Count > 0
            ? Reprovada($"oferta {oferta.Id} não contém a(s) modalidade(s) {string.Join(", ", ausentes)}")
            : Aprovada;
    }

    /// <summary>
    /// Aprova sse existir uma <see cref="DocumentoExigido"/> do tipo pedido que cubra a modalidade
    /// INCONDICIONALMENTE. Modalidade não ofertada em nenhuma oferta do processo: nada a exigir, aprova
    /// vazio (mesmo espírito de <see cref="AvaliarModalidadesMinimas"/> sem nenhuma oferta cadastrada).
    /// </summary>
    /// <remarks>
    /// "Incondicionalmente" é a parte que faz esta verificação diferente do resolvedor de exigências
    /// documentais (que avalia contra um candidato REAL, com todos os fatos dele resolvidos): aqui não há
    /// candidato — só a modalidade em si. Uma exigência GERAL cobre qualquer modalidade, por definição.
    /// Uma CONDICIONAL só cobre a modalidade avaliada se o predicado DNF casar usando <b>somente</b> os
    /// fatos cujos valores são modalidades, todos iguais a <c>predicado.Modalidade</c> — se a exigência
    /// também depender de outro fato (ex.: <c>FAIXA_ETARIA</c>), <see cref="PredicadoDnf.Avaliar"/> trata
    /// esse fato como ausente e reprova a cláusula (conservador, nunca lança): nem todo candidato da
    /// modalidade seria coberto, e é essa parcialidade que a obrigação legal não admite. O tipo da
    /// exigência é conferido pela IDENTIDADE congelada, e não pelo código: renomear o tipo faria a
    /// exigência legítima deixar de casar, e reciclar o código faria outro documento passar por ele. Sem oferta
    /// da modalidade exigida não há o que exigir, inclusive para o código reatribuído, porque a oferta que o
    /// carrega é outra modalidade. Código de tipo ausente do mapa não designa tipo vivo algum: a conferência de
    /// referências recusa a regra antes de chegar aqui, e a ausência apenas não casa.
    /// </remarks>
    private static Desfecho AvaliarDocumentoObrigatorioParaModalidade(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        FatosDeModalidade fatosDeModalidade,
        DocumentoObrigatorioParaModalidade predicado)
    {
        if (!identidades.Modalidades.TryGetValue(predicado.Modalidade ?? string.Empty, out Guid identidadeDaModalidade)
            || CodigosCongeladosDaModalidade(processo, identidadeDaModalidade) is not { Length: > 0 } codigosCongelados)
        {
            return Aprovada;
        }

        if (!identidades.TiposDocumento.TryGetValue(predicado.TipoDocumento ?? string.Empty, out Guid identidadeExigida))
        {
            return SemCoberturaIncondicional(predicado);
        }

        return CobreIncondicionalmente(processo, fatosDeModalidade, codigosCongelados, identidadeExigida)
            ? Aprovada
            : DiagnosticoDaFaltaDeCobertura(processo, predicado, identidadeExigida);
    }

    /// <summary>
    /// Os códigos congelados na distribuição de vagas, não o da regra: uma exigência CONDICIONAL guarda a
    /// condição com o código que valia quando foi configurada, e alimentar o fato com o código novo da
    /// regra reprovaria o processo conforme cuja renomeação esta avaliação acomoda. Ofertas congeladas em
    /// momentos diferentes podem carregar códigos diferentes para a mesma identidade; basta uma cobrir.
    /// </summary>
    private static string[] CodigosCongeladosDaModalidade(ProcessoSeletivo processo, Guid identidadeDaModalidade) =>
        [.. processo.DistribuicaoVagas
            .SelectMany(static d => d.Modalidades)
            .Where(m => m.ModalidadeOrigemId == identidadeDaModalidade)
            .Select(static m => m.Codigo)
            .Distinct(StringComparer.Ordinal)];

    /// <summary>
    /// Só a exigência que casa por tipo, determina o resultado (é obrigatória ou tem consequência de
    /// indeferimento) e é certamente aplicável (<see cref="Ternario.Verdadeiro"/>) prova a cobertura: a
    /// meramente opcional não satisfaz a obrigação legal, e o indeterminado conta como não provado.
    /// </summary>
    private static bool CobreIncondicionalmente(
        ProcessoSeletivo processo,
        FatosDeModalidade fatosDeModalidade,
        string[] codigosCongelados,
        Guid identidadeExigida) =>
        codigosCongelados.Any(codigo =>
        {
            Dictionary<string, FatoResolvido> fatos = FatosDaModalidade(fatosDeModalidade, codigo);

            return processo.DocumentosExigidos.Any(e =>
                e.TipoDocumentoOrigemId == identidadeExigida
                && e.DeterminaResultado()
                && e.AplicavelPara(fatos) == Ternario.Verdadeiro);
        });

    /// <summary>
    /// Só os fatos cujos valores são modalidades entram, todos com a modalidade avaliada: quem foi
    /// convocado nela também concorre nela. Na publicação não há candidato, e todo outro fato é
    /// legitimamente desconhecido: a ausência resolve INDETERMINADO, que aqui significa cobertura não
    /// provada. Materializá-los como NAO_APLICAVEL colapsaria a cláusula em FALSO e afirmaria algo que
    /// não se sabe.
    /// </summary>
    private static Dictionary<string, FatoResolvido> FatosDaModalidade(FatosDeModalidade fatosDeModalidade, string codigo)
    {
        FatoResolvido modalidade = FatoResolvido.Resolvido(JsonSerializer.SerializeToElement(codigo));

        return fatosDeModalidade.Codigos.ToDictionary(static fato => fato, _ => modalidade, StringComparer.Ordinal);
    }

    /// <summary>
    /// Distingue "não há exigência" de "há, mas para outro documento": a segunda só acontece quando o
    /// código foi reatribuído depois que a regra foi escrita. A exigência sem identidade não prova
    /// reatribuição nenhuma, e o diagnóstico errado seria pior que o genérico.
    /// </summary>
    private static Desfecho DiagnosticoDaFaltaDeCobertura(
        ProcessoSeletivo processo, DocumentoObrigatorioParaModalidade predicado, Guid identidadeExigida)
    {
        bool reatribuido = CasamentoPorIdentidade.CarregaOCodigoDeOutroItem(
            processo.DocumentosExigidos, predicado.TipoDocumento, identidadeExigida,
            static e => e.TipoDocumentoOrigemId, static e => e.TipoDocumentoCodigo);

        return reatribuido
            ? Reprovada($"a exigência documental do tipo '{predicado.TipoDocumento}' designa outro documento{CodigoReatribuidoDepoisDaRegra}")
            : SemCoberturaIncondicional(predicado);
    }

    private static Desfecho SemCoberturaIncondicional(DocumentoObrigatorioParaModalidade predicado) =>
        Reprovada($"nenhuma exigência documental do tipo '{predicado.TipoDocumento}' cobre incondicionalmente a modalidade '{predicado.Modalidade}'");

    private static Desfecho AvaliarDesempateDeveIncluir(ProcessoSeletivo processo, DesempateDeveIncluir predicado)
    {
        bool aprovada = processo.CriteriosDesempate.Any(
            c => string.Equals(c.Regra.Codigo, predicado.Criterio, StringComparison.Ordinal));
        return (aprovada, aprovada ? null : $"critério de desempate '{predicado.Criterio}' ausente", null);
    }

    private static Desfecho AvaliarAtendimentoDisponivel(
        ProcessoSeletivo processo,
        IdentidadesDeCadastro identidades,
        AtendimentoDisponivel predicado) =>
        processo.AtendimentoEspecializado is { } atendimento
            ? AvaliarNecessidades(atendimento.TiposDeficiencia, identidades, predicado)
            : Reprovada("nenhum atendimento especializado cadastrado");

    private static Desfecho AvaliarNecessidades(
        IReadOnlyCollection<DeficienciaAtendida> ofertados,
        IdentidadesDeCadastro identidades,
        AtendimentoDisponivel predicado)
    {
        (List<string> ausentes, List<string> reatribuidos) = CasamentoPorIdentidade.Classificar(
            predicado.Necessidades, identidades.TiposDeficiencia, ofertados,
            static t => t.TipoDeficienciaOrigemId, static t => t.TipoDeficienciaCodigo);
        if (reatribuidos.Count > 0)
        {
            return Reprovada(
                $"o atendimento ofertado para {string.Join(", ", reatribuidos)} é de outro tipo de deficiência{CodigoReatribuidoDepoisDaRegra}");
        }

        return ausentes.Count == 0
            ? Aprovada
            : Reprovada($"necessidade(s) de atendimento não ofertada(s): {string.Join(", ", ausentes)}");
    }

    private static Desfecho AvaliarConcorrenciaDuplaObrigatoria(ProcessoSeletivo processo)
    {
        bool aprovada = RegrasDaDistribuicaoDeVagas.ConcorrenciaDuplaAplicavel(processo.Declaracao());
        return (aprovada, aprovada ? null : "nenhuma modalidade de cota reservada (CotaReservada) cadastrada", null);
    }
}
