namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading;

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
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.IntegrationTests.Fixtures.Hosting;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CatalogosDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.CheckpointsDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.ItensDoChecklistDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PassosDoEnvelopeFechado;
using static Unifesspa.UniPlus.Selecao.IntegrationTests.Outbox.Cascading.PayloadsDoEnvelopeFechado;

internal static class CatalogosDoEnvelopeFechado
{
    /// <summary>Os ids dos catálogos semeados por <see cref="SemearCatalogosRicosAsync"/>.</summary>
    internal sealed record CatalogosSemeados(
        Guid UnidadeId,
        Guid OfertaCursoId,
        Guid ReferenciaReservaDemograficaId,
        Guid FaseInscricaoId,
        Guid FaseDeResultadoId,
        Guid FaseAvaliacaoId,
        Guid TipoBancaId,
        Guid TipoProcessoId,
        Guid TipoDocumentoId,
        IReadOnlyDictionary<string, Guid> ModalidadeIdPorCodigo);

    /// <summary>
    /// Semeia, direto pelo <c>DbContext</c> de cada módulo (mesmo Postgres compartilhado pela
    /// <see cref="CascadingFixture"/>), os catálogos que os handlers de Seleção resolvem e que não
    /// têm <c>HasData</c>: Unidade administradora (Organização Institucional), Curso + LocalOferta
    /// + OfertaCurso + ReferenciaReservaDemografica (Configuração), TipoBanca (Configuração).
    /// As quatorze <see cref="FaseCanonica"/> passaram a vir semeadas por migration e são apenas
    /// resolvidas aqui pelo código. As 10 <see cref="Modalidade"/> federais já vêm semeadas por migration
    /// (<see cref="ModalidadeSeed"/>) — só referenciadas aqui pelo código.
    /// </summary>
    internal static async Task<CatalogosSemeados> SemearCatalogosRicosAsync(CascadingApiFactory api, string sufixo)
    {
        Guid unidadeId = await SemearUnidadeAdministradoraAsync(api, sufixo);
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();

        return await SemearCatalogosDeConfiguracaoAsync(scope.ServiceProvider.GetRequiredService<UniPlusDbContext>(), unidadeId, sufixo);
    }

