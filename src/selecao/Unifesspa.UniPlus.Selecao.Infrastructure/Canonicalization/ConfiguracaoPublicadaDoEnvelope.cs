namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>Monta os blocos da configuração a partir dos valores conferidos do envelope.</summary>
internal static class ConfiguracaoPublicadaDoEnvelope
{
    internal static ConfiguracaoPublicada Montar(BlocosDoEnvelope blocos, IReadOnlyList<RegraDeComprovacao> regras,
        IReadOnlyList<FatoColetado> campos, IReadOnlyList<OpcaoDeclaradaFato> opcoes) => new(
        Cronograma(blocos), Avaliacao(blocos), Vagas(blocos), Exigencias(blocos, regras), Coleta(blocos, campos, opcoes),
        DivulgacaoETaxa(blocos), blocos.Publicacao.IdentificadorLegivel)
        {
            MotivosDeDecisao = blocos.Configuracao.MotivosDeDecisao,
            ProvaPresencial = blocos.Configuracao.ProvaPresencial,
            CadastroDeReserva = blocos.Configuracao.CadastroDeReserva,
        };

    private static CronogramaDeclarado Cronograma(BlocosDoEnvelope blocos) => new(
        blocos.Publicacao.Localidade!, blocos.Publicacao.AlgoritmoContagemPrazo, blocos.Configuracao.CronogramaFases);

    private static AvaliacaoDeclarada Avaliacao(BlocosDoEnvelope blocos) => new(
        blocos.Configuracao.Etapas, blocos.Configuracao.Desempate, blocos.Configuracao.Classificacao);

    private static VagasDeclaradas Vagas(BlocosDoEnvelope blocos) => new(
        blocos.Configuracao.Distribuicao, blocos.Publicacao.NumeroDeOpcoesDeCurso,
        // A presença do bônus é o que o envelope conserva; a declaração negativa não tem bloco próprio.
        blocos.Configuracao.Bonus is not null, blocos.Configuracao.Bonus, blocos.Configuracao.Cascata, blocos.Configuracao.Atendimento);

    private static ExigenciasDeclaradas Exigencias(BlocosDoEnvelope blocos, IReadOnlyList<RegraDeComprovacao> regras) => new(
        blocos.Documentos.DocumentosExigidos, regras);

    private static ColetaDeclarada Coleta(BlocosDoEnvelope blocos, IReadOnlyList<FatoColetado> campos, IReadOnlyList<OpcaoDeclaradaFato> opcoes) => new(
        campos, blocos.Coleta.GruposColetados, blocos.Documentos.Formularios, blocos.Documentos.TermosExigidos,
        opcoes, blocos.Coleta.RegrasDerivacao, blocos.Documentos.ReferenciaTemporalFatos);

    private static DivulgacaoETaxaDeclaradas DivulgacaoETaxa(BlocosDoEnvelope blocos) => new(
        blocos.Publicacao.Divulgacao, blocos.Publicacao.TaxaInscricao);
}
