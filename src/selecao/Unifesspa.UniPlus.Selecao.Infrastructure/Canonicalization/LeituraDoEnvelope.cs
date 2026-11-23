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

        return LerColetaEComprovacao(leitor, payload, configuracao, documentos, publicacao, retificacao);
    }

    private static BlocosDoEnvelope? LerColetaEComprovacao(
        LeitorEnvelope leitor, JsonObject payload, BlocosDaConfiguracao configuracao, BlocosDosDocumentos documentos,
        BlocosDaPublicacao publicacao, RetificacaoDoEdital? retificacao)
    {
        Dictionary<Guid, DocumentoExigido> exigenciasPorId = IndexarExigenciasPorId(leitor, documentos.DocumentosExigidos);
        if (leitor.Falhou)
        {
            return null;
        }

        IReadOnlyList<RegraDeComprovacao> regrasSemGrupo = LeituraDasRegrasDeComprovacao.LerRegrasDeComprovacao(leitor, payload, exigenciasPorId);
        if (leitor.Falhou)
        {
            return null;
        }

        BlocosDeColeta coleta = LerColeta(leitor, payload, configuracao);
        return leitor.Falhou ? null : new BlocosDoEnvelope(configuracao, documentos, publicacao, retificacao, regrasSemGrupo, coleta);
    }

    private static BlocosDaConfiguracao LerConfiguracao(LeitorEnvelope leitor, JsonObject payload)
    {
        LeituraDoEdital.LerNome(leitor, payload);
        LeituraDoEdital.LerTipoProcesso(leitor, payload);

        DadosEdital? dados = LeituraDoEdital.LerDadosEdital(leitor, payload, out string hashDocumento);
        BlocosDaConfiguracao configuracao = LerConfiguracaoDoProcesso(leitor, payload, dados, hashDocumento);
        ChavesNoEnvelope.ExigirUnicas(leitor,
            configuracao.Etapas.SelectMany(static etapa => etapa.Bancas.Select(static banca => banca.Chave))
                .Concat(configuracao.CronogramaFases.SelectMany(static fase => fase.BancasRequeridas.Select(static banca => banca.Chave))),
            "$.bancas");
        LeituraDoEdital.LerIdentidadesUnidade(leitor, payload);
        return configuracao;
    }

    private static BlocosDaConfiguracao LerConfiguracaoDoProcesso(
        LeitorEnvelope leitor, JsonObject payload, DadosEdital? dados, string hashDocumento) => new()
        {
            Dados = dados,
            HashDocumento = hashDocumento,
            MotivosDeDecisao = LeituraDosMotivosDeDecisao.Ler(leitor, payload),
            Etapas = LeituraDasEtapas.LerEtapas(leitor, payload),
            Distribuicao = LeituraDasVagas.LerDistribuicao(leitor, payload),
            Atendimento = LeituraDoBonusEAtendimento.LerAtendimento(leitor, payload),
            Bonus = LeituraDoBonusEAtendimento.LerBonusRegional(leitor, payload),
            Cascata = LeituraDaCascata.LerCascataRemanejamento(leitor, payload),
            Desempate = LeituraDoDesempate.LerCriteriosDesempate(leitor, payload),
            Classificacao = LeituraDaClassificacao.LerClassificacao(leitor, payload),
            CronogramaFases = LeituraDoCronograma.LerCronogramaFases(leitor, payload),
        };

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
        return new BlocosDaPublicacao
        {
            Divulgacao = divulgacao,
            TaxaInscricao = taxa,
            Localidade = localidade,
            FusoHorario = fusoHorario,
            NumeroDeOpcoesDeCurso = LeituraDasOpcoesDeCurso.Ler(leitor, payload),
            AlgoritmoContagemPrazo = LeituraDoCalendario.LerAlgoritmoContagemPrazo(leitor, payload),
            CalendarioDiasUteis = LeituraDoCalendario.LerCalendarioDiasUteis(leitor, payload, configuracao.CronogramaFases, configuracao.Etapas),
            IdentificadorLegivel = LeituraDoEdital.LerIdentificadorLegivel(leitor, payload),
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
        return LerDerivacaoEApresentacao(leitor, payload, configuracao, fatos, valores, grupos, agregados);
    }

    private static BlocosDeColeta LerDerivacaoEApresentacao(
        LeitorEnvelope leitor, JsonObject payload, BlocosDaConfiguracao configuracao, IReadOnlyList<FatoColetado> fatos,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valores, IReadOnlyList<GrupoColetado> grupos,
        List<DefinicaoAgregado> agregados) => new()
        {
            FatosColetados = fatos,
            ValoresSelecionaveis = valores,
            GruposColetados = grupos,
            AgregadosDosGrupos = agregados,
            RegrasDerivacao = LeituraDaDerivacao.LerRegrasDerivacao(leitor, payload),
            VersaoInterpretador = leitor.TextoNaoVazio(payload, "versaoDasRegrasDeCalculo", "$"),
            ApresentacaoDosPressupostos = LeituraDoEdital.LerApresentacaoDosPressupostos(leitor, payload),
            TiposDeAtoCongelados = LeituraDosTiposDeAto.LerTiposDeAto(leitor, payload, configuracao.CronogramaFases, configuracao.Etapas),
            FatosDerivadosCongelados = LeituraDosFatosDerivados.LerFatosDerivados(leitor, payload),
        };

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
