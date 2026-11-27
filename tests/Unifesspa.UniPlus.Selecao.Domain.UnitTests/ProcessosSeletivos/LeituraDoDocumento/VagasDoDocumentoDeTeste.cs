namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using System.Text.Json;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// As vagas do documento e os cadastros citados que os testes da leitura das vagas montam: o que não declaram tem o valor de uma
/// vaga completa e conforme, de modo que cada teste muda só o que quer provar.
/// </summary>
internal static class VagasDoDocumentoDeTeste
{
    internal const string OfertaViva = "PEDAGOGIA_MARABA";
    internal const string OfertaDeMedicina = "MEDICINA_MARABA";
    internal const string Ampla = "AC";
    internal const string AmplaComDeficiencia = "AC_PCD";
    internal const string CotaA = "COTA_A";
    internal const string CotaB = "COTA_B";
    internal const string TextoDigitado = "TEXTO-DIGITADO-NO-DOCUMENTO";
    internal const string CondicaoPcd = AtendimentoEspecializado.CodigoCondicaoPcd;
    internal const string CondicaoDeLactante = "LACTANTE";
    internal const string RecursoDeSala = "SALA_TERREA";
    internal const string TipoVisual = "VISUAL";
    internal const string BaseLegalDoFada = "PORTARIA_DE_TESTE";

    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static ReferenciaDeRegraNoDocumento Institucional { get; } = new(RegraDeDistribuicaoDeVagas.Institucional.Codigo(), "v1");

    internal static ReferenciaDeRegraNoDocumento Remanejamento { get; } = new(CriterioDeRemanejamento.OrdemDaLei12711.Codigo(), "v1");

    internal static ReferenciaDeRegraNoDocumento Fada { get; } = new(RegraDoFada.Multiplicativo.Codigo(), "v1");

    internal static ReferenciaDeRegraNoDocumento Ajuste { get; } = new(RegraDeAjusteDaDistribuicaoDeVagas.AjusteDoTotalPeloArtigo11.Codigo(), "v1");

    internal static CadastrosCitadosNoDocumento Cadastros() => new(DocumentoLidoDeTeste.LidoEm)
    {
        Regras = Regras(),
        VagasEExigencias = new()
        {
            OfertasDeCurso = Ofertas(),
            Modalidades = Modalidades(),
            CondicoesDeAtendimento = Opcoes(CondicaoPcd, CondicaoDeLactante),
            RecursosDeAcessibilidade = Opcoes(RecursoDeSala),
            TiposDeDeficiencia = Opcoes(TipoVisual),
            BasesLegaisDoFada = BasesLegais(),
        },
    };

    private static Dictionary<string, OfertaDeCursoCitada> Ofertas() => new()
    {
        [OfertaViva] = Oferta(OfertaViva, "Pedagogia"),
        [OfertaDeMedicina] = Oferta(OfertaDeMedicina, "Medicina"),
    };

    private static Dictionary<string, ModalidadeNoCadastro> Modalidades() => new()
    {
        [Ampla] = ModalidadesNoCadastroDeTeste.Da(Ampla, NaturezaLegalModalidade.Ampla, ComposicaoVagasModalidade.RestanteDasVagasOfertadas),
        [AmplaComDeficiencia] = RetiradaDaAmpla(AmplaComDeficiencia),
        [CotaA] = RetiradaDaAmpla(CotaA),
        [CotaB] = RetiradaDaAmpla(CotaB),
    };

    private static ModalidadeNoCadastro RetiradaDaAmpla(string codigo) =>
        ModalidadesNoCadastroDeTeste.Da(codigo, NaturezaLegalModalidade.AcaoAfirmativa, ComposicaoVagasModalidade.RetiradaDeOutraModalidade) with { ComposicaoOrigemCodigo = Ampla };

    private static Dictionary<string, BaseLegalCongelada> BasesLegais() => new()
    {
        [BaseLegalDoFada] = new(Guid.CreateVersion7(), BaseLegalDoFada, "PORTARIA", "Portaria de teste", "Descrição da portaria de teste", [("1504208", "Marabá", "PA")]),
    };

