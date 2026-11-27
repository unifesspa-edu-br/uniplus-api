namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

/// <summary>
/// O que as vagas, o atendimento, o FADA e as exigências citam: as ofertas de curso, as modalidades, as referências demográficas, as
/// condições, os recursos e os tipos de deficiência do atendimento, os tipos de documento, a base legal do FADA e as regras do
/// catálogo. Cada lista sai sem repetição, na ordem em que aparece.
/// </summary>
internal sealed record CodigosDasVagasEDasExigenciasCitados(
    IReadOnlyList<string> OfertasDeCurso,
    IReadOnlyList<string> Modalidades,
    IReadOnlyList<string> ReferenciasDemograficas,
    IReadOnlyList<(string Codigo, string Versao)> Regras)
{
    public IReadOnlyList<string> TiposDeDocumento { get; init; } = [];

    public string? BaseLegalDoFada { get; init; }

    public IReadOnlyList<string> CondicoesDeAtendimento { get; init; } = [];

    public IReadOnlyList<string> RecursosDeAcessibilidade { get; init; } = [];

    public IReadOnlyList<string> TiposDeDeficiencia { get; init; } = [];
}

/// <summary>
/// Junta, nas vagas e nas exigências, os códigos que o documento cita em cada cadastro: a oferta de curso, as modalidades da
/// distribuição, do ajuste e da cascata, a referência demográfica, o atendimento, a base legal do FADA, o tipo de documento da
/// exigência e as regras de distribuição, de ajuste, da cascata e do FADA. Não confere o que cada código é.
/// </summary>
internal static class CodigosCitadosNasVagasENasExigencias
{
    internal static CodigosDasVagasEDasExigenciasCitados Em(DeclaracoesDoDocumento declaracoes)
    {
        ArgumentNullException.ThrowIfNull(declaracoes);

        ExigenciaNoDocumento[] exigencias = [.. declaracoes.Exigencias?.Exigencias ?? []];

        return DasVagas(declaracoes.Vagas ?? new(null, [])) with
        {
            TiposDeDocumento = CodigosDistintos.De(exigencias.Select(static exigencia => exigencia.TipoDocumentoCodigo)),
        };
    }

    private static CodigosDasVagasEDasExigenciasCitados DasVagas(VagasNoDocumento vagas)
    {
        DistribuicaoNoDocumento[] distribuicoes = [.. vagas.Distribuicoes];
        AtendimentoNoDocumento atendimento = vagas.Atendimento ?? new([], [], []);

        return new(
            CodigosDistintos.De(distribuicoes.Select(static distribuicao => distribuicao.OfertaCursoCodigo)),
            CodigosDistintos.De([.. ModalidadesDasDistribuicoes(distribuicoes), .. ModalidadesDaCascata(vagas.Cascata)]),
            CodigosDistintos.De(distribuicoes.Select(static distribuicao => distribuicao.ReferenciaDemograficaCodigo)),
            CodigosDistintos.DasRegras(Regras(vagas, distribuicoes)))
        {
            BaseLegalDoFada = vagas.Fada?.BaseLegalCodigo,
            CondicoesDeAtendimento = Codigos(atendimento.Condicoes),
            RecursosDeAcessibilidade = Codigos(atendimento.Recursos),
            TiposDeDeficiencia = Codigos(atendimento.TiposDeficiencia),
        };
    }

    private static string[] Codigos(IReadOnlyList<CodigoNoDocumento> itens) =>
        CodigosDistintos.De(itens.Select(static item => item.Codigo));

    private static IEnumerable<string?> ModalidadesDasDistribuicoes(DistribuicaoNoDocumento[] distribuicoes) =>
    [
        .. distribuicoes.SelectMany(static distribuicao => distribuicao.Modalidades).Select(static modalidade => modalidade.Codigo),
        .. distribuicoes.Select(static distribuicao => distribuicao.AjusteDoQuadro).OfType<AjusteDoQuadroNoDocumento>()
            .SelectMany(static ajuste => (string?[])[ajuste.ModalidadeCodigo, .. ajuste.ModalidadeCodigos]),
    ];

    private static IEnumerable<string?> ModalidadesDaCascata(CascataNoDocumento? cascata) => cascata is null
        ? []
        :
        [
            cascata.DestinoFinalCodigo,
            .. cascata.Destinos.SelectMany(static destino => (string?[])[destino.ModalidadeOrigemCodigo, destino.ModalidadeDestinoCodigo]),
        ];

    private static IEnumerable<ReferenciaDeRegraNoDocumento?> Regras(VagasNoDocumento? vagas, DistribuicaoNoDocumento[] distribuicoes) =>
    [
        .. distribuicoes.Select(static distribuicao => distribuicao.RegraDeDistribuicao),
        .. distribuicoes.Select(static distribuicao => distribuicao.RegraDeAjuste),
        vagas?.Cascata?.Regra,
        vagas?.Fada?.Regra,
    ];
}
