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
    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados: Ordem governa sobre o Codigo — oráculo de sequência exata")]
    public void MetadadosFatos_ValoresDominioDeclarados_OrdemGovernaSobreCodigo()
    {
        // Descrições conflitantes que DIFEREM e PRECEDEM `ordem` na ordenação alfabética das
        // chaves do item canônico (`descricao` < `ordem` < `valorCodigo`) — com Descricao null
        // nos dois itens, a ordenação pelos bytes do item coincidiria com a ordenação por Ordem
        // (o primeiro byte que divergiria entre os dois itens seria já `ordem`), e um mutante que
        // trocasse a política por conteúdo passaria despercebido.
        ValorDominioDeclaradoCongelado valorZeta = new("ZETA_VALOR", "Zeta descricao", 0);
        ValorDominioDeclaradoCongelado valorAlfa = new("ALFA_VALOR", "Alfa descricao", 1);

        new[] { valorZeta, valorAlfa }.OrderBy(static v => v.Codigo, StringComparer.Ordinal).Select(static v => v.Codigo)
            .Should().Equal(["ALFA_VALOR", "ZETA_VALOR"], "pré-condição: ordenar pelo Codigo dá o oposto do oráculo de Ordem abaixo");

        // Pré-condição pelos bytes canônicos REAIS do item, na mesma forma que a produção emite
        // (`valorCodigo`/`descricao`/`ordem`, reordenados pelo perfil em `descricao`/`ordem`/
        // `valorCodigo`): um mutante que ordenasse pelo conteúdo do item divergiria já em
        // `descricao` — antes de alcançar `ordem` — e produziria a sequência oposta ao oráculo de
        // Ordem abaixo.
        byte[] bytesZeta = PerfilCanonicoV1.Instancia.Serializar(new JsonObject
        {
            ["valorCodigo"] = valorZeta.Codigo,
            ["descricao"] = valorZeta.Descricao,
            ["ordem"] = valorZeta.Ordem,
        });
        byte[] bytesAlfa = PerfilCanonicoV1.Instancia.Serializar(new JsonObject
        {
            ["valorCodigo"] = valorAlfa.Codigo,
            ["descricao"] = valorAlfa.Descricao,
            ["ordem"] = valorAlfa.Ordem,
        });
        ComparadorLexicograficoDeBytes.Instancia.Compare(bytesAlfa, bytesZeta).Should().BeLessThan(0,
            "pré-condição: os bytes canônicos reais do item (a chave `descricao`, que precede `ordem`) colocariam " +
            "ALFA_VALOR antes de ZETA_VALOR — o oposto do oráculo de Ordem abaixo — se a política fosse ordenar pelo " +
            "conteúdo do item em vez de por Ordem");

        Dictionary<string, MetadadoFatoCongelado> metadados = new(StringComparer.Ordinal)
        {
            ["COR_RACA"] = new MetadadoFatoCongelado(
                Codigo: "COR_RACA", Dominio: "CATEGORICO", Origem: "DECLARADO", Cardinalidade: "ESCALAR",
                PontoResolucao: "INSCRICAO", Binding: "CAMPO_FORMULARIO:COR_RACA",
                ValoresDominio: null, ValoresDominioDeclarados: [valorAlfa, valorZeta]),
        };

        ProcessoSeletivo processo = Montar();
        JsonArray declaradosJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo, metadados)))
            ["documentosExigidos"]!["metadadosFatos"]![0]!["valoresDominioDeclarados"]!.AsArray();

        declaradosJson.Select(static v => v!["valorCodigo"]!.GetValue<string>()).Should().Equal(
            ["ZETA_VALOR", "ALFA_VALOR"],
            "Ordem (0 para ZETA_VALOR, 1 para ALFA_VALOR) governa a posição — o Codigo, em ordem alfabética, apontaria para o oposto");
    }

    // ── camada 2 — identidade de negócio ──

    /// <summary>
    /// Cobre os TRÊS arrays de <c>SerializarAtendimento</c> — não só <c>condicoes</c>: embora as
    /// três coleções passem pela mesma FORMA de expressão (<c>OrderBy(*OrigemId)</c>), são três
    /// expressões <c>OrderBy</c> INDEPENDENTES; regredir uma sozinha (ex.: só <c>recursos</c> para
    /// conteúdo, ou só <c>tiposDeficiencia</c> sem ordenação) não é pego por um oráculo que só
    /// observa <c>condicoes</c>. <c>tiposDeficiencia</c> exige a condição PCD ofertada (ADR-0067) —
    /// por isso a terceira condição, cujo Id de origem fica FORA do conflito Zeta/Alfa.
    /// </summary>
    [Fact(DisplayName = "atendimento.condicoes/recursos/tiposDeficiencia: identidade de origem governa sobre o conteúdo — oráculo de sequência exata para os três arrays")]
    public void OfertaAtendimento_Condicoes_IdentidadeDeOrigemGovernaSobreConteudo()
    {
        OfertaCondicao condicaoComOrigemMenor = OfertaCondicao.Criar(IdFixo(1), "COD_ZETA", "Zeta Nome");
        OfertaCondicao condicaoComOrigemMaior = OfertaCondicao.Criar(IdFixo(2), "COD_ALFA", "Alfa Nome");
        OfertaCondicao condicaoPcd = OfertaCondicao.Criar(IdFixo(9), OfertaAtendimentoEspecializado.CodigoCondicaoPcd, "Atendimento PCD");

        OfertaRecurso recursoComOrigemMenor = OfertaRecurso.Criar(IdFixo(1), "ZETA_RECURSO", "Zeta Recurso");
        OfertaRecurso recursoComOrigemMaior = OfertaRecurso.Criar(IdFixo(2), "ALFA_RECURSO", "Alfa Recurso");

        OfertaTipoDeficiencia tipoComOrigemMenor = OfertaTipoDeficiencia.Criar(IdFixo(1), "ZETA_TIPO", "Zeta Tipo");
        OfertaTipoDeficiencia tipoComOrigemMaior = OfertaTipoDeficiencia.Criar(IdFixo(2), "ALFA_TIPO", "Alfa Tipo");

        new[] { condicaoComOrigemMenor, condicaoComOrigemMaior }.OrderBy(static c => c.CondicaoNome, StringComparer.Ordinal).Select(static c => c.CondicaoNome)
            .Should().Equal(["Alfa Nome", "Zeta Nome"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");
        new[] { recursoComOrigemMenor, recursoComOrigemMaior }.OrderBy(static r => r.RecursoNome, StringComparer.Ordinal).Select(static r => r.RecursoNome)
            .Should().Equal(["Alfa Recurso", "Zeta Recurso"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");
        new[] { tipoComOrigemMenor, tipoComOrigemMaior }.OrderBy(static t => t.TipoDeficienciaNome, StringComparer.Ordinal).Select(static t => t.TipoDeficienciaNome)
            .Should().Equal(["Alfa Tipo", "Zeta Tipo"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");

        OfertaAtendimentoEspecializado oferta = OfertaAtendimentoEspecializado.Criar(
            condicoes: [condicaoComOrigemMaior, condicaoComOrigemMenor, condicaoPcd],
            recursos: [recursoComOrigemMaior, recursoComOrigemMenor],
            tiposDeficiencia: [tipoComOrigemMaior, tipoComOrigemMenor]).Value!;

        ProcessoSeletivo processo = Montar(ofertaAtendimento: oferta);

        JsonObject atendimentoJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["atendimento"]!.AsObject();

        atendimentoJson["condicoes"]!.AsArray().Select(static c => c!["condicaoNome"]!.GetValue<string>()).Should().Equal(
            ["Zeta Nome", "Alfa Nome", "Atendimento PCD"],
            "a identidade de origem (CondicaoOrigemId) governa a posição — o nome, em ordem alfabética, apontaria para o oposto");
        atendimentoJson["recursos"]!.AsArray().Select(static r => r!["recursoNome"]!.GetValue<string>()).Should().Equal(
            ["Zeta Recurso", "Alfa Recurso"],
            "a identidade de origem (RecursoOrigemId) governa a posição — o nome, em ordem alfabética, apontaria para o oposto. É uma " +
            "expressão OrderBy própria, independente da de condicoes — regredir só esta não seria pego pelo oráculo de condicoes");
        atendimentoJson["tiposDeficiencia"]!.AsArray().Select(static t => t!["tipoDeficienciaNome"]!.GetValue<string>()).Should().Equal(
            ["Zeta Tipo", "Alfa Tipo"],
            "a identidade de origem (TipoDeficienciaOrigemId) governa a posição — o nome, em ordem alfabética, apontaria para o oposto. É " +
            "uma expressão OrderBy própria, independente das de condicoes/recursos — regredir só esta não seria pego pelos outros oráculos");
    }

    [Fact(DisplayName = "documentosExigidos.exigencias: identidade parcial (fase + tipo) governa sobre o conteúdo — oráculo de sequência exata")]
    public void DocumentosExigidos_Exigencias_IdentidadeParcialGovernaSobreConteudo()
    {
        FaseCronograma fase = FaseMinima();

        DocumentoExigido exigenciaComOrigemMenor = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), fase.Id, IdFixo(1), "TIPO_COMUM", "Zeta Documento", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao);
        DocumentoExigido exigenciaComOrigemMaior = DocumentoExigido.Reidratar(
            Guid.CreateVersion7(), fase.Id, IdFixo(2), "TIPO_COMUM", "Alfa Documento", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao);

        new[] { exigenciaComOrigemMenor, exigenciaComOrigemMaior }.OrderBy(static e => e.TipoDocumentoNome, StringComparer.Ordinal)
            .Select(static e => e.TipoDocumentoNome)
            .Should().Equal(["Alfa Documento", "Zeta Documento"], "pré-condição: ordenar pelo nome (proxy de conteúdo) dá o oposto do oráculo de identidade abaixo");

        NoExigencia raizMenor = NoExigencia.CriarFolha(exigenciaComOrigemMenor, 0).Value!;
        NoExigencia raizMaior = NoExigencia.CriarFolha(exigenciaComOrigemMaior, 1).Value!;

        ProcessoSeletivo processo = Montar(cronogramaFases: [fase], documentosExigidos: [raizMaior, raizMenor]);

        JsonArray exigenciasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["documentosExigidos"]!["exigencias"]!.AsArray();

        exigenciasJson.Should().HaveCount(2, "pré-condição: duas exigências, mesma fase, TipoDocumentoOrigemId distintos");
        exigenciasJson.Select(static e => e!["tipoDocumentoNome"]!.GetValue<string>()).Should().Equal(
            ["Zeta Documento", "Alfa Documento"],
            "a identidade de origem (TipoDocumentoOrigemId, dentro da MESMA fase) governa a posição — o conteúdo, em ordem alfabética, " +
            "apontaria para o oposto");
    }

    [Fact(DisplayName = "documentosExigidos.exigencias: no empate de identidade (mesma fase e tipo), o conteúdo desempata — não o Id")]
    public void DocumentosExigidos_Exigencias_EmpateDeIdentidade_DesempataPeloConteudo()
    {
        FaseCronograma fase = FaseMinima();
        Guid tipoDocumentoOrigemComum = Guid.CreateVersion7();

        DocumentoExigido exigenciaComIdMaior = DocumentoExigido.Reidratar(
            IdFixo(2), fase.Id, tipoDocumentoOrigemComum, "TIPO_COMUM", "Alfa Documento", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao);
        DocumentoExigido exigenciaComIdMenor = DocumentoExigido.Reidratar(
            IdFixo(1), fase.Id, tipoDocumentoOrigemComum, "TIPO_COMUM", "Zeta Documento", "PESSOAL", ProtecaoDoDocumentoDeTeste.Pessoal,
            Aplicabilidade.Geral, obrigatorio: false, consequenciaIndeferimento: null,
            condicoes: [], basesLegais: [], idadeMaximaEmissao: null,
            formatosPermitidos: FormatosPermitidos.Criar(qualquer: true, entradas: null).Value!, tamanhoMaximoBytes: null, finalidade: FinalidadeFormulario.Inscricao);

        new[] { exigenciaComIdMaior, exigenciaComIdMenor }.OrderBy(static e => e.Id).Select(static e => e.TipoDocumentoNome)
            .Should().Equal(["Zeta Documento", "Alfa Documento"], "pré-condição: ordenar por Id dá o oposto do oráculo de conteúdo abaixo");

        NoExigencia raizComIdMaior = NoExigencia.CriarFolha(exigenciaComIdMaior, 0).Value!;
        NoExigencia raizComIdMenor = NoExigencia.CriarFolha(exigenciaComIdMenor, 1).Value!;

        ProcessoSeletivo processo = Montar(cronogramaFases: [fase], documentosExigidos: [raizComIdMaior, raizComIdMenor]);

        JsonArray exigenciasJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["documentosExigidos"]!["exigencias"]!.AsArray();

        exigenciasJson.Select(static e => e!["tipoDocumentoNome"]!.GetValue<string>()).Should().Equal(
            ["Alfa Documento", "Zeta Documento"],
            "duas exigências para a MESMA fase e o MESMO tipo de documento (nada impede isso) empatam na identidade — o conteúdo resolve " +
            "o empate, não o Id, que apontaria para o oposto");
    }

    // ── delegação — grafoDependencia ──

    /// <summary>
    /// <c>B_FATO</c> (Ordem 0, sem pré-condição) produz <c>A_FATO</c> (Ordem 1, pré-condição cita
    /// <c>B_FATO</c>): o produtor tem de vir antes do consumidor na ordem topológica. Alfabeticamente
    /// "A_FATO" precede "B_FATO" — o oposto da dependência real. O manifesto promete os TRÊS arrays
    /// de <c>grafoDependencia</c> (<c>nos</c>, <c>arestas</c>, <c>ordemTopologica</c>) — este teste
    /// afirma a sequência exata dos três para o mesmo cenário, não só de <c>ordemTopologica</c>: um
    /// mutante que reordenasse <c>nos</c> ou <c>arestas</c> sozinho (alfabeticamente, ou pela ordem
    /// física de uma coleção diferente da que o domínio devolve) passaria despercebido se só
    /// <c>ordemTopologica</c> fosse observada.
    /// </summary>
    [Fact(DisplayName = "grafoDependencia.nos/arestas/ordemTopologica: delegação — a ordem produzida pelo domínio é preservada, não reordenada alfabeticamente")]
    public void GrafoDependencia_OrdemTopologica_PreservaAOrdemDoDominio_NaoReordenaAlfabeticamente()
    {
        FatoColetado fatoB = FatoColetado.Criar("B_FATO", 0, "Rótulo B", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;
        FatoColetado fatoA = FatoColetado.Criar("A_FATO", 1, "Rótulo A", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, [
            CondicaoPrecondicaoFato.Criar(0, "B_FATO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!,
        ], classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!;

        ProcessoSeletivo processo = Montar(fatosColetados: [fatoA, fatoB]);

        JsonObject grafoJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))
            ["grafoDependencia"]!.AsObject();

        // Só os nós do caso: o formulário de inscrição traz também os do conjunto básico.
        List<string> ordemTopologica = [.. grafoJson["ordemTopologica"]!.AsArray().Select(static n => n!.GetValue<string>()).Where(DoCaso)];
        ordemTopologica.Should().HaveCountGreaterThanOrEqualTo(2, "pré-condição: o caminho existe e tem nós de A_FATO e de B_FATO");

        List<string> ordenadosAlfabeticamente = [.. ordemTopologica.OrderBy(static r => r, StringComparer.Ordinal)];
        int indiceDeAAlfabetico = ordenadosAlfabeticamente.FindIndex(static r => r.Contains("A_FATO", StringComparison.Ordinal));
        int indiceDeBAlfabetico = ordenadosAlfabeticamente.FindIndex(static r => r.Contains("B_FATO", StringComparison.Ordinal));
        indiceDeAAlfabetico.Should().BeLessThan(indiceDeBAlfabetico,
            "pré-condição: reordenar os MESMOS rótulos alfabeticamente poria A_FATO antes de B_FATO — o oposto da ordem topológica " +
            "que o domínio calcula (produtor antes do consumidor)");

        // Pré-condição sobre `arestas`: as origens das duas arestas de PRODUCAO ("CAMPO/" + o
        // código do fato) ordenadas alfabeticamente começariam por A_FATO — o oposto da ordem
        // canônica abaixo, que segue a ordem de coleta efetiva do nó de origem (B_FATO, Ordem 0,
        // primeiro), não o alfabeto.
        new[] { $"CAMPO/{fatoA.FatoCodigo}", $"CAMPO/{fatoB.FatoCodigo}" }.OrderBy(static r => r, StringComparer.Ordinal).First()
            .Should().Be($"CAMPO/{fatoA.FatoCodigo}", "pré-condição: a origem das duas arestas de PRODUCAO, alfabeticamente, começaria por CAMPO/A_FATO — o oposto do oráculo abaixo");

        ordemTopologica.Should().Equal(
            ["CAMPO/B_FATO", "FATO/B_FATO", "CAMPO/A_FATO", "FATO/A_FATO"],
            "a ordem topológica é CALCULADA por GrafoDependenciaConjunta (Kahn, produtor antes do consumidor) e apenas PRESERVADA " +
            "pela projeção — nunca recalculada nem reordenada alfabeticamente aqui");

        grafoJson["nos"]!.AsArray().Select(static n => n!["idCanonico"]!.GetValue<string>()).Where(DoCaso).Should().Equal(
            ["CAMPO/B_FATO", "FATO/B_FATO", "CAMPO/A_FATO", "FATO/A_FATO"],
            "os nós saem na MESMA ordem canônica que GrafoDependenciaConjunta calcula (ordem de coleta efetiva, depois Classe/Codigo) — " +
            "não a ordem alfabética, que poria CAMPO/A_FATO primeiro");

        grafoJson["arestas"]!.AsArray()
            .Select(static a => (Tipo: a!["tipo"]!.GetValue<string>(), Origem: a["origem"]!.GetValue<string>(), Destino: a["destino"]!.GetValue<string>()))
            .Where(static a => DoCaso(a.Origem))
            .Should().Equal(
                [
                    ("PRODUCAO", "CAMPO/B_FATO", "FATO/B_FATO"),
                    ("PRODUCAO", "CAMPO/A_FATO", "FATO/A_FATO"),
                    ("PRECONDICAO", "FATO/B_FATO", "CAMPO/A_FATO"),
                ],
                "as arestas saem ordenadas por (Tipo, Origem, Destino) canônicos — as duas de PRODUCAO desempatam pela ordem de coleta " +
                "efetiva da origem (B_FATO antes de A_FATO), o oposto da ordem alfabética da pré-condição acima");
    }

    private static bool DoCaso(string idCanonico) =>
        idCanonico.EndsWith("/A_FATO", StringComparison.Ordinal) || idCanonico.EndsWith("/B_FATO", StringComparison.Ordinal);

    // ── camada 1 — Bônus regional (Story #1466) ──

    /// <summary>
    /// Dois municípios com o MESMO código IBGE (dataset de origem corrompido ou desatualizado)
    /// não deixam o array de <c>municipios</c> à mercê da ordem física de retorno do EF/Postgres
    /// para linhas empatadas — Nome/UF desempatam.
    /// </summary>
    [Fact(DisplayName = "bonusRegional.municipios: mesmo código IBGE desempata por Nome e UF — não pela ordem física de entrada")]
    public void BonusRegional_MunicipiosComMesmoCodigoIbge_DesempataPorNomeEUf()
    {
        ConfiguracaoBonusRegional bonus = ConfiguracaoBonusRegional.Criar(
            Regra(RegraBonusCodigo.Multiplicativo, 'd'), 1.20m, null, null, Guid.CreateVersion7(),
            "PORTARIA", "Portaria Teste 1/2026", "Descrição de teste",
            // Inserido fisicamente na ordem ERRADA (Zebu antes de Abaco) — um canonicalizador que
            // preservasse a ordem de ENTRADA para um empate de CodigoIbge reproduziria esta mesma
            // sequência errada.
            [("1504208", "Zebu", "PA"), ("1504208", "Abaco", "PA")]).Value!;

        ProcessoSeletivo processo = Montar();
        processo.DefinirBonusRegional(aplica: true, bonus: bonus, PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        JsonArray municipiosJson = EnvelopeCodecRoundTripTests.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))
            ["bonusRegional"]!["municipios"]!.AsArray();

        municipiosJson.Should().HaveCount(2, "pré-condição: dois municípios sob o mesmo código IBGE");
        municipiosJson.Select(static m => m!["nome"]!.GetValue<string>()).Should().Equal(
            ["Abaco", "Zebu"],
            "com o CodigoIbge empatado, Nome (ordinal) desempata — a ordem física de entrada era [Zebu, Abaco]");
    }
}