    internal static DeclaracoesDoDocumento Declaracoes(VagasNoDocumento? vagas) => new(DocumentoLidoDeTeste.Identificacao(), []) { Vagas = vagas };

    internal static VagasNoDocumento Vagas(params DistribuicaoNoDocumento[] distribuicoes) => new(1, distribuicoes);

    /// <summary>A distribuição institucional conforme: duas modalidades que somam as dez vagas ofertadas.</summary>
    internal static DistribuicaoNoDocumento Distribuicao(int posicao, string? oferta = OfertaViva) => new(
        posicao,
        oferta,
        10,
        [new(0, Ampla, 6), new(1, AmplaComDeficiencia, 4)])
    {
        RegraDeDistribuicao = Institucional,
    };

    internal static DestinoDaCascataNoDocumento Destino(int posicao, string? origem, string? destino) => new(posicao, origem, destino);

    /// <summary>A cascata que a regra do catálogo declara: da cota A para a B e dela para a ampla concorrência.</summary>
    internal static CascataNoDocumento Cascata(params DestinoDaCascataNoDocumento[] destinos) => new(Remanejamento, Ampla, destinos);

    internal static DestinoDaCascataNoDocumento[] DestinosConformes() => [Destino(0, CotaA, CotaB), Destino(1, CotaA, Ampla), Destino(2, CotaB, Ampla)];

    internal static FadaNoDocumento FadaConforme(bool aplica = true) => new(aplica, Fada, 1.2m, null, "MORA_NA_REGIAO", BaseLegalDoFada);

    internal static AtendimentoNoDocumento AtendimentoConforme() => new(
        [new(0, CondicaoPcd), new(1, CondicaoDeLactante)],
        [new(0, RecursoDeSala)],
        [new(0, TipoVisual)]);

    internal static (string? Ponteiro, string Codigo)[] Causas(IEnumerable<ParteRecusada> partes) =>
        [.. partes.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code))];

    private static OfertaDeCursoCitada Oferta(string codigo, string curso) =>
        new(Guid.CreateVersion7(), codigo, curso, "Bacharelado", "Campus de Marabá", ["NOTURNO"], null, null, null);

    private static Dictionary<string, OpcaoDeAtendimentoCitada> Opcoes(params string[] codigos) =>
        codigos.ToDictionary(static codigo => codigo, static codigo => new OpcaoDeAtendimentoCitada(Guid.CreateVersion7(), codigo, $"Nome de {codigo}"), StringComparer.Ordinal);

    private static Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> Regras() => new()
    {
        [(Institucional.Codigo!, "v1")] = Regra(TipoRegra.RegraDistribuicaoVagas, Institucional.Codigo!),
        [(Ajuste.Codigo!, "v1")] = Regra(TipoRegra.RegraAjusteDistribuicaoVagas, Ajuste.Codigo!),
        [(Remanejamento.Codigo!, "v1")] = Regra(TipoRegra.CriterioRemanejamento, Remanejamento.Codigo!, DeclaracaoDaCascata),
        [(Fada.Codigo!, "v1")] = Regra(TipoRegra.RegraFada, Fada.Codigo!),
    };

    private static RegraDoCatalogoCitada Regra(TipoRegra tipo, string codigo, JsonElement declaracao = default) =>
        new(tipo, ReferenciaRegra.De(codigo, "v1", Hash), declaracao);

    private static JsonElement DeclaracaoDaCascata
    {
        get
        {
            using JsonDocument declaracao = JsonDocument.Parse(
                $$"""{"destinoFinalCodigo":"{{Ampla}}","ordens":[{"origem":"{{CotaA}}","destinos":["{{CotaB}}","{{Ampla}}"]},{"origem":"{{CotaB}}","destinos":["{{Ampla}}"]}]}""");

            return declaracao.RootElement.Clone();
        }
    }
}
