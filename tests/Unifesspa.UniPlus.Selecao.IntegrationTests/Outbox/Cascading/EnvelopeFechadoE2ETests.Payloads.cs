namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;

using AwesomeAssertions;

using Domain.Entities;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.ValueObjects;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Authentication;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed partial class EnvelopeFechadoE2ETests
{
    // ══════════════════════════════════════════════════════════════════════════════
    // Payloads das dimensões — um método por dimensão, na ordem topológica do plano §4.2
    // ══════════════════════════════════════════════════════════════════════════════

    private static readonly Guid TipoEtapaProvaObjetivaOrigemId = new("019fee1e-7000-7000-8000-000000000001");
    private static readonly Guid TipoEtapaRedacaoOrigemId = new("019fee1e-7000-7000-8000-000000000002");
    private static readonly Guid TipoEtapaEntrevistaOrigemId = new("019fee1e-7000-7000-8000-000000000003");

    private static object[] EtapasIniciais() =>
    [
        new { nome = "Prova Objetiva", carater = (int)CaraterEtapa.Ambas, tipoEtapaOrigemId = TipoEtapaProvaObjetivaOrigemId, peso = 3.5000m, notaMinima = 40.0000m, notaMaxima = 100m, formaDeAvaliacao = "OBJETIVA", ordem = 1, faseCodigo = "AVALIACAO", produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>() },
        new { nome = "Redação", carater = (int)CaraterEtapa.Classificatoria, tipoEtapaOrigemId = TipoEtapaRedacaoOrigemId, peso = 2.2500m, notaMinima = (decimal?)null, notaMaxima = 100m, formaDeAvaliacao = "OBJETIVA", ordem = 2, faseCodigo = "AVALIACAO", produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>() },
        new { nome = "Entrevista", carater = (int)CaraterEtapa.Eliminatoria, tipoEtapaOrigemId = TipoEtapaEntrevistaOrigemId, peso = (decimal?)null, notaMinima = 60.0000m, notaMaxima = 100m, formaDeAvaliacao = "OBJETIVA", ordem = 3, faseCodigo = "AVALIACAO", produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>() },
    ];

    /// <summary>
    /// A mesma coleção, com o Id de cada etapa ECOADO (reconciliação in-place —
    /// <c>DefinirEtapasCommandHandler.cs:64-77</c>) e o peso da Objetiva alterado — é a
    /// dimensão que muda sob a sessão editorial (passo 18).
    /// </summary>
    private static object[] EtapasComPesoAlterado(Guid objetivaId, Guid redacaoId, Guid entrevistaId) =>
    [
        new { id = objetivaId, nome = "Prova Objetiva", carater = (int)CaraterEtapa.Ambas, tipoEtapaOrigemId = TipoEtapaProvaObjetivaOrigemId, peso = 4.0000m, notaMinima = 40.0000m, notaMaxima = 100m, formaDeAvaliacao = "OBJETIVA", ordem = 1, faseCodigo = "AVALIACAO", produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>() },
        new { id = redacaoId, nome = "Redação", carater = (int)CaraterEtapa.Classificatoria, tipoEtapaOrigemId = TipoEtapaRedacaoOrigemId, peso = 2.2500m, notaMinima = (decimal?)null, notaMaxima = 100m, formaDeAvaliacao = "OBJETIVA", ordem = 2, faseCodigo = "AVALIACAO", produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>() },
        new { id = entrevistaId, nome = "Entrevista", carater = (int)CaraterEtapa.Eliminatoria, tipoEtapaOrigemId = TipoEtapaEntrevistaOrigemId, peso = (decimal?)null, notaMinima = 60.0000m, notaMaxima = 100m, formaDeAvaliacao = "OBJETIVA", ordem = 3, faseCodigo = "AVALIACAO", produtos = Array.Empty<object>(), bancas = Array.Empty<object>(), recursos = Array.Empty<object>() },
    ];

    private static object RegraDeDeliberacaoDaBanca() => new
    {
        modo = "colegiadaComVotacao",
        integrantes = 3,
        suplentes = 0,
        quorum = 2,
        criterio = "maioriaSimples",
        desempate = "votoDaPresidencia",
        encerramento = "presidenciaNoQuorum",
    };

    private static object[] CronogramaDeFases(CatalogosSemeados catalogos) =>
    [
        FaseDeInscricao(catalogos),
        FaseDeResultadoPreliminar(catalogos),
        FaseDeAvaliacao(catalogos),
    ];

    private static object FaseDeInscricao(CatalogosSemeados catalogos) => new
    {
        ordem = 1,
        faseCanonicaId = catalogos.FaseInscricaoId,
        inicio = new DateTimeOffset(2026, 3, 2, 0, 0, 0, TimeSpan.Zero),
        fim = new DateTimeOffset(2026, 3, 20, 23, 59, 59, TimeSpan.Zero),
        produtos = Array.Empty<object>(),
        faseConcluinteCodigo = (string?)null,
        emiteParecerIndividual = false,
        bancasRequeridas = Array.Empty<object>(),
        regraRecurso = (object?)null,
        unidadeResponsavelId = catalogos.UnidadeId,
    };

    // A fase publica preliminar e definitiva da mesma matéria, com o mesmo tipo de ato, e por isso
    // conclui a si mesma — não declara fase concluinte. O tipo é semeado à parte de TiposDeAtoSeeder,
    // que só cobre os dois atos da sessão editorial.
    private static object FaseDeResultadoPreliminar(CatalogosSemeados catalogos) => new
    {
        ordem = 2,
        faseCanonicaId = catalogos.FaseResultadoPreliminarId,
        inicio = new DateTimeOffset(2026, 3, 25, 0, 0, 0, TimeSpan.Zero),
        fim = new DateTimeOffset(2026, 3, 25, 18, 0, 0, TimeSpan.Zero),
        produtos = ProdutosDoResultado(),
        faseConcluinteCodigo = (string?)null,
        emiteParecerIndividual = true,
        bancasRequeridas = new object[] { BancaDoTipo(catalogos.TipoBancaId) },
        regraRecurso = (object?)null,
        unidadeResponsavelId = catalogos.UnidadeId,
    };

    private static object[] ProdutosDoResultado() =>
    [
        new { atoCodigo = CodigoAtoResultado, papel = PapelProdutoFaseCodigo.Preliminar },
        new { atoCodigo = CodigoAtoResultado, papel = PapelProdutoFaseCodigo.Definitivo },
    ];

    private static object BancaDoTipo(Guid tipoBancaId) => new
    {
        tipoBancaId,
        categoriasDocumentoIds = Array.Empty<Guid>(),
        regraDeDeliberacao = RegraDeDeliberacaoDaBanca(),
    };

    // A etapa pontuada exige a fase que agrupa etapas no mesmo cronograma
    // (ProcessoSeletivo.EtapaSemFaseDeAvaliacao). No catálogo fechado, só a avaliação tem esse
    // sinalizador — e ela não publica ato.
    private static object FaseDeAvaliacao(CatalogosSemeados catalogos) => new
    {
        ordem = 3,
        faseCanonicaId = catalogos.FaseAvaliacaoId,
        inicio = new DateTimeOffset(2026, 3, 22, 8, 0, 0, TimeSpan.Zero),
        fim = new DateTimeOffset(2026, 3, 22, 18, 0, 0, TimeSpan.Zero),
        produtos = Array.Empty<object>(),
        faseConcluinteCodigo = (string?)null,
        emiteParecerIndividual = false,
        bancasRequeridas = Array.Empty<object>(),
        regraRecurso = (object?)null,
        unidadeResponsavelId = catalogos.UnidadeId,
    };
}
