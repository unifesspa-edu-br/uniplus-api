namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

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

public sealed partial class PoliticaDeOrdenacaoTests
{
    [Fact(DisplayName = "cronogramaFases.fases: Ordem governa sobre o conteúdo — oráculo de sequência exata")]
    public void CronogramaFases_Fases_OrdemGovernaSobreConteudo()
    {
        // Id CONTRÁRIO à Ordem também: ZETA_FASE (Ordem 1) recebe o Id MAIOR, ALFA_FASE (Ordem 2)
        // o MENOR — se o Id decidisse a posição, o resultado seria [ALFA_FASE, ZETA_FASE], o mesmo
        // "errado" que o código (proxy de conteúdo) daria.
        FaseCronograma faseZeta = FaseCronograma.Reidratar(
            id: IdFixo(2), ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "ZETA_FASE",
            origemData: OrigemDataFase.NoEdital, agrupaEtapas: true,
            permiteComplementacao: true, coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("ZETA_FASE", PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps);
        FaseCronograma faseAlfa = FaseCronograma.Reidratar(
            id: IdFixo(1), ordem: 2, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: "ALFA_FASE",
            origemData: OrigemDataFase.NoEdital, agrupaEtapas: true,
            permiteComplementacao: true, coletaInscricao: true, coletaSolicitacaoIsencao: false,
            inicio: new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 2, 28, 0, 0, 0, TimeSpan.Zero),
            produtos: [ProdutoDaFase.Criar("ALFA_FASE", PapelProdutoFase.Definitivo)], faseConcluinteCodigo: null, emiteParecerIndividual: false, bancasRequeridas: [], regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps);

        new[] { faseZeta, faseAlfa }.OrderBy(static f => f.Codigo, StringComparer.Ordinal).Select(static f => f.Codigo)
            .Should().Equal(["ALFA_FASE", "ZETA_FASE"], "pré-condição: ordenar pelo código (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");
        new[] { faseZeta, faseAlfa }.OrderBy(static f => f.Id).Select(static f => f.Codigo)
            .Should().Equal(["ALFA_FASE", "ZETA_FASE"], "pré-condição: ordenar pelo Id dá o oposto do oráculo de Ordem abaixo");

        ProcessoSeletivo processo = Montar(cronogramaFases: [faseAlfa, faseZeta]);

        JsonArray fasesJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["cronogramaFases"]!["fases"]!.AsArray();

        fasesJson.Select(static f => f!["codigo"]!.GetValue<string>()).Should().Equal(
            ["ZETA_FASE", "ALFA_FASE"],
            "Ordem (1 para ZETA_FASE, 2 para ALFA_FASE) governa a posição — o código, em ordem alfabética, apontaria para o oposto");
    }

    [Fact(DisplayName = "cronogramaFases.fases[].bancasRequeridas: identidade de origem governa sobre o conteúdo — oráculo de sequência exata")]
    public void CronogramaFases_BancasRequeridas_IdentidadeDeOrigemGovernaSobreConteudo()
    {
        BancaRequerida bancaComOrigemMenor = BancaRequerida.Criar(IdFixo(1), "ZETA_BANCA", []);
        BancaRequerida bancaComOrigemMaior = BancaRequerida.Criar(IdFixo(2), "ALFA_BANCA", []);

        new[] { bancaComOrigemMenor, bancaComOrigemMaior }.OrderBy(static b => b.Codigo, StringComparer.Ordinal).Select(static b => b.Codigo)
            .Should().Equal(["ALFA_BANCA", "ZETA_BANCA"], "pré-condição: ordenar pelo código (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");

        JsonArray bancasJson = BancasRequeridasEnvelopadas(FaseComAsBancas("INSCRICAO", bancaComOrigemMaior, bancaComOrigemMenor));

        bancasJson.Select(static b => b!["codigo"]!.GetValue<string>()).Should().Equal(
            ["ZETA_BANCA", "ALFA_BANCA"],
            "a identidade de origem (TipoBancaOrigemId) governa a posição — o código, em ordem alfabética, apontaria para o oposto");
    }

