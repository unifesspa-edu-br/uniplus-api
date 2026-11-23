namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaPoliticaDeOrdenacao;

public sealed class PoliticaDeOrdenacaoIdentidadeDeOrigemTests
{
    private static FaseCronograma FaseDeAlfaFase() => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        id: IdFixo(1),
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "ALFA_FASE", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: true, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 2, Inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("ALFA_FASE", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);

    private static FaseCronograma FaseDeZetaFase() => FaseCronograma.Reidratar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        id: IdFixo(2),
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: "ZETA_FASE", OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: true, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("ZETA_FASE", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);

    [Fact(DisplayName = "cronogramaFases.fases: Ordem governa sobre o conteúdo — oráculo de sequência exata")]
    public void CronogramaFases_Fases_OrdemGovernaSobreConteudo()
    {
        // Id CONTRÁRIO à Ordem também: ZETA_FASE (Ordem 1) recebe o Id MAIOR, ALFA_FASE (Ordem 2)
        // o MENOR — se o Id decidisse a posição, o resultado seria [ALFA_FASE, ZETA_FASE], o mesmo
        // "errado" que o código (proxy de conteúdo) daria.
        FaseCronograma faseZeta = FaseDeZetaFase();
        FaseCronograma faseAlfa = FaseDeAlfaFase();

        ConferirCronogramaContrario(faseZeta, faseAlfa);

        ProcessoSeletivo processo = Montar(cronogramaFases: [faseAlfa, faseZeta]);

        JsonArray fasesJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["cronogramaFases"]!["fases"]!.AsArray();

        fasesJson.Select(static f => f!["codigo"]!.GetValue<string>()).Should().Equal(["ZETA_FASE", "ALFA_FASE"]);
    }

