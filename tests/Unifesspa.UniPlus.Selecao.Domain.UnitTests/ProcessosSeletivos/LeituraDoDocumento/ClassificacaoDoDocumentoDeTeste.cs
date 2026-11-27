namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>A classificação do documento e os cadastros citados que os testes da leitura da classificação montam.</summary>
internal static class ClassificacaoDoDocumentoDeTeste
{
    internal const string EtapaAdotada = "prova_objetiva";
    internal const string EtapaRecusada = "prova_recusada";
    internal const string ChaveDigitada = "chave-digitada-no-documento";
    internal const string RegraDigitada = "REGRA-DIGITADA-NO-DOCUMENTO";

    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    internal static ReferenciaDeRegraNoDocumento Referencia<T>(T regra)
        where T : struct, Enum =>
        new(regra.Codigo(), "v1");

    internal static EtapasLidas Etapas() => new(
        [],
        new Dictionary<string, Guid> { [EtapaAdotada] = Guid.CreateVersion7() },
        new HashSet<string>(StringComparer.Ordinal) { EtapaRecusada },
        [],
        []);

    /// <summary>Os dados do candidato do vocabulário de desempate de teste: um de sim ou não e um de opção.</summary>
    internal static IReadOnlyDictionary<string, DescritorFatoCandidato> VocabularioDeDados() => new Dictionary<string, DescritorFatoCandidato>
    {
        ["PROFESSOR_RURAL"] = DescritorFatoCandidato.Criar("PROFESSOR_RURAL", TipoDominioFato.Booleano, null).Value!,
        ["MODALIDADE"] = DescritorFatoCandidato.Criar("MODALIDADE", TipoDominioFato.CategoricoEstatico, ["AMPLA", "COTA"]).Value!,
    };

    internal static CadastrosCitadosNoDocumento Cadastros(QuadroDePesosCitado? quadro = null, IReadOnlyDictionary<string, DescritorFatoCandidato>? dados = null) => new(DocumentoLidoDeTeste.LidoEm)
    {
        Regras = RegrasDoCatalogo(),
        DadosDoDesempate = dados ?? new Dictionary<string, DescritorFatoCandidato>(),
        QuadrosDePesos = quadro is null ? new Dictionary<string, QuadroDePesosCitado>() : new Dictionary<string, QuadroDePesosCitado> { ["RES-DE-TESTE"] = quadro },
    };

    private static Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada> RegrasDoCatalogo() =>
        new[]
        {
            Entradas(TipoRegra.RegraCalculo, RegraDeCalculoDaNota.MediaPonderada, RegraDeCalculoDaNota.ClassificacaoImportada),
            Entradas(TipoRegra.RegraArredondamento, RegraDeArredondamentoDaNota.Truncar),
            Entradas(TipoRegra.RegraOrdemAlocacao, RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria),
            Entradas(TipoRegra.RegraEliminacao, RegraDeEliminacaoPorCalculo.NotaMinimaNaEtapa, RegraDeEliminacaoPorCalculo.CorteEmAreaDoEnem, RegraDeEliminacaoPorCalculo.AusenciaNaEtapa, RegraDeEliminacaoPorCalculo.ZeroEmAreaDoEnem, RegraDeEliminacaoPorCalculo.FaltaEmDiaDeProvaDoEnem),
            Entradas(TipoRegra.CriterioDesempate, RegraDeDesempate.MaiorNotaNaEtapa, RegraDeDesempate.MaiorIdade, RegraDeDesempate.Idoso, RegraDeDesempate.CondicaoSobreDado, RegraDeDesempate.MaiorNotaPorAreaDoEnem),
        }.SelectMany(static entradas => entradas).ToDictionary(static entrada => entrada.Key, static entrada => entrada.Value);

    private static IEnumerable<KeyValuePair<(string Codigo, string Versao), RegraDoCatalogoCitada>> Entradas<T>(TipoRegra tipo, params T[] regras)
        where T : struct, Enum =>
        regras.Select(regra => KeyValuePair.Create(Chave(regra), Regra(tipo, regra)));

    /// <summary>A classificação de cálculo local, sem eliminação nem desempate: o que o teste não declara é conforme.</summary>
    internal static ClassificacaoNoDocumento Local() => new(
        Referencia(RegraDeCalculoDaNota.MediaPonderada),
        Referencia(RegraDeArredondamentoDaNota.Truncar),
        2,
        100m,
        Referencia(RegraDeOrdemDeAlocacao.PrimeiraOpcaoPrioritaria),
        false,
        null);

    internal static (string? Ponteiro, string Codigo)[] Causas(IEnumerable<ParteRecusada> partes) =>
        [.. partes.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code))];

    internal static QuadroDePesosCitado QuadroCompleto()
    {
        GrupoPesoAreaEnemCongelado grupo = GrupoPesoAreaEnemCongelado.Criar(
            "AMPLA_CONCORRENCIA", "Ampla concorrência", "Resolução de teste", [("REDACAO", "Redação", 2m, 400m)]).Value!;

        return new([grupo], []);
    }

    private static (string Codigo, string Versao) Chave<T>(T regra)
        where T : struct, Enum =>
        (regra.Codigo(), "v1");

    private static RegraDoCatalogoCitada Regra<T>(TipoRegra tipo, T regra)
        where T : struct, Enum =>
        new(tipo, ReferenciaRegra.De(regra.Codigo(), "v1", Hash));
}
