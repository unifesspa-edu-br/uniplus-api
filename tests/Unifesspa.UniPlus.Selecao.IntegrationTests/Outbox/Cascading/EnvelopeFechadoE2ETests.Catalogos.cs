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

public sealed partial class EnvelopeFechadoE2ETests
{
    /// <summary>Os ids dos catálogos semeados por <see cref="SemearCatalogosRicosAsync"/>.</summary>
    private sealed record CatalogosSemeados(
        Guid UnidadeId,
        Guid OfertaCursoId,
        Guid ReferenciaReservaDemograficaId,
        Guid FaseInscricaoId,
        Guid FaseResultadoPreliminarId,
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
    /// As dezesseis <see cref="FaseCanonica"/> passaram a vir semeadas por migration e são apenas
    /// resolvidas aqui pelo código. As 10 <see cref="Modalidade"/> federais já vêm semeadas por migration
    /// (<see cref="ModalidadeSeed"/>) — só referenciadas aqui pelo código.
    /// </summary>
    private static async Task<CatalogosSemeados> SemearCatalogosRicosAsync(CascadingApiFactory api, string sufixo)
    {
        Guid unidadeId;
        await using (AsyncServiceScope scopeOrg = api.Services.CreateAsyncScope())
        {
            UniPlusDbContext org = scopeOrg.ServiceProvider.GetRequiredService<UniPlusDbContext>();
            Unidade unidade = Unidade.Criar(
                "Centro de Processos Seletivos E2E",
                new IdentificadoresDaUnidade($"ceps-e2e-{sufixo}", $"CEPS{sufixo}", $"CEPS-{sufixo}", null),
                // Issue #1114 (CA-02): Unidade administradora sem cidade recusa a criação do
                // processo — este catálogo é consumido justamente para criar um.
                new CidadeDaUnidade("1504208", "Marabá", "PA"),
                new VigenciaDaUnidade(new DateOnly(2020, 1, 1), null),
                null,
                TipoUnidade.Centro,
                true).Value!;
            org.Unidades.Add(unidade);
            await org.SaveChangesAsync().ConfigureAwait(false);
            unidadeId = unidade.Id;
        }

        Guid ofertaCursoId;
        Guid referenciaDemograficaId;
        Guid faseInscricaoId;
        Guid faseResultadoPreliminarId;
        Guid faseAvaliacaoId;
        Guid tipoBancaId;
        Guid tipoProcessoId;
        Guid tipoDocumentoId;
        await using (AsyncServiceScope scopeConfig = api.Services.CreateAsyncScope())
        {
            UniPlusDbContext config = scopeConfig.ServiceProvider.GetRequiredService<UniPlusDbContext>();

            Curso curso = Curso.Criar($"MED{sufixo}", "Medicina", "Bacharelado", "Graduação", "SAUDE_E_BIOLOGICAS").Value!;
            LocalOferta local = LocalOferta.Criar(
                TipoLocalOferta.CampusSede,
                campusResponsavelId: null,
                cidadeCodigoIbge: "1504208",
                cidadeNome: "Marabá",
                cidadeUf: "PA",
                cidadeOrigem: Unifesspa.UniPlus.Kernel.Domain.Cidades.ReferenciaCidadeGeo.OrigemGeoApi,
                cidadeDisplayAtualizadoEm: DateTimeOffset.UtcNow,
                endereco: null,
                codigoEmec: null).Value!;
            config.Cursos.Add(curso);
            config.LocaisOferta.Add(local);
            await config.SaveChangesAsync().ConfigureAwait(false);
            await config.AprovarCatalogoAsync().ConfigureAwait(false);

            Result<UnidadeOfertante> unidadeOfertanteResult = UnidadeOfertante.Criar(
                unidadeId, $"CEPS{sufixo}", "Centro de Processos Seletivos E2E", nameof(TipoUnidade.Centro));
            unidadeOfertanteResult.IsSuccess.Should().BeTrue(unidadeOfertanteResult.Error?.Message);
            Result<OfertaCurso> ofertaResult = OfertaCurso.Criar(
                curso.Id, local.Id, unidadeOfertanteResult.Value!,
                programaDeOferta: "REGULAR", formatoPedagogico: null,
                regimeDeFuncionamento: "EXTENSIVO", regimeDeTurno: "REGULAR", turnos: ["MATUTINO"],
                eMecCodigo: null, codigoSga: null, vagasAnuaisAutorizadas: null,
                baseLegal: null, atoAutorizacaoMec: null);
            ofertaResult.IsSuccess.Should().BeTrue(ofertaResult.Error?.Message);
            OfertaCurso oferta = ofertaResult.Value!;

            // Sufixado (não é mais "Censo IBGE 2022" fixo): ix_referencia_reserva_demografica_censo_vivo
            // é único por censo — LimpezaRegrasDistribuicaoMigrationTests chama este mesmo
            // semeador na mesma coleção sequencial (CascadingCollection), e um literal fixo
            // colidiria na segunda chamada. "Censo " + sufixo (8 hex) cabe no limite de 20
            // caracteres do value object — "Censo IBGE 2022 " + sufixo estourava.
            Result<ReferenciaReservaDemografica> referenciaResult = ReferenciaReservaDemografica.Criar(
                $"Censo {sufixo}", 78.55m, 1.20m, 8.40m, "Lei 12.711/2012 art. 3º");
            referenciaResult.IsSuccess.Should().BeTrue(referenciaResult.Error?.Message);
            ReferenciaReservaDemografica referencia = referenciaResult.Value!;

            // As fases canonicas nascem semeadas pela migration — o vocabulario e fechado,
            // entao criar aqui colidiria com o indice unico do codigo vivo. O teste passa a
            // usar as linhas que o proprio sistema garante existirem.
            List<FaseCanonica> fasesVivas = await config.FasesCanonicas.ToListAsync().ConfigureAwait(false);
            FaseCanonica faseInscricao = fasesVivas.Single(f =>
                string.Equals(f.Codigo.Valor, "INSCRICAO", StringComparison.Ordinal));

            // "Apenas a fase de avaliação agrupa etapas pontuadas" (FaseCanonica.Criar) — o
            // único código do catálogo fechado com essa permissão é AVALIACAO
            // (FaseCanonicaCatalogo.CodigoAvaliacao).
            // A fase precisa produzir resultado — o cronograma declara o ato dela, e a
            // conformidade recusa vagas ofertadas sem nenhuma fase que produza. A avaliacao
            // agrupa etapas, mas nao publica ato: quem publica e o resultado.
            FaseCanonica faseResultadoPreliminar = fasesVivas.Single(f =>
                string.Equals(f.Codigo.Valor, "RESULTADO_PRELIMINAR", StringComparison.Ordinal));

            FaseCanonica faseAvaliacao = fasesVivas.Single(f =>
                string.Equals(f.Codigo.Valor, FaseCanonicaCatalogo.CodigoAvaliacao, StringComparison.Ordinal));

            const string CodigoBancaAnaliseDocumental = "BANCA_ANALISE_DOCUMENTAL";

            // TipoBanca.Codigo também é vocabulário fechado (ck_tipo_banca_codigo_canonico
            // admite só 6 valores) com índice único sobre o vivo — mesmo motivo de
            // FasesCanonicas acima: uma segunda chamada deste semeador na mesma coleção
            // colidiria se criasse de novo. Idempotente: reaproveita a linha se já existir.
            // CodigoBanca (value object) não traduz para SQL em predicado de LINQ — mesma
            // solução de fasesVivas acima: materializa e filtra em memória.
            List<TipoBanca> bancasVivas = await config.TiposBanca.ToListAsync().ConfigureAwait(false);
            TipoBanca? bancaExistente = bancasVivas.SingleOrDefault(b =>
                string.Equals(b.Codigo.Valor, CodigoBancaAnaliseDocumental, StringComparison.Ordinal));
            TipoBanca banca;
            if (bancaExistente is not null)
            {
                banca = bancaExistente;
            }
            else
            {
                Result<TipoBanca> bancaResult = TipoBanca.Criar(
                    CodigoBancaAnaliseDocumental, "Banca de análise documental", "RESULTADO_PRELIMINAR", null);
                bancaResult.IsSuccess.Should().BeTrue(bancaResult.Error?.Message);
                banca = bancaResult.Value!;
                config.TiposBanca.Add(banca);
            }

            // Sufixado (não é reaproveitado como TipoBanca acima): o Codigo de TipoDocumento
            // não é vocabulário fechado — é chave natural editável, só exige formato
            // (^[A-Z][A-Z0-9_]{1,49}$) e unicidade entre vivos. Um literal fixo colidiria se
            // este semeador rodasse mais de uma vez na mesma coleção sequencial.
            Result<TipoDocumento> tipoDocumentoResult = TipoDocumento.Criar(
                $"DOC_LIMPEZA_{sufixo.ToUpperInvariant()}", "Documento — Limpeza 1421",
                descricao: null, categoria: "IDENTIFICACAO", formatosAceitos: null,
                tamanhoMaximoMb: null, tipoEquivalente: null, classificacaoProtecao: ClassificacaoDeProtecao.Pessoal, finalidadeTratamento: "Comprovação documental do candidato.", hipoteseLegal: HipoteseLegalTratamento.CumprimentoObrigacaoLegal);
            tipoDocumentoResult.IsSuccess.Should().BeTrue(tipoDocumentoResult.Error?.Message);
            TipoDocumento tipoDocumento = tipoDocumentoResult.Value!;

            config.OfertasCurso.Add(oferta);
            config.ReferenciasReservaDemografica.Add(referencia);
            config.TiposDocumento.Add(tipoDocumento);
            await config.SaveChangesAsync().ConfigureAwait(false);
            await config.AprovarCatalogoAsync().ConfigureAwait(false);

            ofertaCursoId = oferta.Id;
            referenciaDemograficaId = referencia.Id;
            faseInscricaoId = faseInscricao.Id;
            faseResultadoPreliminarId = faseResultadoPreliminar.Id;
            faseAvaliacaoId = faseAvaliacao.Id;
            tipoBancaId = banca.Id;
            tipoDocumentoId = tipoDocumento.Id;
            tipoProcessoId = await config.TiposProcesso
                .Where(tipo => tipo.Codigo == "SiSU" && tipo.Ativo)
                .Select(tipo => tipo.Id)
                .SingleAsync().ConfigureAwait(false);
        }

        Dictionary<string, Guid> modalidadeIdPorCodigo = ModalidadeSeed.Itens
            .Where(i => i.Codigo == ModalidadesFederaisLei12711.Ac || ModalidadesFederaisLei12711.Codigos.Contains(i.Codigo))
            .ToDictionary(i => i.Codigo, i => i.Id, StringComparer.Ordinal);

        return new CatalogosSemeados(
            unidadeId, ofertaCursoId, referenciaDemograficaId, faseInscricaoId, faseResultadoPreliminarId, faseAvaliacaoId, tipoBancaId, tipoProcessoId,
            tipoDocumentoId, modalidadeIdPorCodigo);
    }

    /// <summary>
    /// <see cref="TiposDeAtoSeeder"/> só cobre os dois atos da sessão editorial (abertura e
    /// retificação). Os dois resultados que a fase publica exigem seus próprios tipos
    /// vigentes no catálogo de Publicações — semeados aqui, à parte, sem duplicar o seeder
    /// existente para dois códigos extras.
    /// </summary>
    private static async Task SemearTipoDeAtoResultadoPreliminarAsync(CascadingApiFactory api)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db =
            scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();

        await GarantirTipoDeAtoDeResultadoAsync(db, CodigoAtoResultado, "Resultado da classificação");
        await db.SaveChangesAsync().ConfigureAwait(false);
    }

