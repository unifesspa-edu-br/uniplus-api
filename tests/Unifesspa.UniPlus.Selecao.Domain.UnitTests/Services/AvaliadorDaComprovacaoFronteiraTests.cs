namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static ArvoreDeTeste;

/// <summary>
/// Consequência por nó e fronteira ativa de emissão do <see cref="AvaliadorDaComprovacao"/>.
/// </summary>
public sealed class AvaliadorDaComprovacaoFronteiraTests
{
    // ── Consequência por nó + fronteira ativa ────────────────────────────────────────────

    [Fact(DisplayName = "Ramo satisfeito suprime pendências e consequências abaixo")]
    public void Fronteira_RamoSatisfeito_SuprimeSubarvore()
    {
        DocumentoExigido rg = DocumentoGeral("ELIMINA");
        DocumentoExigido cpf = DocumentoGeral("ELIMINA");
        DocumentoExigido cin = DocumentoGeral("RECLASSIFICA_AC");
        NoExigencia grupoE = NoExigencia.CriarGrupo(
            TipoNo.GrupoE, 0, null, null, [], [NoExigencia.CriarFolha(rg, 0).Value!, NoExigencia.CriarFolha(cpf, 1).Value!]).Value!;
        NoExigencia noCin = NoExigencia.CriarFolha(cin, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoOu, 0, null, null, [], [grupoE, noCin]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(cin));

        resultado.Value!.ConsequenciasVigentes.Should().BeEmpty();
        resultado.Value!.PendenciasDeOrientacao.Should().BeEmpty();
    }

    [Fact(DisplayName = "OU opaco pendente emite a própria consequência, não uma derivação das folhas")]
    public void Fronteira_OuOpacoPendente_EmiteAPropria()
    {
        DocumentoExigido a = DocumentoGeral("ELIMINA");
        DocumentoExigido b = DocumentoGeral("PENDENCIA_REENVIO");
        NoExigencia raiz = NoExigencia.CriarGrupo(
            TipoNo.GrupoOu, 0, 1, "RECLASSIFICA_AC", [BaseLegalResolvida()],
            [NoExigencia.CriarFolha(a, 0).Value!, NoExigencia.CriarFolha(b, 1).Value!]).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz));

        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.NoExigenciaId == raiz.Id && c.TipoOrigem == TipoNo.GrupoOu && c.Consequencia == "RECLASSIFICA_AC");
        resultado.Value!.PendenciasDeOrientacao.Should().HaveCount(2);
    }

    [Fact(DisplayName = "E transparente emite só os filhos não satisfeitos, incluindo INDETERMINADO — sem dupla emissão")]
    public void Fronteira_ETransparente_EmiteSoFilhosNaoSatisfeitos()
    {
        DocumentoExigido docA = DocumentoCondicional("FATO_X", "ELIMINA");
        DocumentoExigido docB = DocumentoGeral("RECLASSIFICA_AC");
        NoExigencia noA = NoExigencia.CriarFolha(docA, 0).Value!;
        NoExigencia noB = NoExigencia.CriarFolha(docB, 1).Value!;
        NoExigencia raiz = NoExigencia.CriarGrupo(TipoNo.GrupoE, 0, null, null, [], [noA, noB]).Value!;

        // docB satisfeito (apresentado); docA indeterminado (FATO_X não resolvido).
        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz), apresentacoes: Apresenta(docB));

        resultado.Value!.SituacoesPorRegra[raiz.Id].Should().Be(SituacaoDaComprovacao.Indeterminado);
        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.NoExigenciaId == noA.Id && c.TipoOrigem == TipoNo.Folha && c.Consequencia == "ELIMINA");
    }

    [Fact(DisplayName = "Folha solteira (sem grupo) pendente emite a própria consequência — caso degenerado")]
    public void Fronteira_FolhaSolteiraPendente_EmiteAPropria()
    {
        DocumentoExigido documento = DocumentoGeral("ELIMINA");
        NoExigencia raiz = NoExigencia.CriarFolha(documento, 0).Value!;

        Result<ResultadoDaComprovacao> resultado = Resolver(Arvore(raiz));

        resultado.Value!.ConsequenciasVigentes.Should().ContainSingle(
            c => c.NoExigenciaId == raiz.Id && c.TipoOrigem == TipoNo.Folha && c.Consequencia == "ELIMINA");
        resultado.Value!.StatusPorExigencia[documento.Id].Should().Be(StatusResolucaoExigencia.Pendente);
    }
}
