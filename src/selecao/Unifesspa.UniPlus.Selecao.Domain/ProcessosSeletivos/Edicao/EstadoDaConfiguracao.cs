namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;

using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Vista somente leitura da configuração de um <see cref="ProcessoSeletivo"/>: é o que as regras
/// de configuração recebem no lugar da raiz. As coleções são as vivas do agregado, expostas sem
/// possibilidade de escrita, de modo que quem precisa do estado depois de uma mutação deve
/// materializá-lo antes. O candidato de uma escrita é <c>De(processo) with { Etapas = candidatas }</c>.
/// </summary>
internal sealed record EstadoDaConfiguracao
{
    public required Guid ProcessoId { get; init; }

    public required StatusProcesso Status { get; init; }

    public required OrigemCandidatos OrigemCandidatos { get; init; }

    public required IdentificadorLegivel? IdentificadorLegivel { get; init; }

    public required TipoProcessoSnapshot TipoProcesso { get; init; }

    public required UnidadeAdministradoraSnapshot UnidadeAdministradora { get; init; }

    public required LocalidadeRegente Localidade { get; init; }

    public required ReferenciaRegra? AlgoritmoContagemPrazo { get; init; }

    public required ReferenciaTemporalFatos? ReferenciaTemporalFatos { get; init; }

    public required bool? AplicaBonusRegional { get; init; }

    public required int? NumeroDeOpcoesDeCurso { get; init; }

    public required IReadOnlyCollection<EtapaProcesso> Etapas { get; init; }

    public required IReadOnlyCollection<FaseCronograma> CronogramaFases { get; init; }

    public required IReadOnlyCollection<DocumentoExigido> DocumentosExigidos { get; init; }

    public required IReadOnlyCollection<NoExigencia> NosExigencia { get; init; }

    public required IReadOnlyCollection<FatoColetado> Campos { get; init; }

    public required IReadOnlyCollection<GrupoColetado> GruposColetados { get; init; }

    public required IReadOnlyCollection<FormularioProcesso> Formularios { get; init; }

    public required IReadOnlyCollection<TermoExigidoFormulario> TermosExigidos { get; init; }

    public required IReadOnlyCollection<OpcaoDeclaradaFato> OpcoesDeclaradas { get; init; }

    public required IReadOnlyCollection<ConfiguracaoDerivacaoFato> RegrasDerivacao { get; init; }

    public required IReadOnlyCollection<ConfiguracaoDistribuicaoVagas> DistribuicaoVagas { get; init; }

    public required IReadOnlyCollection<CriterioDesempate> CriteriosDesempate { get; init; }

    public required OfertaAtendimentoEspecializado? OfertaAtendimento { get; init; }

    public required ConfiguracaoBonusRegional? BonusRegional { get; init; }

    public required ConfiguracaoCascataRemanejamento? Cascata { get; init; }

    public required ConfiguracaoClassificacao? Classificacao { get; init; }

    public required ConfiguracaoDivulgacao? ConfiguracaoDivulgacao { get; init; }

    public required ConfiguracaoTaxaInscricao? ConfiguracaoTaxaInscricao { get; init; }

    public required RascunhoRetificacao? Rascunho { get; init; }

    public static EstadoDaConfiguracao De(ProcessoSeletivo processo)
    {
        ArgumentNullException.ThrowIfNull(processo);

        return new EstadoDaConfiguracao
        {
            ProcessoId = processo.Id,
            Status = processo.Status,
            OrigemCandidatos = processo.OrigemCandidatos,
            IdentificadorLegivel = processo.IdentificadorLegivel,
            TipoProcesso = processo.TipoProcesso,
            UnidadeAdministradora = processo.UnidadeAdministradora,
            Localidade = processo.Localidade,
            AlgoritmoContagemPrazo = processo.AlgoritmoContagemPrazo,
            ReferenciaTemporalFatos = processo.ReferenciaTemporalFatos,
            AplicaBonusRegional = processo.AplicaBonusRegional,
            NumeroDeOpcoesDeCurso = processo.NumeroDeOpcoesDeCurso,
            Etapas = processo.Etapas,
            CronogramaFases = processo.CronogramaFases,
            DocumentosExigidos = processo.DocumentosExigidos,
            NosExigencia = processo.NosExigencia,
            Campos = processo.Campos,
            GruposColetados = processo.GruposColetados,
            Formularios = processo.Formularios,
            TermosExigidos = processo.TermosExigidos,
            OpcoesDeclaradas = processo.OpcoesDeclaradas,
            RegrasDerivacao = processo.RegrasDerivacao,
            DistribuicaoVagas = processo.DistribuicaoVagas,
            CriteriosDesempate = processo.CriteriosDesempate,
            OfertaAtendimento = processo.OfertaAtendimento,
            BonusRegional = processo.BonusRegional,
            Cascata = processo.Cascata,
            Classificacao = processo.Classificacao,
            ConfiguracaoDivulgacao = processo.ConfiguracaoDivulgacao,
            ConfiguracaoTaxaInscricao = processo.ConfiguracaoTaxaInscricao,
            Rascunho = processo.Rascunho,
        };
    }
}