    private static async Task GarantirTipoDeAtoDeResultadoAsync(
        UniPlusDbContext db,
        string codigo,
        string nome)
    {
        if (await db.Set<TipoAtoPublicado>().AnyAsync(t => t.Codigo == codigo))
        {
            return;
        }

        Result<TipoAtoPublicado> tipoResult = TipoAtoPublicado.Criar(
            codigo,
            nome,
            congelaConfiguracao: false,
            unicoPorObjeto: false,
            efeitoIrreversivel: false,
            ehResultado: true,
            new DateOnly(2020, 1, 1),
            vigenciaFim: null,
            baseLegal: null);
        tipoResult.IsSuccess.Should().BeTrue(tipoResult.Error?.Message);

        await db.Set<TipoAtoPublicado>().AddAsync(tipoResult.Value!).ConfigureAwait(false);
    }

    /// <summary>
    /// Semeia uma Base Legal de Bônus Regional (Configuração, Story #1465) direto no
    /// contexto de persistência — o E2E cascateado testa a drenagem de eventos de Seleção, não o
    /// cadastro de Configuração, então seguir o mesmo atalho de <see cref="SemearDocumentoConfirmadoAsync"/>
    /// evita um HTTP round-trip extra sem cobertura adicional.
    /// </summary>
    private static async Task<Guid> SemearBaseLegalBonusRegionalAsync(CascadingApiFactory api)
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