    // (TipoBancaOrigemId, Codigo) empata: as duas bancas são do MESMO tipo, que é o
    // arranjo que o recorte de competência existe para tornar declarável. Sem uma
    // terceira chave, a posição no array passaria a depender da ordem em que o EF
    // materializou as linhas, e a mesma configuração produziria dois hashes.
    [Fact(DisplayName = "cronogramaFases.fases[].bancasRequeridas: duas bancas do mesmo tipo são desempatadas pelo recorte — oráculo de sequência exata")]
    public void CronogramaFases_BancasRequeridas_MesmoTipo_DesempatadasPeloRecorte()
    {
        Guid tipoUnico = IdFixo(1);
        BancaRequerida bancaDaRenda = BancaRequerida.Criar(
            tipoUnico, "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(IdFixo(9), "RENDA")]);
        BancaRequerida bancaEtnicoRacial = BancaRequerida.Criar(
            tipoUnico, "BANCA_ANALISE_DOCUMENTAL", [CategoriaJulgada.Criar(IdFixo(8), "ETNICO_RACIAL")]);

        JsonArray bancasJson = BancasRequeridasEnvelopadas(FaseComAsBancas("ANALISE", bancaDaRenda, bancaEtnicoRacial));

        bancasJson
            .Select(static b => b!["recorteDeCompetencia"]!.AsArray()[0]!["codigo"]!.GetValue<string>())
            .Should().Equal(
                ["ETNICO_RACIAL", "RENDA"],
                "empatado o par (identidade de origem, código), os bytes do próprio item desempatam — e é a banca do recorte ETNICO_RACIAL que vem primeiro, embora tenha sido declarada por último");
    }

    // O id de origem é identidade técnica e não pode vazar para dentro do hash: dois
    // recortes com as MESMAS categorias precisam produzir os mesmos bytes.
    [Fact(DisplayName = "cronogramaFases.fases[].bancasRequeridas[].recorteDeCompetencia: o conteúdo (código) governa — oráculo de sequência exata")]
    public void CronogramaFases_RecorteDeCompetencia_CodigoGoverna()
    {
        BancaRequerida banca = BancaRequerida.Criar(
            IdFixo(1),
            "BANCA_ANALISE_DOCUMENTAL",
            [
                CategoriaJulgada.Criar(IdFixo(1), "ZETA_CATEGORIA"),
                CategoriaJulgada.Criar(IdFixo(2), "ALFA_CATEGORIA"),
            ]);

        JsonArray recorteJson = BancasRequeridasEnvelopadas(FaseComAsBancas("ANALISE", banca))[0]!["recorteDeCompetencia"]!.AsArray();

        recorteJson.Select(static c => c!["codigo"]!.GetValue<string>()).Should().Equal(
            ["ALFA_CATEGORIA", "ZETA_CATEGORIA"],
            "o código da categoria é a chave natural do recorte e governa a posição, apesar da ordem de declaração");
    }

    private static FaseCronograma FaseComAsBancas(string codigo, params BancaRequerida[] bancas) => FaseCronograma.Criar(
        ordem: 1, faseCanonicaOrigemId: Guid.CreateVersion7(), codigo: codigo,
        origemData: OrigemDataFase.NoEdital, agrupaEtapas: true, permiteComplementacao: true, coletaInscricao: true, coletaSolicitacaoIsencao: false,
        inicio: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), fim: new DateTimeOffset(2026, 1, 31, 0, 0, 0, TimeSpan.Zero),
        produtos: [ProdutoDaFase.Criar("INSCRICAO", PapelProdutoFase.Definitivo)],
        faseConcluinteCodigo: null,
        emiteParecerIndividual: false,
        bancasRequeridas: bancas, regraRecurso: null, unidadeResponsavel: UnidadesDeTeste.Ceps).Value!;

