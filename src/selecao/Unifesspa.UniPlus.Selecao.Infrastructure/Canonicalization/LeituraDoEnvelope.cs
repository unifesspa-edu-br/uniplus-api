namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Lê, na ordem do envelope, cada bloco pela classe de leitura do seu assunto e entrega os blocos
/// ainda sem conferir a coerência entre eles. O primeiro desvio fica no <see cref="LeitorEnvelope"/>.
/// </summary>
internal static class LeituraDoEnvelope
{
    /// <summary>
    /// Lê todos os blocos do envelope.
    /// </summary>
    /// <param name="leitor">O leitor que acumula o primeiro erro.</param>
    /// <param name="payload">O objeto canônico já parseado.</param>
    /// <param name="temRetificacao">Se o envelope carrega o bloco de retificação.</param>
    /// <returns>Os blocos lidos, ou nulo quando a leitura falhou.</returns>
    internal static BlocosDoEnvelope? Ler(LeitorEnvelope leitor, JsonObject payload, bool temRetificacao)
    {
        BlocosDaConfiguracao configuracao = LerConfiguracao(leitor, payload);
        BlocosDosDocumentos documentos = LerDocumentos(leitor, payload);
        BlocosDaPublicacao publicacao = LerPublicacao(leitor, payload, configuracao);
        RetificacaoDoEdital? retificacao = temRetificacao ? LeituraDaRetificacao.LerRetificacao(leitor, payload) : null;
        if (leitor.Falhou)
        {
            return null;
        }

        Dictionary<Guid, DocumentoExigido> exigenciasPorId = IndexarExigenciasPorId(leitor, documentos.DocumentosExigidos);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<NoExigencia> raizes = LeituraDaArvoreDeSatisfacao.LerArvoreSatisfacao(leitor, payload, exigenciasPorId);
        if (leitor.Falhou)
        {
            return null;
        }

        BlocosDeColeta coleta = LerColeta(leitor, payload, configuracao);
        return leitor.Falhou ? null : new BlocosDoEnvelope(configuracao, documentos, publicacao, retificacao, raizes, coleta);
    }

    private static BlocosDaConfiguracao LerConfiguracao(LeitorEnvelope leitor, JsonObject payload)
    {
        LeituraDoEdital.LerTipoProcesso(leitor, payload);

        DadosEdital? dados = LeituraDoEdital.LerDadosEdital(leitor, payload, out string hashDocumento);
        IReadOnlyList<EtapaProcesso> etapas = LeituraDasEtapas.LerEtapas(leitor, payload);
        IReadOnlyList<ConfiguracaoDistribuicaoVagas> distribuicao = LeituraDasVagas.LerDistribuicao(leitor, payload);
        OfertaAtendimentoEspecializado? atendimento = LeituraDoBonusEAtendimento.LerAtendimento(leitor, payload);
        ConfiguracaoBonusRegional? bonus = LeituraDoBonusEAtendimento.LerBonusRegional(leitor, payload);
        ConfiguracaoCascataRemanejamento? cascata = LeituraDaCascata.LerCascataRemanejamento(leitor, payload);
        IReadOnlyList<CriterioDesempate> desempate = LeituraDoDesempate.LerCriteriosDesempate(leitor, payload);
        ConfiguracaoClassificacao? classificacao = LeituraDaClassificacao.LerClassificacao(leitor, payload);
        IReadOnlyList<FaseCronograma> cronogramaFases = LeituraDoCronograma.LerCronogramaFases(leitor, payload, comId: true);
        LeituraDoEdital.LerIdentidadesUnidade(leitor, payload);

        return new BlocosDaConfiguracao
        {
            Dados = dados,
            HashDocumento = hashDocumento,
            Etapas = etapas,
            Distribuicao = distribuicao,
            Atendimento = atendimento,
            Bonus = bonus,
            Cascata = cascata,
            Desempate = desempate,
            Classificacao = classificacao,
            CronogramaFases = cronogramaFases,
        };
    }

    private static BlocosDosDocumentos LerDocumentos(LeitorEnvelope leitor, JsonObject payload)
    {
        (ResultadoConformidade? conformidade, IReadOnlyList<DocumentoExigido> documentos, ReferenciaTemporalFatos? referenciaTemporal,
            IReadOnlyDictionary<string, MetadadoFatoCongelado>? metadados) = LeituraDosDocumentosExigidos.LerDocumentosExigidos(leitor, payload);
        (IReadOnlyList<FormularioProcesso> formularios, IReadOnlyList<TermoExigidoFormulario> termos) = LeituraDosFormularios.LerFormularios(leitor, payload);

        return new BlocosDosDocumentos
        {
            Conformidade = conformidade,
            DocumentosExigidos = documentos,
            ReferenciaTemporalFatos = referenciaTemporal,
            MetadadosFatosCongelados = metadados,
            Formularios = formularios,
            TermosExigidos = termos,
        };
    }