    private static async Task<Guid> SemearDocumentoConfirmadoAsync(CascadingApiFactory api, Guid processoId)
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

    private static async Task<Guid> ObterAtoCriadorUnicoAsync(CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        return await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .Select(v => v.AtoCriadorId)
            .SingleAsync();
    }

    private static async Task<(string Hash, Guid VersaoId)> LerIdentidadeDaVersaoAsync(CascadingApiFactory api, Guid atoCriadorId)
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
    private static async Task<(Guid AtoV2Id, Guid AtoV2RetificaId, string HashV1AposFechamento, Guid VersaoV2Id, string HashV2)> LerVersoesAposFechamentoAsync(
        CascadingApiFactory api, Guid processoId)
    {
        await using AsyncServiceScope scope = api.Services.CreateAsyncScope();
        UniPlusDbContext db = scope.ServiceProvider.GetRequiredService<UniPlusDbContext>();
        List<VersaoConfiguracao> versoes = await db.VersoesConfiguracao.AsNoTracking()
            .Where(v => v.ProcessoSeletivoId == processoId)
            .OrderBy(v => v.NumeroVersao)
            .ToListAsync();

        versoes.Should().HaveCount(2, "publicação (V1) + fechamento da sessão (V2)");
        VersaoConfiguracao v1 = versoes[0];
        VersaoConfiguracao v2 = versoes[1];
        v2.AtoCriadorRetificaId.Should().NotBeNull("V2 é uma retificação — ela emenda o ato criador de V1");

        return (v2.AtoCriadorId, v2.AtoCriadorRetificaId!.Value, v1.HashConfiguracao, v2.Id, v2.HashConfiguracao);
    }
}