    /// <summary>
    /// Unidade administradora sem cidade recusa a criação do processo (issue #1114, CA-02), e este
    /// catálogo é consumido justamente para criar um.
    /// </summary>
    private static async Task<Guid> SemearUnidadeAdministradoraAsync(CascadingApiFactory api, string sufixo)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext org = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        Unidade unidade = Unidade.Criar(
            "Centro de Processos Seletivos E2E",
            new IdentificadoresDaUnidade($"ceps-e2e-{sufixo}", $"CEPS{sufixo}", $"CEPS-{sufixo}", null),
            new CidadeDaUnidade("1504208", "Marabá", "PA"),
            new VigenciaDaUnidade(new DateOnly(2020, 1, 1), null),
            null, TipoUnidade.Centro, true).Value!;
        org.Unidades.Add(unidade);
        await org.SaveChangesAsync().ConfigureAwait(false);
        return unidade.Id;
    }

    private static async Task<CatalogosSemeados> SemearCatalogosDeConfiguracaoAsync(UniPlusDbContext config, Guid unidadeId, string sufixo)
    {
        OfertaCurso oferta = await SemearCursoELocalEPrepararAOfertaAsync(config, unidadeId, sufixo);
        VocabulariosFechados vocabularios = await ObterVocabulariosFechadosAsync(config);
        ReferenciaReservaDemografica referencia = NovaReferenciaReservaDemografica(sufixo);
        TipoDocumento tipoDocumento = NovoTipoDocumentoDeTeste(sufixo);
        config.AddRange(oferta, referencia, tipoDocumento);
        await PersistirEAprovarAsync(config);

        return new CatalogosSemeados(
            unidadeId, oferta.Id, referencia.Id, vocabularios.FaseInscricaoId, vocabularios.FaseDeResultadoId, vocabularios.FaseAvaliacaoId,
            vocabularios.TipoBancaId, await ObterTipoProcessoSisuAsync(config), tipoDocumento.Id, ModalidadesFederaisEAmplaPorCodigo());
    }

    private static async Task PersistirEAprovarAsync(UniPlusDbContext config)
    {
        await config.SaveChangesAsync().ConfigureAwait(false);
        await config.AprovarCatalogoAsync().ConfigureAwait(false);
    }

    private static async Task<OfertaCurso> SemearCursoELocalEPrepararAOfertaAsync(UniPlusDbContext config, Guid unidadeId, string sufixo)
    {
        Curso curso = Curso.Criar($"MED{sufixo}", "Medicina", "Bacharelado", "Graduação", "SAUDE_E_BIOLOGICAS").Value!;
        LocalOferta local = LocalOferta.Criar(
            new(TipoLocalOferta.CampusSede, null, "1504208", "Marabá", "PA", null, null),
            DateTimeOffset.UtcNow).ValorEsperado();
        config.Cursos.Add(curso);
        config.LocaisOferta.Add(local);
        await PersistirEAprovarAsync(config);

        return NovaOfertaDoCurso(curso, local, unidadeId, sufixo);
    }

    private static OfertaCurso NovaOfertaDoCurso(Curso curso, LocalOferta local, Guid unidadeId, string sufixo)
    {
        Result<UnidadeOfertante> unidadeOfertante = UnidadeOfertante.Criar(
            unidadeId, $"CEPS{sufixo}", "Centro de Processos Seletivos E2E", nameof(TipoUnidade.Centro));
        unidadeOfertante.IsSuccess.Should().BeTrue(unidadeOfertante.Error?.Message);
        string codigo = $"OFERTA_E2E_{sufixo}".ToUpperInvariant().Replace("-", "_", StringComparison.Ordinal);
        Result<OfertaCurso> oferta = OfertaCurso.Criar(
            curso.Id, local.Id, unidadeOfertante.Value!, new DeclaracaoDaOfertaDeCurso(codigo, "REGULAR", null, "EXTENSIVO", "REGULAR", ["MATUTINO"]));
        oferta.IsSuccess.Should().BeTrue(oferta.Error?.Message);

        return oferta.Value!;
    }

    /// <summary>
    /// Sufixado em vez de "Censo IBGE 2022" fixo: o índice de censo vivo é único, e este semeador roda
    /// mais de uma vez na mesma coleção sequencial. "Censo " + sufixo (8 hex) cabe no limite de 20
    /// caracteres do value object — "Censo IBGE 2022 " + sufixo estourava.
    /// </summary>
    private static ReferenciaReservaDemografica NovaReferenciaReservaDemografica(string sufixo)
    {
        Result<ReferenciaReservaDemografica> referencia = ReferenciaReservaDemografica.Criar(
            $"Censo {sufixo}", 78.55m, 1.20m, 8.40m, "Lei 12.711/2012 art. 3º");
        referencia.IsSuccess.Should().BeTrue(referencia.Error?.Message);

        return referencia.Value!;
    }

    /// <summary>
    /// O código do tipo de documento não é vocabulário fechado: é chave natural editável, com formato
    /// (<c>^[A-Z][A-Z0-9_]{1,49}$</c>) e unicidade entre vivos. Por isso vai sufixado, e não reaproveitado.
    /// </summary>
    private static TipoDocumento NovoTipoDocumentoDeTeste(string sufixo)
    {
        Result<TipoDocumento> tipoDocumento = TipoDocumento.Criar(
            $"DOC_LIMPEZA_{sufixo.ToUpperInvariant()}", "Documento — Limpeza 1421", descricao: null, categoria: "IDENTIFICACAO", formatosAceitos: null,
            tamanhoMaximoMb: null, tipoEquivalente: null, classificacaoProtecao: ClassificacaoDeProtecao.Pessoal,
            finalidadeTratamento: "Comprovação documental do candidato.", hipoteseLegal: HipoteseLegalTratamento.CumprimentoObrigacaoLegal);
        tipoDocumento.IsSuccess.Should().BeTrue(tipoDocumento.Error?.Message);

        return tipoDocumento.Value!;
    }

    private static Task<Guid> ObterTipoProcessoSisuAsync(UniPlusDbContext config) =>
        config.TiposProcesso.Where(tipo => tipo.Codigo == "SiSU" && tipo.Ativo).Select(tipo => tipo.Id).SingleAsync();

    private static Dictionary<string, Guid> ModalidadesFederaisEAmplaPorCodigo() =>
        ModalidadeSeed.Itens
            .Where(i => i.Codigo == ModalidadesFederaisLei12711.Ac || ModalidadesFederaisLei12711.Codigos.Contains(i.Codigo))
            .ToDictionary(i => i.Codigo, i => i.Id, StringComparer.Ordinal);

    private sealed record VocabulariosFechados(Guid FaseInscricaoId, Guid FaseDeResultadoId, Guid FaseAvaliacaoId, Guid TipoBancaId);

    /// <summary>
    /// As fases canônicas nascem semeadas pela migration, e o vocabulário é fechado: criar aqui colidiria
    /// com o índice único do código vivo. Só a fase de avaliação agrupa etapas pontuadas
    /// (<see cref="FaseCanonicaCatalogo.CodigoAvaliacao"/>), e quem publica o ato de resultado é a fase
    /// de resultado — a conformidade recusa vagas ofertadas sem nenhuma fase que produza resultado.
    /// </summary>
    private static async Task<VocabulariosFechados> ObterVocabulariosFechadosAsync(UniPlusDbContext config)
    {
        List<FaseCanonica> fasesVivas = await config.FasesCanonicas.ToListAsync().ConfigureAwait(false);
        TipoBanca banca = await ObterOuCriarTipoBancaDeAnaliseDocumentalAsync(config);

        return new VocabulariosFechados(
            IdDaFase(fasesVivas, "INSCRICAO"), IdDaFase(fasesVivas, "RESULTADO_FINAL"), IdDaFase(fasesVivas, FaseCanonicaCatalogo.CodigoAvaliacao), banca.Id);
    }

    private static Guid IdDaFase(List<FaseCanonica> fasesVivas, string codigo) =>
        fasesVivas.Single(f => string.Equals(f.Codigo.Valor, codigo, StringComparison.Ordinal)).Id;

    /// <summary>
    /// O código do tipo de banca também é vocabulário fechado, com índice único sobre o vivo: reaproveita
    /// a linha se já existir. O código (value object) não traduz para SQL, então o filtro é em memória.
    /// </summary>
    private static async Task<TipoBanca> ObterOuCriarTipoBancaDeAnaliseDocumentalAsync(UniPlusDbContext config)
    {
        const string Codigo = "BANCA_ANALISE_DOCUMENTAL";
        List<TipoBanca> bancasVivas = await config.TiposBanca.ToListAsync().ConfigureAwait(false);
        TipoBanca? existente = bancasVivas.SingleOrDefault(b => string.Equals(b.Codigo.Valor, Codigo, StringComparison.Ordinal));
        if (existente is not null)
        {
            return existente;
        }

        Result<TipoBanca> criada = TipoBanca.Criar(Codigo, "Banca de análise documental", "RESULTADO_FINAL", null);
        criada.IsSuccess.Should().BeTrue(criada.Error?.Message);
        config.TiposBanca.Add(criada.Value!);
        return criada.Value!;
    }

    /// <summary>
    /// <see cref="TiposDeAtoSeeder"/> só cobre os atos de abertura e de
    /// retificação. Os dois resultados que a fase publica exigem seus próprios tipos
    /// vigentes no catálogo de Publicações — semeados aqui, à parte, sem duplicar o seeder
    /// existente para dois códigos extras.
    /// </summary>
    internal static async Task SemearTipoDeAtoResultadoPreliminarAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db =
            scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        await GarantirTipoDeAtoDeResultadoAsync(db, CodigoAtoResultado, "Resultado da classificação");
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    internal static async Task GarantirTipoDeAtoDeResultadoAsync(UniPlusDbContext db, string codigo, string nome)
    {
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
        {
            return;
        }

        Result<TipoAtoPublicado> tipoResult = TipoAtoPublicado.Criar(
            codigo, nome, congelaConfiguracao: false, unicoPorObjeto: false, efeitoIrreversivel: false, ehResultado: true,
            new DateOnly(2020, 1, 1), vigenciaFim: null, baseLegal: null);
        tipoResult.IsSuccess.Should().BeTrue(tipoResult.Error?.Message);

        await db.Set<TipoAtoPublicado>().AddAsync(tipoResult.Value!).ConfigureAwait(false);
    }

    /// <summary>
    /// Semeia uma Base Legal de Bônus Regional (Configuração, Story #1465) direto no
    /// contexto de persistência — o E2E cascateado testa a drenagem de eventos de Seleção, não o
    /// cadastro de Configuração, então seguir o mesmo atalho de <see cref="SemearDocumentoConfirmadoAsync"/>
    /// evita um HTTP round-trip extra sem cobertura adicional.
    /// </summary>
    internal static async Task<Guid> SemearBaseLegalBonusRegionalAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        BaseLegalBonusRegional baseLegal = BaseLegalBonusRegional.Criar(
            "PORTARIA", "Portaria Unifesspa nº 2514/2023", "Institui inclusão regional",
            [("1504208", "Marabá", "PA")]).Value!;
        await db.BaseLegaisBonus.AddAsync(baseLegal).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return baseLegal.Id;
    }

    internal static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        string hash = string.Concat(Enumerable.Repeat("cd45670189", 7))[..64];
        DocumentoAPublicar documento = DocumentoAPublicar.IniciarPendente(processoId, FinalidadeDoDocumento.DocumentoDoEdital, "edital.pdf", TiposDeMidiaDeDocumento.Pdf, TimeProvider.System, TimeSpan.FromMinutes(15)).Value!;
        documento.Confirmar(2048, hash, TimeProvider.System).IsSuccess.Should().BeTrue();
        await db.DocumentosAPublicar.AddAsync(documento).ConfigureAwait(false);
        await db.SaveChangesAsync().ConfigureAwait(false);
        return documento.Id;
    }

    internal static async Task<Guid> ObterAtoCriadorUnicoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }

    internal static async Task<(string Hash, Guid VersaoId)> LerIdentidadeDaVersaoAsync(CascadingApiFactory api, Guid atoCriadorId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        VersaoConfiguracao versao = await db.VersoesConfiguracao.AsNoTracking()
            .SingleAsync(v => v.AtoCriadorId == atoCriadorId);
        return (versao.HashConfiguracao, versao.Id);
    }

    /// <summary>
    /// Lê as duas versões congeladas depois do fechamento e devolve, pela identidade do
    /// <c>AtoCriadorId</c> (plano §4.5) — nunca "qualquer ato do processo" — o que o fechamento
    /// produziu: o id do ato V2, o ato que ele retifica (deve ser o de V1) e o hash de V1 tal
    /// como está gravado agora (para o checkpoint de imutabilidade).
    /// </summary>
    internal static async Task<(Guid AtoV2Id, Guid AtoV2RetificaId, string HashV1AposFechamento, Guid VersaoV2Id, string HashV2)> LerVersoesAposFechamentoAsync(
        CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();

        versoes.Should().HaveCount(2, "publicação (V1) + retificação (V2)");
        VersaoConfiguracao v1 = versoes[0];
        VersaoConfiguracao v2 = versoes[1];
        v2.AtoCriadorRetificaId.Should().NotBeNull("V2 é uma retificação — ela emenda o ato criador de V1");

        return (v2.AtoCriadorId, v2.AtoCriadorRetificaId!.Value, v1.HashConfiguracao, v2.Id, v2.HashConfiguracao);
    }
}