    private static JsonArray BancasRequeridasEnvelopadas(FaseCronograma fase) =>
        EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(Montar(cronogramaFases: [fase]))))
            ["cronogramaFases"]!["fases"]![0]!["bancasRequeridas"]!.AsArray();

    [Fact(DisplayName = "fatosColetados: Ordem governa sobre o conteúdo (FatoCodigo) — oráculo de sequência exata")]
    public void FatosColetados_OrdemGovernaSobreConteudo()
    {
        FatoColetado fatoZeta = FatoColetado.Criar("ZETA_FATO", 0, "Rótulo Zeta", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        FatoColetado fatoAlfa = FatoColetado.Criar("ALFA_FATO", 1, "Rótulo Alfa", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

        new[] { fatoZeta, fatoAlfa }.OrderBy(static f => f.FatoCodigo, StringComparer.Ordinal).Select(static f => f.FatoCodigo)
            .Should().Equal(["ALFA_FATO", "ZETA_FATO"], "pré-condição: ordenar pelo FatoCodigo (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");

        ProcessoSeletivo processo = Montar(fatosColetados: [fatoAlfa, fatoZeta]);

        JsonArray fatosJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["fatosColetados"]!.AsArray();

        // O formulário de inscrição traz também a seção do conjunto básico, que vem antes.
        fatosJson.Select(static f => f!["fatoCodigo"]!.GetValue<string>()).Where(static c => c is "ZETA_FATO" or "ALFA_FATO").Should().Equal(
            ["ZETA_FATO", "ALFA_FATO"],
            "Ordem (0 para ZETA_FATO, 1 para ALFA_FATO) governa a posição — o FatoCodigo, em ordem alfabética, apontaria para o oposto");
    }

    [Fact(DisplayName = "regrasDerivacao[].regras: Ordem governa sobre o conteúdo (Contribui) — oráculo de sequência exata")]
    public void RegrasDerivacao_RegrasInternas_OrdemGovernaSobreConteudo()
    {
        RegraDerivacaoConfigurada regraZeta = RegraDerivacaoConfigurada.Criar(0, "ZETA_VALOR", null).Value!;
        RegraDerivacaoConfigurada regraAlfa = RegraDerivacaoConfigurada.Criar(1, "ALFA_VALOR", null).Value!;

        new[] { regraZeta, regraAlfa }.OrderBy(static r => r.Contribui, StringComparer.Ordinal).Select(static r => r.Contribui)
            .Should().Equal(["ALFA_VALOR", "ZETA_VALOR"], "pré-condição: ordenar por Contribui (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");

        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("FATO_TESTE", [regraAlfa, regraZeta]).Value!;
        ProcessoSeletivo processo = Montar(regrasDerivacao: [config]);

        JsonArray regrasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["regrasDerivacao"]![0]!["regras"]!.AsArray();

        regrasJson.Select(static r => r!["contribui"]!.GetValue<string>()).Should().Equal(
            ["ZETA_VALOR", "ALFA_VALOR"],
            "Ordem (0 para ZETA_VALOR, 1 para ALFA_VALOR) governa a posição — Contribui, em ordem alfabética, apontaria para o oposto");
    }

    [Fact(DisplayName = "cascataRemanejamento.ordens[].destinos: Ordem governa sobre o conteúdo, dentro da mesma origem — oráculo de sequência exata")]
    public void CascataRemanejamento_DestinosDaMesmaOrigem_OrdemGovernaSobreConteudo()
    {
        DestinoRemanejamento destinoZeta = DestinoRemanejamento.Criar("ORIGEM_TESTE", 1, "ZETA_DEST").Value!;
        DestinoRemanejamento destinoAlfa = DestinoRemanejamento.Criar("ORIGEM_TESTE", 2, "ALFA_DEST").Value!;

        new[] { destinoZeta, destinoAlfa }.OrderBy(static d => d.ModalidadeDestinoCodigo, StringComparer.Ordinal).Select(static d => d.ModalidadeDestinoCodigo)
            .Should().Equal(["ALFA_DEST", "ZETA_DEST"], "pré-condição: ordenar pelo código do destino (proxy de conteúdo) dá o oposto do oráculo de Ordem abaixo");

        ConfiguracaoCascataRemanejamento cascata = ConfiguracaoCascataRemanejamento.Criar(
            Regra(RegraRemanejamentoCodigo.Cascata, 'e'), "FALLBACK_TESTE", [destinoAlfa, destinoZeta]).Value!;

        ProcessoSeletivo processo = Montar(cascata: cascata);

        JsonArray destinosJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))
            ["cascataRemanejamento"]!["ordens"]![0]!["destinos"]!.AsArray();

        destinosJson.Select(static d => d!.GetValue<string>()).Should().Equal(
            ["ZETA_DEST", "ALFA_DEST"],
            "Ordem (1 para ZETA_DEST, 2 para ALFA_DEST) governa a posição dentro da origem — o código, em ordem alfabética, apontaria para o oposto");
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

        ProcessoSeletivo processo = Montar(cascata: cascata);

        JsonArray ordensJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["cascataRemanejamento"]!["ordens"]!.AsArray();

        ordensJson.Should().HaveCount(2, "pré-condição: duas origens distintas sob teste");
        ordensJson.Select(static o => o!["origem"]!.GetValue<string>()).Should().Equal(
            ["ALFA_ORIGEM", "ZETA_ORIGEM"],
            "as origens ordenam por código, ordinal — não pela ordem física de entrada, que era [ZETA_ORIGEM, ALFA_ORIGEM]");
    }

    /// <summary>
    /// Combina raízes e filhos num teste só, com a precondição que o parecer da issue nomeou:
    /// <b>duas raízes e dois irmãos</b> — duas raízes de um filho cada deixariam <c>filhos</c>
    /// trivial (qualquer ordenação produziria o mesmo resultado).
    /// </summary>
    [Fact(DisplayName = "arvoreSatisfacao: raízes e filhos — Ordem governa sobre o conteúdo, com duas raízes e dois irmãos")]
    public void ArvoreSatisfacao_RaizesEFilhos_OrdemGovernaSobreConteudo()
    {
        FaseCronograma fase = FaseMinima();

        DocumentoExigido docRaizFolha = DocumentoExigido.Criar(
            fase.Id, Guid.CreateVersion7(), "RAIZ_ZETA", "Documento raiz solteira", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null, [], [], null,
            FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, null, finalidade: FinalidadeFormulario.Inscricao).Value!;

        // Exigência e nó reidratados com Id FIXO e CONTRÁRIO à Ordem — não Guid.CreateVersion7(),
        // que alinharia acidentalmente os Ids com a ordem de criação (e, por tabela, com a Ordem
        // esperada): o filho ZETA (Ordem 0, deve vir PRIMEIRO) recebe o Id MAIOR; o filho ALFA
        // (Ordem 1, deve vir SEGUNDO) recebe o Id MENOR, tanto na exigência quanto no nó.
        DocumentoExigido docFilhoZeta = DocumentoExigido.Reidratar(
            IdFixo(2), fase.Id, Guid.CreateVersion7(), "FILHO_ZETA", "Documento filho zeta", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao);
        DocumentoExigido docFilhoAlfa = DocumentoExigido.Reidratar(
            IdFixo(1), fase.Id, Guid.CreateVersion7(), "FILHO_ALFA", "Documento filho alfa", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao);

        NoExigencia raizFolha = NoExigencia.CriarFolha(docRaizFolha, ordem: 1).Value!;
        // filhoZeta tem Ordem MENOR (0) mas Id do nó MAIOR — a chave em conflito que distingue
        // "Ordem governa" de "identidade técnica (Id) governa".
        NoExigencia filhoZeta = NoExigencia.Reidratar(
            IdFixo(4), TipoNo.Folha, ordem: 0, docFilhoZeta.Id, docFilhoZeta,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);
        NoExigencia filhoAlfa = NoExigencia.Reidratar(
            IdFixo(3), TipoNo.Folha, ordem: 1, docFilhoAlfa.Id, docFilhoAlfa,
            quantidadeMinima: NoExigencia.QuantidadeMinimaPadrao, consequencia: null, chaveDistincao: null,
            dataReferencia: null, ocorrenciasEsperadas: null, repetePorEntidade: null, basesLegais: [], filhos: []);

        // Dois irmãos — não um: duas raízes de um filho cada deixaria "filhos" trivial. Inseridos na
        // ordem física ERRADA (Alfa antes de Zeta).
        NoExigencia raizGrupo = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, ordem: 0, quantidadeMinima: 1, consequencia: null, basesLegais: [],
            filhos: [filhoAlfa, filhoZeta]).Value!;

        new[] { raizFolha, raizGrupo }.OrderBy(static r => r.Tipo.ToCodigo(), StringComparer.Ordinal).Select(static r => r.Tipo.ToCodigo())
            .Should().Equal(["FOLHA", "OU"], "pré-condição: ordenar pelo tipo (proxy de conteúdo) dá o oposto do oráculo de Ordem das raízes");
        // Pré-condição calculada sobre o MESMO campo que arvoreSatisfacao[].filhos[] realmente
        // emite para o nó (o Id, técnico) — não sobre TipoDocumentoCodigo, que esse caminho não
        // emite: um mutante que ordenasse os filhos pelo Id do nó produziria exatamente esta
        // sequência oposta.
        new[] { filhoAlfa, filhoZeta }.OrderBy(static f => f.Id).Select(static f => f.DocumentoExigidoId)
            .Should().Equal(
                [docFilhoAlfa.Id, docFilhoZeta.Id],
                "pré-condição: ordenar pelo Id do nó (identidade técnica, não conteúdo) dá o oposto do oráculo de Ordem dos filhos abaixo");

        // Inseridas na ordem física ERRADA (folha, cuja Ordem é maior, antes do grupo).
        ProcessoSeletivo processo = Montar(cronogramaFases: [fase], documentosExigidos: [raizFolha, raizGrupo]);

        JsonArray arvoreJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["arvoreSatisfacao"]!.AsArray();

        arvoreJson.Should().HaveCount(2, "pré-condição: duas raízes, como o cenário exige");
        arvoreJson.Select(static r => r!["tipo"]!.GetValue<string>()).Should().Equal(
            ["OU", "FOLHA"],
            "Ordem (0 para o grupo OU, 1 para a folha solteira) governa a posição das raízes — o tipo, em ordem alfabética, apontaria para o oposto");

        JsonArray filhosJson = arvoreJson[0]!["filhos"]!.AsArray();
        filhosJson.Should().HaveCount(2, "pré-condição: dois irmãos, não um — com um só, 'filhos' seria trivial");
        filhosJson.Select(static f => f!["exigenciaId"]!.GetValue<Guid>()).Should().Equal(
            [docFilhoZeta.Id, docFilhoAlfa.Id],
            "Ordem (0 para o filho ZETA, 1 para o filho ALFA) governa a posição dos irmãos — o Id do nó, contrário à Ordem, apontaria para o oposto");
    }
}
