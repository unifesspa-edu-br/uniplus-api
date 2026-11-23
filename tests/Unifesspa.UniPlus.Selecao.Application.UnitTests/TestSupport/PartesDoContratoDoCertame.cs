namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;

/// <summary>
/// As partes públicas do certame na ordem em que a projeção as lê: o bloco do envelope congelado e o nome
/// pelo qual a recusa cita a parte.
/// </summary>
internal static class PartesDoContratoDoCertame
{
    /// <summary>Os blocos cuja chave no envelope é outra que o nome público da parte, e não só outra grafia.</summary>
    internal static readonly string[] BlocosComChaveInternaDiferente =
        ["hashesEdital", "identidadesUnidade", "cronogramaFases", "documentosExigidos", "vagas"];

    internal static IReadOnlyList<(string Bloco, string Parte)> NaOrdemDaLeitura { get; } =
    [
        ("nome", "nome do processo"),
        ("identificadorLegivel", "identificador legível"),
        ("tipoProcesso", "tipo do processo"),
        ("periodo", "período de inscrição"),
        ("localidade", "localidade"),
        ("identidadesUnidade", "unidade administradora"),
        ("hashesEdital", "documento do edital"),
        ("ofertas", "ofertas"),
        ("opcoesDeCurso", "opções de curso"),
        ("modalidadesOfertadas", "modalidades ofertadas"),
        ("vagas", "quadro de vagas"),
        ("etapas", "etapas"),
        ("cronogramaFases", "cronograma"),
        ("documentosExigidos", "documentos exigidos"),
        ("atendimento", "atendimento especializado"),
        ("taxaInscricao", "taxa de inscrição"),
        ("retificacao", "retificação"),
    ];
}