    [Fact(DisplayName = "cronogramaFases.fases[].bancasRequeridas: o código governa sobre a identidade de origem — oráculo de sequência exata")]
    public void CronogramaFases_BancasRequeridas_CodigoGovernaSobreIdentidadeDeOrigem()
    {
        BancaRequerida bancaComOrigemMenor = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.Entrevista, IdFixo(1)), []);
        BancaRequerida bancaComOrigemMaior = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value, TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, IdFixo(2)), []);

        JsonArray bancasJson = BancasRequeridasEnvelopadas(FaseComAsBancas("INSCRICAO", bancaComOrigemMenor, bancaComOrigemMaior));

        bancasJson.Select(static b => b!["codigo"]!.GetValue<string>()).Should().Equal(["BANCA_ANALISE_DOCUMENTAL", "BANCA_ENTREVISTA"]);
    }

    // (código do tipo) empata: as duas bancas são do MESMO tipo. Quem desempata é a
    // posição no tipo, que o processo calcula pela ordem de chegada — nunca o id nem os candidatos
    // atendidos.
    [Fact(DisplayName = "cronogramaFases.fases[].bancasRequeridas: duas bancas do mesmo tipo seguem a posição no tipo — oráculo de sequência exata")]
    public void CronogramaFases_BancasRequeridas_MesmoTipo_SeguemAPosicaoNoTipo()
    {
        Guid tipoUnico = IdFixo(1);
        BancaRequerida bancaDaRenda = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, tipoUnico), [CategoriaJulgada.Criar(IdFixo(9), "RENDA")]);
        BancaRequerida bancaEtnicoRacial = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, tipoUnico), [CategoriaJulgada.Criar(IdFixo(8), "ETNICO_RACIAL")]);

        CandidatosAtendidosNaOrdemDasBancas(FaseComAsBancas("ANALISE", bancaDaRenda, bancaEtnicoRacial))
            .Should().Equal(["RENDA", "ETNICO_RACIAL"]);
        CandidatosAtendidosNaOrdemDasBancas(FaseComAsBancas("ANALISE", bancaEtnicoRacial, bancaDaRenda))
            .Should().Equal(["ETNICO_RACIAL", "RENDA"]);
    }

    private static string[] CandidatosAtendidosNaOrdemDasBancas(FaseCronograma fase) =>
        [.. BancasRequeridasEnvelopadas(fase).Select(static b => b!["candidatosAtendidos"]!.AsArray()[0]!["codigo"]!.GetValue<string>())];

    // O id de origem é identidade técnica e não pode vazar para dentro do hash: dois
    // candidatos atendidos com as MESMAS categorias precisam produzir os mesmos bytes.
    [Fact(DisplayName = "cronogramaFases.fases[].bancasRequeridas[].candidatosAtendidos: o conteúdo (código) governa — oráculo de sequência exata")]
    public void CronogramaFases_CandidatosAtendidos_CodigoGoverna()
    {
        BancaRequerida banca = BancaRequerida.Criar(ChaveLocal.Criar("banca").Value,
            TiposDeBancaCopiadosDeTeste.De(TipoDeBanca.AnaliseDocumental, IdFixo(1)),
            [
                CategoriaJulgada.Criar(IdFixo(1), "ZETA_CATEGORIA"),
                CategoriaJulgada.Criar(IdFixo(2), "ALFA_CATEGORIA"),
            ]);

        JsonArray candidatosAtendidosJson = BancasRequeridasEnvelopadas(FaseComAsBancas("ANALISE", banca))[0]!["candidatosAtendidos"]!.AsArray();

        candidatosAtendidosJson.Select(static c => c!["codigo"]!.GetValue<string>()).Should().Equal(["ALFA_CATEGORIA", "ZETA_CATEGORIA"]);
    }

    private static FaseCronograma FaseComAsBancas(string codigo, params BancaRequerida[] bancas) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        new OrigemCanonicaDaFase(
            OrigemId: Guid.CreateVersion7(), Codigo: codigo, OrigemData: OrigemDataFase.NoEdital, AgrupaEtapas: true, PermiteComplementacao: true, ColetaInscricao: true,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 1, Inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero), UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: bancas).Value!;

    private static JsonArray BancasRequeridasEnvelopadas(FaseCronograma fase) =>
        ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(Montar(cronogramaFases: [fase]))))
            ["cronogramaFases"]!["fases"]![0]!["bancasRequeridas"]!.AsArray();

    [Fact(DisplayName = "dadosDosFormularios: Ordem governa sobre o conteúdo (FatoCodigo) — oráculo de sequência exata")]
    public void FatosColetados_OrdemGovernaSobreConteudo()
    {
        FatoColetado fatoZeta = FatoColetadoDeTeste.Coletando("ZETA_FATO", 0).ComRotulo("Rótulo Zeta").Construir();
        FatoColetado fatoAlfa = FatoColetadoDeTeste.Coletando("ALFA_FATO", 1).ComRotulo("Rótulo Alfa").Construir();

        new[] { fatoZeta, fatoAlfa }.OrderBy(static f => f.FatoCodigo, StringComparer.Ordinal).Select(static f => f.FatoCodigo)
            .Should().Equal(["ALFA_FATO", "ZETA_FATO"]);

        ProcessoSeletivo processo = Montar(fatosColetados: [fatoAlfa, fatoZeta]);

        JsonArray fatosJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["dadosDosFormularios"]!.AsArray();

        // O formulário de inscrição traz também a seção do conjunto básico, que vem antes.
        fatosJson.Select(static f => f!["codigoDoDado"]!.GetValue<string>()).Where(static c => c is "ZETA_FATO" or "ALFA_FATO").Should().Equal(["ZETA_FATO", "ALFA_FATO"]);
    }

    [Fact(DisplayName = "calculosDosDados[].regras: Ordem governa sobre o conteúdo (Contribui) — oráculo de sequência exata")]
    public void RegrasDerivacao_RegrasInternas_OrdemGovernaSobreConteudo()
    {
        RegraDerivacaoConfigurada regraZeta = RegraDerivacaoConfigurada.Criar(0, "ZETA_VALOR", null).Value!;
        RegraDerivacaoConfigurada regraAlfa = RegraDerivacaoConfigurada.Criar(1, "ALFA_VALOR", null).Value!;

        new[] { regraZeta, regraAlfa }.OrderBy(static r => r.Contribui, StringComparer.Ordinal).Select(static r => r.Contribui)
            .Should().Equal(["ALFA_VALOR", "ZETA_VALOR"]);

        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("FATO_TESTE", [regraAlfa, regraZeta]).Value!;
        ProcessoSeletivo processo = Montar(regrasDerivacao: [config]);

        JsonArray regrasJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["calculosDosDados"]![0]!["regras"]!.AsArray();

        regrasJson.Select(static r => r!["contribui"]!.GetValue<string>()).Should().Equal(["ZETA_VALOR", "ALFA_VALOR"]);
    }

    [Fact(DisplayName = "cascataRemanejamento.ordens[].destinos: Ordem governa sobre o conteúdo, dentro da mesma origem — oráculo de sequência exata")]
    public void CascataRemanejamento_DestinosDaMesmaOrigem_OrdemGovernaSobreConteudo()
    {
        DestinoRemanejamento destinoZeta = DestinoRemanejamento.Criar("ORIGEM_TESTE", 1, "ZETA_DEST").Value!;
        DestinoRemanejamento destinoAlfa = DestinoRemanejamento.Criar("ORIGEM_TESTE", 2, "ALFA_DEST").Value!;

        ConferirDestinosContrarios(destinoZeta, destinoAlfa);

        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, 'e'), "FALLBACK_TESTE", [destinoAlfa, destinoZeta]).Value!;

        ProcessoSeletivo processo = MontarComCascata(cascata);

        JsonArray destinosJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))
            ["cascataRemanejamento"]!["ordens"]![0]!["destinos"]!.AsArray();

        destinosJson.Select(static d => d!.GetValue<string>()).Should().Equal(["ZETA_DEST", "ALFA_DEST"]);
    }

    [Fact(DisplayName = "cascataRemanejamento.ordens: as origens ordenam pelo código, ordinal — não pela ordem física de entrada")]
    public void CascataRemanejamento_Origens_OrdenaAlfabeticamentePorCodigo()
    {
        DestinoRemanejamento destinoDaZeta = DestinoRemanejamento.Criar("ZETA_ORIGEM", 1, "DESTINO_COMUM_A").Value!;
        DestinoRemanejamento destinoDaAlfa = DestinoRemanejamento.Criar("ALFA_ORIGEM", 1, "DESTINO_COMUM_B").Value!;

        // Inserida fisicamente na ordem ERRADA (ZETA_ORIGEM antes de ALFA_ORIGEM) — um canonicalizador
        // que preservasse a ordem de ENTRADA reproduziria esta mesma sequência errada.
        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, 'f'), "FALLBACK_TESTE", [destinoDaZeta, destinoDaAlfa]).Value!;

        ProcessoSeletivo processo = MontarComCascata(cascata);

        JsonArray ordensJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["cascataRemanejamento"]!["ordens"]!.AsArray();

        ordensJson.Should().HaveCount(2, "pré-condição: duas origens distintas sob teste");
        ordensJson.Select(static o => o!["origem"]!.GetValue<string>()).Should().Equal(["ALFA_ORIGEM", "ZETA_ORIGEM"]);
    }

    // Duas regras sem grupo e dois itens permitem observar as duas ordenações.
    [Fact(DisplayName = "A ordem declarada governa as regras sem grupo e os itens do grupo")]
    public void RegrasDeComprovacao_OrdemDeclaradaPrevalece()
    {
        FaseCronograma fase = FaseMinima();
        RegraDeComprovacao regraDeDocumentoSemGrupo = RegraDeDocumentoSemGrupo(fase);
        RegraDeComprovacao itemZeta = RegraDeDocumentoComIdentidade(fase, 2, 4, 0, "ITEM_ZETA");
        RegraDeComprovacao itemAlfa = RegraDeDocumentoComIdentidade(fase, 1, 3, 1, "ITEM_ALFA");
        RegraDeComprovacao grupoSemGrupo = RegraDeComprovacao.CriarGrupo(FormaDeComprovacao.QuantidadeMinimaDeItens, 0, 1, null, [], [itemAlfa, itemZeta]).Value!;
        ConferirEntradasContrariasAOrdem(regraDeDocumentoSemGrupo, grupoSemGrupo, itemAlfa, itemZeta);
        ProcessoSeletivo processo = Montar(cronogramaFases: [fase], documentosExigidos: [regraDeDocumentoSemGrupo, grupoSemGrupo]);
        JsonArray regras = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["regrasDeComprovacao"]!.AsArray();
        regras.Should().HaveCount(2);
        regras.Select(static r => r!["forma"]!.GetValue<string>()).Should().Equal(["QUANTIDADE_MINIMA_DE_ITENS", "DOCUMENTO"]);
        JsonArray itens = regras[0]!["itens"]!.AsArray();
        itens.Should().HaveCount(2);
        itens.Select(static f => f!["exigenciaId"]!.GetValue<Guid>()).Should().Equal([itemZeta.DocumentoExigidoId!.Value, itemAlfa.DocumentoExigidoId!.Value]);
    }

    private static RegraDeComprovacao RegraDeDocumentoSemGrupo(FaseCronograma fase)
    {
        DocumentoExigido documento = DocumentoExigido.Criar(
            new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), "REGRA_ZETA", "Documento sem grupo", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
            new MomentoDaExigencia(fase.Id, null, FinalidadeFormulario.Inscricao),
            new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
            new IndeferimentoDoDocumento(false, null, []),
            RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
            null).Value!;
        return RegraDeComprovacao.CriarRegraDeDocumento(documento, 1).Value!;
    }

    private static RegraDeComprovacao RegraDeDocumentoComIdentidade(FaseCronograma fase, int idDocumento, int idDaRegra, int ordem, string codigo)
    {
        DocumentoExigido documento = DocumentoExigido.Reidratar(
            IdFixo(idDocumento),
            new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), codigo, "Documento do grupo", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal),
            new MomentoDaExigencia(fase.Id, null, FinalidadeFormulario.Inscricao),
            new AplicabilidadeDaExigencia(Aplicabilidade.Geral, []),
            new IndeferimentoDoDocumento(false, null, []),
            RequisitosDoDocumentoDeTeste.Com(FormatosPermitidos.Criar(true, null).Value!),
            null);
        return RegraDeComprovacao.ReidratarRegraDeDocumento(IdFixo(idDaRegra), ordem, documento, RegraDeComprovacao.QuantidadeMinimaPadrao, null, null);
    }

    private static void ConferirEntradasContrariasAOrdem(RegraDeComprovacao regraDeDocumento, RegraDeComprovacao grupo, RegraDeComprovacao alfa, RegraDeComprovacao zeta)
    {
        // A ordenação pela forma das regras ou pelo identificador dos itens deve divergir da ordem declarada.
        new[] { regraDeDocumento, grupo }.OrderBy(static r => r.Forma.Codigo(), StringComparer.Ordinal).Select(static r => r.Forma.Codigo())
            .Should().Equal(["DOCUMENTO", "QUANTIDADE_MINIMA_DE_ITENS"]);
        new[] { alfa, zeta }.OrderBy(static f => f.Id).Select(static f => f.DocumentoExigidoId)
            .Should().Equal([alfa.DocumentoExigidoId, zeta.DocumentoExigidoId]);
    }
    private static void ConferirCronogramaContrario(FaseCronograma faseZeta, FaseCronograma faseAlfa)
    {
        new[] { faseZeta, faseAlfa }.OrderBy(static f => f.Codigo, StringComparer.Ordinal).Select(static f => f.Codigo)
            .Should().Equal(["ALFA_FASE", "ZETA_FASE"]);
        new[] { faseZeta, faseAlfa }.OrderBy(static f => f.Id).Select(static f => f.Codigo)
            .Should().Equal(["ALFA_FASE", "ZETA_FASE"]);

    }
    private static void ConferirDestinosContrarios(DestinoRemanejamento destinoZeta, DestinoRemanejamento destinoAlfa)
    {
        new[] { destinoZeta, destinoAlfa }.OrderBy(static d => d.ModalidadeDestinoCodigo, StringComparer.Ordinal).Select(static d => d.ModalidadeDestinoCodigo)
            .Should().Equal(["ALFA_DEST", "ZETA_DEST"]);

    }
}
