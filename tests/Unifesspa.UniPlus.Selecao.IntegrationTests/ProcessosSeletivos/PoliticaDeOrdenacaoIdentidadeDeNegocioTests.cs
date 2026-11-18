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

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaIdentidadeDeNegocio;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ApoioDaPoliticaDeOrdenacao;

public sealed class PoliticaDeOrdenacaoIdentidadeDeNegocioTests
{
    [Fact(DisplayName = "documentosExigidos.metadadosFatos[].valoresDominioDeclarados: Ordem governa sobre o Codigo — oráculo de sequência exata")]
    public void MetadadosFatos_ValoresDominioDeclarados_OrdemGovernaSobreCodigo()
    {
        ValorDominioDeclaradoCongelado valorZeta = new("ZETA_VALOR", "Zeta descricao", 0);
        ValorDominioDeclaradoCongelado valorAlfa = new("ALFA_VALOR", "Alfa descricao", 1);
        ConferirQueOCodigoOrdenaAoContrarioDaOrdem(valorZeta, valorAlfa);
        ConferirQueOsBytesCanonicosOrdenamAoContrarioDaOrdem(valorZeta, valorAlfa);
        ProcessoSeletivo processo = Montar();

        JsonArray declaradosJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo, MetadadosDaCorRaca(valorAlfa, valorZeta))))
            ["documentosExigidos"]!["metadadosFatos"]![0]!["valoresDominioDeclarados"]!.AsArray();

        declaradosJson.Select(static v => v!["valorCodigo"]!.GetValue<string>()).Should().Equal(
            ["ZETA_VALOR", "ALFA_VALOR"],
            "Ordem (0 para ZETA_VALOR, 1 para ALFA_VALOR) governa a posição — o Codigo, em ordem alfabética, apontaria para o oposto");
    }

    // ── camada 2 — identidade de negócio ──

    [Fact(DisplayName = "atendimento.condicoes/recursos/tiposDeficiencia: identidade de origem governa sobre o conteúdo — oráculo de sequência exata para os três arrays")]
    public void AtendimentoEspecializado_Condicoes_IdentidadeDeOrigemGovernaSobreConteudo()
    {
        ProcessoSeletivo processo = Montar(atendimentoEspecializado: AtendimentoComIdentidadeDeOrigemInvertida());

        JsonObject atendimentoJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["atendimento"]!.AsObject();

        ConferirONomeNaOrdemDaOrigem(atendimentoJson, "condicoes", "condicaoNome", "CondicaoOrigemId", ["Zeta Nome", "Alfa Nome", "Atendimento PCD"]);
        ConferirONomeNaOrdemDaOrigem(atendimentoJson, "recursos", "recursoNome", "RecursoOrigemId", ["Zeta Recurso", "Alfa Recurso"]);
        ConferirONomeNaOrdemDaOrigem(atendimentoJson, "tiposDeficiencia", "tipoDeficienciaNome", "TipoDeficienciaOrigemId", ["Zeta Tipo", "Alfa Tipo"]);
    }

    [Fact(DisplayName = "documentosExigidos.exigencias: identidade parcial (fase + tipo) governa sobre o conteúdo — oráculo de sequência exata")]
    public void DocumentosExigidos_Exigencias_IdentidadeParcialGovernaSobreConteudo()
    {
        FaseCronograma fase = FaseMinima();
        DocumentoExigido exigenciaComOrigemMenor = ExigenciaDeTeste(Guid.CreateVersion7(), fase.Id, IdFixo(1), "Zeta Documento");
        DocumentoExigido exigenciaComOrigemMaior = ExigenciaDeTeste(Guid.CreateVersion7(), fase.Id, IdFixo(2), "Alfa Documento");
        ConferirQueONomeOrdenaAoContrarioDaOrigem(static e => e.TipoDocumentoNome, exigenciaComOrigemMenor, exigenciaComOrigemMaior);

        IReadOnlyList<string> nomes = NomesDasExigenciasNoEnvelope(
            fase, NoExigencia.CriarFolha(exigenciaComOrigemMaior, 1).Value!, NoExigencia.CriarFolha(exigenciaComOrigemMenor, 0).Value!);

        nomes.Should().HaveCount(2, "pré-condição: duas exigências, mesma fase, TipoDocumentoOrigemId distintos");
        nomes.Should().Equal(
            ["Zeta Documento", "Alfa Documento"],
            "a identidade de origem (TipoDocumentoOrigemId, dentro da MESMA fase) governa a posição — o conteúdo, em ordem alfabética, " +
            "apontaria para o oposto");
    }

    [Fact(DisplayName = "documentosExigidos.exigencias: no empate de identidade (mesma fase e tipo), o conteúdo desempata — não o Id")]
    public void DocumentosExigidos_Exigencias_EmpateDeIdentidade_DesempataPeloConteudo()
    {
        FaseCronograma fase = FaseMinima();
        Guid tipoDocumentoOrigemComum = Guid.CreateVersion7();
        DocumentoExigido exigenciaComIdMaior = ExigenciaDeTeste(IdFixo(2), fase.Id, tipoDocumentoOrigemComum, "Alfa Documento");
        DocumentoExigido exigenciaComIdMenor = ExigenciaDeTeste(IdFixo(1), fase.Id, tipoDocumentoOrigemComum, "Zeta Documento");
        new[] { exigenciaComIdMaior, exigenciaComIdMenor }.OrderBy(static e => e.Id).Select(static e => e.TipoDocumentoNome)
            .Should().Equal(["Zeta Documento", "Alfa Documento"], "pré-condição: ordenar por Id dá o oposto do oráculo de conteúdo abaixo");

        IReadOnlyList<string> nomes = NomesDasExigenciasNoEnvelope(
            fase, NoExigencia.CriarFolha(exigenciaComIdMaior, 0).Value!, NoExigencia.CriarFolha(exigenciaComIdMenor, 1).Value!);

        nomes.Should().Equal(
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
        FatoColetado fatoB = FatoBooleanoPessoal("B_FATO", 0, null);
        FatoColetado fatoA = FatoBooleanoPessoal("A_FATO", 1, [CondicaoPrecondicaoFato.Criar(0, "B_FATO", Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!]);
        ProcessoSeletivo processo = Montar(fatosColetados: [fatoA, fatoB]);

        JsonObject grafoJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))["grafoDependencia"]!.AsObject();

        ConferirAOrdemTopologica(grafoJson);
        ConferirOsNosDoGrafo(grafoJson);
        ConferirAsArestasDoGrafo(grafoJson);
    }

    // ── camada 1 — Bônus regional (Story #1466) ──

    /// <summary>
    /// Dois municípios com o MESMO código IBGE (dataset de origem corrompido ou desatualizado)
    /// não deixam o array de <c>municipios</c> à mercê da ordem física de retorno do EF/Postgres
    /// para linhas empatadas — Nome/UF desempatam.
    /// </summary>
    [Fact(DisplayName = "bonusRegional.municipios: mesmo código IBGE desempata por Nome e UF — não pela ordem física de entrada")]
    public void BonusRegional_MunicipiosComMesmoCodigoIbge_DesempataPorNomeEUf()
    {
        ProcessoSeletivo processo = Montar();
        processo.DefinirBonusRegional(aplica: true, bonus: BonusComMunicipiosDeMesmoCodigoIbgeForaDeOrdem()).IsSuccess.Should().BeTrue();

        JsonArray municipiosJson = ApoioDoEnvelopeCodec.Envelope(Canonicalizador.Canonicalizar(Entrada(processo)))
            ["bonusRegional"]!["municipios"]!.AsArray();

        municipiosJson.Should().HaveCount(2, "pré-condição: dois municípios sob o mesmo código IBGE");
        municipiosJson.Select(static m => m!["nome"]!.GetValue<string>()).Should().Equal(
            ["Abaco", "Zebu"],
            "com o CodigoIbge empatado, Nome (ordinal) desempata — a ordem física de entrada era [Zebu, Abaco]");
    }
}