    private static BlocosDaPublicacao LerPublicacao(LeitorEnvelope leitor, JsonObject payload, BlocosDaConfiguracao configuracao)
    {
        ConfiguracaoDivulgacao? divulgacao = LeituraDaDivulgacaoETaxa.LerDivulgacao(leitor, payload);
        ConfiguracaoTaxaInscricao? taxa = LeituraDaDivulgacaoETaxa.LerTaxaInscricao(leitor, payload);
        (LocalidadeRegente? localidade, string? fusoHorario) = LeituraDoCalendario.LerLocalidade(leitor, payload);
        int? opcoesDeCurso = LeituraDasVagas.LerOpcoesDeCurso(leitor, payload);
        ReferenciaRegra? algoritmo = LeituraDoCalendario.LerAlgoritmoContagemPrazo(leitor, payload);
        CalendarioDiasUteisCongelado? calendario = LeituraDoCalendario.LerCalendarioDiasUteis(
            leitor, payload, configuracao.CronogramaFases, configuracao.Etapas);
        IdentificadorLegivel? identificadorLegivel = LeituraDoEdital.LerIdentificadorLegivel(leitor, payload);

        return new BlocosDaPublicacao
        {
            Divulgacao = divulgacao,
            TaxaInscricao = taxa,
            Localidade = localidade,
            FusoHorario = fusoHorario,
            NumeroDeOpcoesDeCurso = opcoesDeCurso,
            AlgoritmoContagemPrazo = algoritmo,
            CalendarioDiasUteis = calendario,
            IdentificadorLegivel = identificadorLegivel,
        };
    }

    private static BlocosDeColeta LerColeta(LeitorEnvelope leitor, JsonObject payload, BlocosDaConfiguracao configuracao)
    {
        (IReadOnlyList<FatoColetado> fatos, Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores) =
            LeituraDosFatos.LerFatosColetados(leitor, payload);
        List<DefinicaoAgregado> agregados = [];
        IReadOnlyList<GrupoColetado> grupos = leitor.Falhou
            ? []
            : LeituraDosGrupos.LerGruposColetados(leitor, payload, valores, agregados);
        IReadOnlyList<ConfiguracaoDerivacaoFato> regras = LeituraDaDerivacao.LerRegrasDerivacao(leitor, payload);
        string versaoInterpretador = leitor.TextoNaoVazio(payload, "versaoInterpretador", "$");
        Dictionary<string, ApresentacaoDoFato> apresentacao = LeituraDoEdital.LerApresentacaoDosPressupostos(leitor, payload);
        Dictionary<string, TipoDeAtoCongelado> tiposDeAto = LeituraDosTiposDeAto.LerTiposDeAto(
            leitor, payload, configuracao.CronogramaFases, configuracao.Etapas);
        Dictionary<string, FatoDerivadoCongelado> fatosDerivados = LeituraDosFatosDerivados.LerFatosDerivados(leitor, payload);

        return new BlocosDeColeta
        {
            FatosColetados = fatos,
            ValoresSelecionaveis = valores,
            GruposColetados = grupos,
            AgregadosDosGrupos = agregados,
            RegrasDerivacao = regras,
            VersaoInterpretador = versaoInterpretador,
            ApresentacaoDosPressupostos = apresentacao,
            TiposDeAtoCongelados = tiposDeAto,
            FatosDerivadosCongelados = fatosDerivados,
        };
    }

    /// <summary>
    /// O bloco de exigências nunca repete <c>exigenciaId</c> quando produzido por um encoder real, mas
    /// um envelope adulterado poderia. Sem esta checagem, o dicionário lançaria exceção (500) em vez
    /// de recusar o envelope como malformado.
    /// </summary>
    private static Dictionary<Guid, DocumentoExigido> IndexarExigenciasPorId(
        LeitorEnvelope leitor, IReadOnlyList<DocumentoExigido> documentosExigidos)
    {
        List<Guid> duplicados = [.. documentosExigidos
            .GroupBy(static d => d.Id)
            .Where(static grupo => grupo.Count() > 1)
            .Select(static grupo => grupo.Key)];

        return duplicados.Count > 0
            ? leitor.Recusar<Dictionary<Guid, DocumentoExigido>>(
                $"'documentosExigidos.exigencias': o exigenciaId '{duplicados[0]}' aparece mais de uma vez.") ?? []
            : documentosExigidos.ToDictionary(static d => d.Id);
    }
}
