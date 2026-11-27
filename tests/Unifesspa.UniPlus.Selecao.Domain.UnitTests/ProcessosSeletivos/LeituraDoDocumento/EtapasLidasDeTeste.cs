namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.FasesEEtapas;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>As etapas e os cadastros citados que os testes da leitura das etapas montam: o que não declaram tem o valor de uma etapa completa e conforme.</summary>
internal static class EtapasLidasDeTeste
{
    internal const string TipoDeEtapa = "PROVA_OBJETIVA";
    internal const string TipoDeBancaAvaliadora = "BANCA_CORRECAO_REDACOES";
    internal const string RegraDeApuracao = "APURACAO-DE-TESTE";
    internal const string FaseDaAvaliacao = "avaliacao";

    private const string Hash = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    private static readonly ReferenciaDeRegraNoDocumento PrazoDeRecurso = new(RegraDePrazoDoRecurso.ContadoDaPublicacaoDoAto.Codigo(), "v1");

    internal static CadastrosCitadosNoDocumento CadastrosDasEtapas() => new(LidoEm)
    {
        TiposDeEtapa = new Dictionary<string, TipoEtapaSnapshot> { [TipoDeEtapa] = TipoDeEtapaDoCadastro() },
        TiposDeBanca = new Dictionary<string, TipoDeBancaCopiado>
        {
            ["ANALISE_RECURSOS"] = TiposDeBancaCopiadosDeTeste.DeRecursos(),
            [TipoDeBancaAvaliadora] = TiposDeBancaCopiadosDeTeste.Avaliadora(),
        },
        Regras = new Dictionary<(string Codigo, string Versao), RegraDoCatalogoCitada>(Cadastros().Regras)
        {
            [(RegraDeApuracao, "v1")] = new(TipoRegra.RegraApuracaoAvaliacao, ReferenciaRegra.De(RegraDeApuracao, "v1", Hash)),
        },
        TiposDeAto = Cadastros().TiposDeAto,
    };

    internal static EtapasLidas Ler(IReadOnlyList<EtapaNoDocumento> etapas, params FaseNoDocumento[] fases) =>
        LeituraDasEtapas.Ler(etapas, LeituraDasFases.Ler(fases, Cadastros()), CadastrosDasEtapas());

    internal static EtapasLidas Ler(params EtapaNoDocumento[] etapas) => Ler(etapas, Fase(0, FaseDaAvaliacao, "INSCRICAO"));

    internal static EtapaNoDocumento Etapa(int posicao, string chave, string? tipo = TipoDeEtapa, string? fase = null) => new(
        posicao,
        chave,
        new("Prova objetiva de exemplo", tipo, fase, CaraterEtapa.Classificatoria, 1m, null),
        new(null, FormaDeAvaliacao.Objetiva, 10m, null, [], []),
        new(null, null),
        []);

    internal static EtapaNoDocumento ComBancas(this EtapaNoDocumento etapa, params BancaNoDocumento[] bancas) => etapa with { Bancas = bancas };

    internal static EtapaNoDocumento ComItens(this EtapaNoDocumento etapa, params ItemDeAvaliacaoNoDocumento[] itens) =>
        etapa with { Avaliacao = etapa.Avaliacao with { Itens = itens } };

    internal static EtapaNoDocumento ComNiveis(this EtapaNoDocumento etapa, params NivelDeAtendimentoNoDocumento[] niveis) =>
        etapa with { Avaliacao = etapa.Avaliacao with { Niveis = niveis } };

    internal static EtapaNoDocumento ComRegraDeApuracao(this EtapaNoDocumento etapa, ReferenciaDeRegraNoDocumento? regra) =>
        etapa with { Avaliacao = etapa.Avaliacao with { RegraDeApuracao = regra } };

    internal static EtapaNoDocumento Publicando(this EtapaNoDocumento etapa, params ProdutoNoDocumento[] produtos) =>
        etapa with { PublicacoesPrevistas = produtos };

    internal static EtapaNoDocumento ComRecursos(this EtapaNoDocumento etapa, params RecursoDaEtapaNoDocumento[] recursos) =>
        etapa with { Recursos = recursos };

    internal static BancaNoDocumento BancaDaEtapa(int posicao, string chave, string tipo = "ANALISE_RECURSOS", DeclaracaoDaDeliberacao? regra = null) =>
        new(posicao, chave, tipo, [], regra);

    internal static ItemDeAvaliacaoNoDocumento Item(int posicao, string chave, string? rotulo = "Item de exemplo", decimal? pontuacao = 5m) =>
        new(posicao, chave, rotulo, pontuacao);

    internal static NivelDeAtendimentoNoDocumento Nivel(int posicao, string chave, string? rotulo = "Nível de exemplo", decimal? fracao = 0.5m) =>
        new(posicao, chave, rotulo, fracao);

    internal static RecursoDaEtapaNoDocumento Recurso(
        int posicao,
        EventoInicialDoPrazo? evento = EventoInicialDoPrazo.PublicacaoDoAto,
        string? ato = AtoDoResultado,
        params ProcedimentoNoDocumento[] procedimentos) =>
        new(posicao, evento, new(PrazoDeRecurso, ato, ParametrosDoRecursoDeTeste.Declarados(2m, UnidadePrazo.DiasUteis), procedimentos));

    internal static RecursoDaEtapaNoDocumento ComRegraDoRecurso(this RecursoDaEtapaNoDocumento recurso, ReferenciaDeRegraNoDocumento regra) =>
        recurso with { Regra = recurso.Regra with { Regra = regra } };

    internal static RecursoDaEtapaNoDocumento ComParametros(this RecursoDaEtapaNoDocumento recurso, ParametrosDeclaradosDoRecurso parametros) =>
        recurso with { Regra = recurso.Regra with { Parametros = parametros } };

    internal static ProcedimentoNoDocumento Procedimento(int posicao, TipoMotivoDeDecisao tipo, string? itemChave = null) =>
        new(posicao, tipo, itemChave is null ? null : new AlvoNoDocumento(itemChave, null), new(CodigoDoProcedimento, "v1"));

    internal static OrigemNoDocumento Origem(EtapasLidas lidas) => OrigemNoDocumento.Reunir(lidas.Origens);

    internal static (string? Ponteiro, string Codigo)[] PartesRecusadas(EtapasLidas lidas) =>
        [.. Origem(lidas).PartesRecusadas.Select(static parte => (parte.Causa.Field, parte.Causa.Error.Code))];

    internal static string[] Mensagens(EtapasLidas lidas) => [.. Origem(lidas).PartesRecusadas.Select(static parte => parte.Causa.Error.Message)];

    internal static string[] Caminhos(EtapasLidas lidas) => [.. Origem(lidas).PartesRecusadas.Select(static parte => parte.Caminho)];

    private static TipoEtapaSnapshot TipoDeEtapaDoCadastro() =>
        TipoEtapaSnapshot.Criar(Guid.CreateVersion7(), TipoDeEtapa, "Prova objetiva", admitePontuacao: true, admiteEliminacao: true, notaDeOrigemNoEnem: false).Value!;
}
