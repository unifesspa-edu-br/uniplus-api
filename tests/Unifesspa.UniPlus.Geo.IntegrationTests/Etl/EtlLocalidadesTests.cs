namespace Unifesspa.UniPlus.Geo.IntegrationTests.Etl;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

using Npgsql;

using Unifesspa.UniPlus.Geo.Domain.Entities;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Bulk;
using Unifesspa.UniPlus.Geo.Infrastructure.Persistence.Etl.Fonte;
using Unifesspa.UniPlus.Geo.IntegrationTests.Infrastructure;

/// <summary>
/// Carga das folhas (Story #673) sobre Postgres+PostGIS real: Distrito/Bairro (+faixas)
/// por upsert e Logradouro/Complemento via COPY binário em lote. Cobre os critérios CA-01
/// a CA-08 — COPY/lote, índices pós-carga via CONCURRENTLY fora de transação, CEP
/// compartilhado, órfão descartado, chave composta de distrito/bairro e idempotência da
/// recarga. Cada teste pré-carrega o topo (País/Estado/Cidade) via o importador da #672.
/// </summary>
[Collection(GeoPostgisCollection.Name)]
public sealed class EtlLocalidadesTests
{
    private const string CodigoMaraba = "1500402";
    private const string CodigoSaoPaulo = "3550308";
    private const int IdCidadeMaraba = 1;
    private const int IdCidadeSaoPaulo = 2;
    private const int IdCidadeInexistente = 999;

    // Espelha a migration ReconciliaNomeNormalizadoLogradouroTextoCompleto (#707): dropa o
    // índice único, recalcula nome_normalizado para o texto completo sem acento, deduplica
    // colisões reais (vigente DESC, versao_dataset DESC, id DESC) e recria o índice. Mantido
    // idêntico ao SQL da migration.
    private const string BackfillSql = """
        DROP INDEX IF EXISTS ix_logradouro_natural;
        UPDATE logradouro SET nome_normalizado = lower(translate(coalesce(nome_completo, nome), 'àáâãäçèéêëìíîïñòóôõöùúûüÀÁÂÃÄÇÈÉÊËÌÍÎÏÑÒÓÔÕÖÙÚÛÜ', 'aaaaaceeeeiiiinooooouuuuAAAAACEEEEIIIINOOOOOUUUU'));
        DELETE FROM logradouro l USING (SELECT id, row_number() OVER (PARTITION BY cep, nome_normalizado, cidade_id ORDER BY vigente DESC, versao_dataset DESC, id DESC) AS rn FROM logradouro) ranked WHERE l.id = ranked.id AND ranked.rn > 1;
        CREATE UNIQUE INDEX ix_logradouro_natural ON logradouro (cep, nome_normalizado, cidade_id);
        """;

    private readonly GeoPostgisFixture _fixture;

    public EtlLocalidadesTests(GeoPostgisFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "CA-01/02: COPY popula logradouros com FK Guid resolvida, ativo bool e coordenada 4326")]
    public async Task CargaInicial_PopulaLogradouros()
    {
        await PrepararTopoAsync(FonteCompleta());
        await ExecutarLocalidadesAsync(FonteCompleta(), ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Guid marabaId = (await leitura.Cidades.SingleAsync(c => c.CodigoIbge == CodigoMaraba)).Id;

        (await leitura.Distritos.CountAsync()).Should().Be(2);
        (await leitura.Bairros.CountAsync()).Should().Be(2);
        (await leitura.DistritoFaixasCep.CountAsync()).Should().Be(1);
        (await leitura.BairroFaixasCep.CountAsync()).Should().Be(1);
        (await leitura.CepGrandesUsuarios.CountAsync()).Should().Be(1);
        (await leitura.LogradouroComplementos.CountAsync()).Should().Be(2);

        Logradouro ruaA = await leitura.Logradouros.SingleAsync(l => l.Cep == "68500000" && l.Nome == "Rua A");
        ruaA.CidadeId.Should().Be(marabaId);
        ruaA.DistritoId.Should().NotBeNull(); // distrito_id 10 resolvido para Guid
        ruaA.BairroId.Should().NotBeNull();
        ruaA.Ativo.Should().BeTrue();          // cep_ativo 'S'
        ruaA.Coordenada.Should().NotBeNull();
        ruaA.Coordenada!.SRID.Should().Be(4326);

        Logradouro ruaB = await leitura.Logradouros.SingleAsync(l => l.Nome == "Rua B");
        ruaB.DistritoId.Should().BeNull();     // distrito_id nulo aceito (FK opcional)
        ruaB.Ativo.Should().BeFalse();         // cep_ativo 'N'
    }

    [Fact(DisplayName = "CA-03/04: na carga inicial os índices trigram/GIST são recriados via CONCURRENTLY (fora de transação) e ficam válidos")]
    public async Task CargaInicial_RecriaIndicesPesadosForaDeTransacao()
    {
        await PrepararTopoAsync(FonteCompleta());

        // A própria conclusão sem erro prova que CREATE INDEX CONCURRENTLY rodou fora de
        // transação — dentro de uma, o Postgres lançaria 25001.
        await ExecutarLocalidadesAsync(FonteCompleta(), ModoCarga.Inicial);

        (await IndiceValidoAsync("ix_logradouro_nome_trgm")).Should().BeTrue();
        (await IndiceValidoAsync("ix_logradouro_coordenada")).Should().BeTrue();
    }

    [Fact(DisplayName = "CA-05: CEP geral é compartilhado por vários logradouros (cep indexado, não único)")]
    public async Task CepCompartilhado_Coexiste()
    {
        await PrepararTopoAsync(FonteCompleta());
        FonteEmMemoria fonte = FonteCompleta();
        fonte.Logradouros.Add(DadosDne.Logradouro("68500000", "Rua C", IdCidadeMaraba)); // mesmo CEP de "Rua A"

        await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Logradouros.CountAsync(l => l.Cep == "68500000")).Should().Be(2);
    }

    [Fact(DisplayName = "#707: backfill reconcilia chave legada (name-only) para texto completo; recarga de mesma versão não duplica")]
    public async Task Backfill_ReconciliaChaveLegada_RecargaMesmaVersaoNaoDuplica()
    {
        await PrepararTopoAsync(FonteCompleta());

        FonteEmMemoria fonte = FonteCompleta();
        fonte.Logradouros.Clear();
        // Tipo e nome em colunas distintas: o name-only ("se") difere do texto completo
        // ("praca da se"), reproduzindo o que distingue a chave antiga da nova (#707).
        fonte.Logradouros.Add(DadosDne.Logradouro(
            "68500000", "Sé", IdCidadeMaraba, tipo: "Praça",
            nomeCompleto: "Praça da Sé", logradouroSemAcento: "praca da se"));

        await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        // Rebaixa nome_normalizado para a forma name-only que a versão anterior do ETL
        // gravava a partir de nome_logradouro_sem_acento ("Sé" → "se") — o estado legado
        // que a migration precisa reconciliar.
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            await ctx.Database.ExecuteSqlRawAsync("UPDATE logradouro SET nome_normalizado = 'se'");
            (await ctx.Logradouros.SingleAsync()).NomeNormalizado.Should().Be("se");
        }

        // Aplica o backfill da migration: reconcilia para o texto completo sem acento.
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(BackfillSql);
            (await ctx.Logradouros.SingleAsync()).NomeNormalizado.Should().Be("praca da se");
        }

        // Recarga da MESMA versão: agora o ON CONFLICT casa a chave reconciliada e atualiza
        // a linha em vez de inserir uma duplicata vigente.
        await ExecutarLocalidadesAsync(fonte, ModoCarga.Recarga);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Logradouros.CountAsync(l => l.Cep == "68500000")).Should().Be(1);
        (await leitura.Logradouros.SingleAsync(l => l.Cep == "68500000")).NomeNormalizado.Should().Be("praca da se");
    }

    [Fact(DisplayName = "#707: backfill não aborta com rotação de chave (texto completo de um == name-only de outro no mesmo CEP)")]
    public async Task Backfill_RotacaoDeChave_NaoAbortaPelaUnique()
    {
        await PrepararTopoAsync(FonteCompleta());

        FonteEmMemoria fonte = FonteCompleta();
        fonte.Logradouros.Clear();
        // Rotação: o texto completo de A ("rua a") coincide com o name-only de B ("Rua A"
        // sem o tipo). Sob a chave antiga (name-only) e a nova (texto completo) ambos são
        // válidos, mas um UPDATE de uma passada colidiria no instante intermediário.
        fonte.Logradouros.Add(DadosDne.Logradouro(
            "68500000", "A", IdCidadeMaraba, tipo: "Rua",
            nomeCompleto: "Rua A", logradouroSemAcento: "rua a"));
        fonte.Logradouros.Add(DadosDne.Logradouro(
            "68500000", "Rua A", IdCidadeMaraba, tipo: "Avenida",
            nomeCompleto: "Avenida Rua A", logradouroSemAcento: "avenida rua a"));

        await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        // Estado legado name-only: A → "a", B → "rua a" (rotaciona com o futuro texto de A).
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            await ctx.Database.ExecuteSqlRawAsync("UPDATE logradouro SET nome_normalizado = 'a' WHERE nome = 'A'");
            await ctx.Database.ExecuteSqlRawAsync("UPDATE logradouro SET nome_normalizado = 'rua a' WHERE nome = 'Rua A'");
        }

        // O backfill (drop índice → recalcula → recria) conclui sem abortar pela colisão
        // transitória e deixa as duas chaves finais corretas.
        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(BackfillSql);
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        List<Logradouro> doCep = await leitura.Logradouros.Where(l => l.Cep == "68500000").ToListAsync();
        doCep.Should().HaveCount(2);
        doCep.Select(l => l.NomeNormalizado).Should().BeEquivalentTo(["rua a", "avenida rua a"]);
    }

    [Fact(DisplayName = "#707: dedup do backfill preserva a linha vigente, não a stale de id maior")]
    public async Task Backfill_Dedup_PreservaVigenteSobreStale()
    {
        await PrepararTopoAsync(FonteCompleta());

        const string vivaId = "01000000-0000-7000-8000-000000000001";
        const string staleId = "01000000-0000-7000-8000-000000000002"; // id MAIOR que a viva

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            Guid cidadeId = (await ctx.Cidades.SingleAsync(c => c.CodigoIbge == CodigoMaraba)).Id;

            // Duas linhas que convergem para "rua a" após o recálculo (mesmo nome_completo),
            // com chaves legadas distintas ("a" e "rua a"). A stale (vigente=false) tem id
            // MAIOR — a regra ingênua "maior id vence" manteria a errada, escondendo o
            // endereço dos filtros read-side por vigente=true.
            string sql = string.Format(
                System.Globalization.CultureInfo.InvariantCulture,
                "INSERT INTO logradouro (id, cep, tipo, nome, nome_completo, nome_normalizado, cidade_id, uf, ativo, versao_dataset, vigente, created_at) VALUES " +
                "('{0}', '68500000', 'Rua', 'A', 'Rua A', 'a', '{2}', 'PA', true, '202601', true, now()), " +
                "('{1}', '68500000', 'Avenida', 'Rua A', 'Rua A', 'rua a', '{2}', 'PA', true, '202512', false, now());",
                vivaId, staleId, cidadeId);
            await ctx.Database.ExecuteSqlRawAsync(sql);
        }

        await using (GeoDbContext ctx = _fixture.CreateDbContext())
        {
            await ctx.Database.ExecuteSqlRawAsync(BackfillSql);
        }

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Logradouro sobrevivente = await leitura.Logradouros.SingleAsync(l => l.Cep == "68500000");
        sobrevivente.Id.Should().Be(Guid.Parse(vivaId), "a linha vigente vence a stale de id maior");
        sobrevivente.Vigente.Should().BeTrue();
        sobrevivente.NomeNormalizado.Should().Be("rua a");
    }

    [Fact(DisplayName = "CA-05 (#707): logradouros homônimos sob o mesmo CEP-geral coexistem — o texto completo distingue a chave de upsert")]
    public async Task CepGeral_HomonimosPorTipo_CoexistemPorTextoCompleto()
    {
        await PrepararTopoAsync(FonteCompleta());
        FonteEmMemoria fonte = FonteCompleta();
        fonte.Logradouros.Clear();

        // Mesmo CEP-geral, mesmo nome sem o tipo ("das Flores"); só o tipo — logo, o texto
        // completo — difere. Com a antiga chave name-only um seria descartado pelo DISTINCT ON;
        // com o texto completo (nome_normalizado) ambos coexistem.
        fonte.Logradouros.Add(DadosDne.Logradouro(
            "68500500", "das Flores", IdCidadeMaraba, tipo: "Rua",
            nomeCompleto: "Rua das Flores", logradouroSemAcento: "rua das flores"));
        fonte.Logradouros.Add(DadosDne.Logradouro(
            "68500500", "das Flores", IdCidadeMaraba, tipo: "Travessa",
            nomeCompleto: "Travessa das Flores", logradouroSemAcento: "travessa das flores"));

        await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        List<Logradouro> doCep = await leitura.Logradouros.Where(l => l.Cep == "68500500").ToListAsync();
        doCep.Should().HaveCount(2);
        doCep.Select(l => l.NomeNormalizado).Should().BeEquivalentTo(["rua das flores", "travessa das flores"]);
    }

    [Fact(DisplayName = "CA-05: logradouro com cidade órfã é descartado e contado; distrito_id nulo é aceito")]
    public async Task LogradouroOrfao_Descartado()
    {
        await PrepararTopoAsync(FonteCompleta());
        FonteEmMemoria fonte = FonteCompleta();
        fonte.Logradouros.Add(DadosDne.Logradouro("99999999", "Rua Fantasma", IdCidadeInexistente));

        RelatorioImportacao relatorio = await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Logradouros.AnyAsync(l => l.Cep == "99999999")).Should().BeFalse();
        relatorio.Tabelas["logradouro"].Orfaos.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "CA-06: upsert de distrito por (cidade, nome) — duplicado na mesma cidade não duplica")]
    public async Task DistritoDuplicado_NaMesmaCidade_NaoDuplica()
    {
        await PrepararTopoAsync(FonteCompleta());
        FonteEmMemoria fonte = FonteCompleta();
        // Mesmo (cidade, nome) de um distrito já presente, com outro id_distrito da fonte.
        fonte.Distritos.Add(DadosDne.Distrito(11, "Cidade Nova", IdCidadeMaraba));

        RelatorioImportacao relatorio = await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Distritos.CountAsync(d => d.Nome == "Cidade Nova")).Should().Be(1);
        relatorio.Tabelas["distrito"].Duplicados.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "CA-06 + FK: distritos homônimos em cidades distintas coexistem e o logradouro resolve para o Guid correto via id_distrito")]
    public async Task DistritoHomonimo_EmCidadesDistintas_ResolveFkCorreta()
    {
        await PrepararTopoAsync(FonteCompleta());
        FonteEmMemoria fonte = FonteCompleta();
        // "Centro" existe em Marabá (id 30) e em São Paulo (id 20 já no FonteCompleta).
        fonte.Distritos.Add(DadosDne.Distrito(30, "Centro", IdCidadeMaraba));
        // Dois logradouros, um por "Centro", referenciando o id_distrito respectivo.
        fonte.Logradouros.Add(DadosDne.Logradouro("68500100", "Av Marabá", IdCidadeMaraba, distritoIdDne: 30));
        fonte.Logradouros.Add(DadosDne.Logradouro("01000000", "Av Paulista", IdCidadeSaoPaulo, distritoIdDne: 20));

        await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Guid marabaId = (await leitura.Cidades.SingleAsync(c => c.CodigoIbge == CodigoMaraba)).Id;
        Guid spId = (await leitura.Cidades.SingleAsync(c => c.CodigoIbge == CodigoSaoPaulo)).Id;

        (await leitura.Distritos.CountAsync(d => d.Nome == "Centro")).Should().Be(2);

        Distrito centroMaraba = await leitura.Distritos.SingleAsync(d => d.Nome == "Centro" && d.CidadeId == marabaId);
        Distrito centroSp = await leitura.Distritos.SingleAsync(d => d.Nome == "Centro" && d.CidadeId == spId);
        centroMaraba.Id.Should().NotBe(centroSp.Id);

        Logradouro avMaraba = await leitura.Logradouros.SingleAsync(l => l.Nome == "Av Marabá");
        Logradouro avPaulista = await leitura.Logradouros.SingleAsync(l => l.Nome == "Av Paulista");
        avMaraba.DistritoId.Should().Be(centroMaraba.Id);
        avPaulista.DistritoId.Should().Be(centroSp.Id);
    }

    [Fact(DisplayName = "Coerência: distrito_id que resolve para distrito de outra cidade degrada para null (não cruza cidade)")]
    public async Task DistritoDeOutraCidade_DegradaParaNull()
    {
        await PrepararTopoAsync(FonteCompleta());
        FonteEmMemoria fonte = FonteCompleta();
        // Logradouro em Marabá apontando para o distrito 20 ("Centro"), que é de São Paulo.
        fonte.Logradouros.Add(DadosDne.Logradouro("68500200", "Rua Cruzada", IdCidadeMaraba, distritoIdDne: 20));

        RelatorioImportacao relatorio = await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        Logradouro cruzada = await leitura.Logradouros.SingleAsync(l => l.Nome == "Rua Cruzada");
        cruzada.DistritoId.Should().BeNull("o distrito resolvido é de outra cidade — não pode cruzar");
        relatorio.Tabelas["logradouro"].ParsesDegradados.Should().BeGreaterThanOrEqualTo(1);
    }

    [Fact(DisplayName = "CA-07: recarga (staging + ON CONFLICT) é idempotente — contagens idênticas, Id e created_at preservados, updated_at carimbado")]
    public async Task Recarga_Idempotente()
    {
        await PrepararTopoAsync(FonteCompleta());
        await ExecutarLocalidadesAsync(FonteCompleta(), ModoCarga.Inicial);

        Guid logradouroId;
        DateTimeOffset createdAt;
        int totalLogradouros;
        await using (GeoDbContext antes = _fixture.CreateDbContext())
        {
            Logradouro ruaA = await antes.Logradouros.SingleAsync(l => l.Nome == "Rua A");
            logradouroId = ruaA.Id;
            createdAt = ruaA.CreatedAt;
            totalLogradouros = await antes.Logradouros.CountAsync();
        }

        await ExecutarLocalidadesAsync(FonteCompleta(), ModoCarga.Recarga);

        await using GeoDbContext depois = _fixture.CreateDbContext();
        (await depois.Logradouros.CountAsync()).Should().Be(totalLogradouros, "a recarga idempotente não pode duplicar");
        (await depois.LogradouroComplementos.CountAsync()).Should().Be(2);

        Logradouro ruaARecarga = await depois.Logradouros.SingleAsync(l => l.Nome == "Rua A");
        ruaARecarga.Id.Should().Be(logradouroId, "ON CONFLICT preserva a PK");
        ruaARecarga.CreatedAt.Should().Be(createdAt, "created_at é preservado no conflito");
        ruaARecarga.UpdatedAt.Should().NotBeNull("updated_at é carimbado no DO UPDATE");
    }

    [Fact(DisplayName = "CA-02: COPY em lote suporta volume — várias linhas streamadas entram sem duplicar")]
    public async Task CopyEmLote_SuportaVolume()
    {
        await PrepararTopoAsync(FonteCompleta());
        FonteEmMemoria fonte = FonteCompleta();
        fonte.Logradouros.Clear();
        for (int i = 0; i < 250; i++)
        {
            fonte.Logradouros.Add(DadosDne.Logradouro(
                cep: 68500000.ToString(System.Globalization.CultureInfo.InvariantCulture),
                nome: $"Rua {i.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
                cidadeIdDne: IdCidadeMaraba));
        }

        await ExecutarLocalidadesAsync(fonte, ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Logradouros.CountAsync()).Should().Be(250);
    }

    [Fact(DisplayName = "CA-08: o relatório reporta totais por tabela (lidos/inseridos) das folhas")]
    public async Task Relatorio_ReportaTotais()
    {
        await PrepararTopoAsync(FonteCompleta());
        RelatorioImportacao relatorio = await ExecutarLocalidadesAsync(FonteCompleta(), ModoCarga.Inicial);

        relatorio.Tabelas["distrito"].Lidos.Should().Be(2);
        relatorio.Tabelas["bairro"].Lidos.Should().Be(2);
        relatorio.Tabelas["logradouro"].Lidos.Should().Be(2);
        relatorio.Tabelas["logradouro"].Inseridos.Should().Be(2);
        relatorio.Tabelas["logradouro_complemento"].Inseridos.Should().Be(2);
    }

    [Fact(DisplayName = "Reexecução do modo Inicial é idempotente (TRUNCATE + recarga não viola UNIQUE)")]
    public async Task Inicial_Reexecutado_Idempotente()
    {
        await PrepararTopoAsync(FonteCompleta());
        await ExecutarLocalidadesAsync(FonteCompleta(), ModoCarga.Inicial);
        await ExecutarLocalidadesAsync(FonteCompleta(), ModoCarga.Inicial);

        await using GeoDbContext leitura = _fixture.CreateDbContext();
        (await leitura.Logradouros.CountAsync()).Should().Be(2);
        (await leitura.Distritos.CountAsync()).Should().Be(2);
    }

    private async Task PrepararTopoAsync(IGeoFonteDados fonte)
    {
        await LimparAsync();
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        GeoImportadorPaisEstadoCidade importador = new(ctx, NullLogger<GeoImportadorPaisEstadoCidade>.Instance);
        await importador.ImportarAsync(fonte, CancellationToken.None);
    }

    private async Task<RelatorioImportacao> ExecutarLocalidadesAsync(IGeoFonteDados fonte, ModoCarga modo)
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        IConfiguration configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:GeoDb"] = _fixture.ConnectionString })
            .Build();
        GeoImportadorDistritoBairro distritoBairro = new(ctx);
        LogradouroCopyImporter logradouro = new(configuracao, TimeProvider.System, NullLogger<LogradouroCopyImporter>.Instance);
        GeoImportadorLocalidades orquestrador = new(distritoBairro, logradouro, NullLogger<GeoImportadorLocalidades>.Instance);
        return await orquestrador.ImportarAsync(fonte, modo, CancellationToken.None);
    }

    private async Task LimparAsync()
    {
        await using GeoDbContext ctx = _fixture.CreateDbContext();
        // pais CASCADE limpa a cadeia de FK (estado→cidade→distrito/bairro→logradouro);
        // complemento e grande usuário não têm FK para essa cadeia, então são truncados
        // à parte para isolar cada teste no banco compartilhado da coleção.
        await ctx.Database.ExecuteSqlRawAsync("TRUNCATE TABLE pais, logradouro_complemento, cep_grande_usuario CASCADE");
    }

    // indisvalid = false sinaliza um CREATE INDEX CONCURRENTLY que não concluiu; true
    // prova que a recriação fora de transação completou.
    private async Task<bool> IndiceValidoAsync(string indice)
    {
        await using NpgsqlConnection conexao = new(_fixture.ConnectionString);
        await conexao.OpenAsync();
        await using NpgsqlCommand comando = conexao.CreateCommand();
        comando.CommandText = "SELECT i.indisvalid FROM pg_index i JOIN pg_class c ON c.oid = i.indexrelid WHERE c.relname = @nome";
        comando.Parameters.AddWithValue("nome", indice);
        object? resultado = await comando.ExecuteScalarAsync();
        return resultado is bool valido && valido;
    }

    private static FonteEmMemoria FonteCompleta()
    {
        FonteEmMemoria fonte = new() { Versao = "202601" };

        // Topo (carregado pelo importador da #672 antes das folhas).
        fonte.Paises.Add(DadosDne.Pais("BRA", "Brasil", "BR"));
        fonte.Estados.Add(DadosDne.Estado("PA", "Pará", capital: "Belém", faixaIni: "66000000", faixaFim: "68899999"));
        fonte.Estados.Add(DadosDne.Estado("SP", "São Paulo", regiao: "Sudeste", capital: "São Paulo", faixaIni: "01000000", faixaFim: "19999999"));
        fonte.EstadoIndicadores.Add(DadosDne.EstadoIndicador("PA", "15"));
        fonte.EstadoIndicadores.Add(DadosDne.EstadoIndicador("SP", "35"));
        fonte.Cidades.Add(DadosDne.Cidade(CodigoMaraba, "Marabá", "PA", ddd: "94"));
        fonte.Cidades.Add(DadosDne.Cidade(CodigoSaoPaulo, "São Paulo", "SP", ddd: "11"));

        // Mapa id_cidade (int4 da fonte) → código IBGE, para resolver a FK das folhas.
        fonte.CidadeIds.Add(DadosDne.CidadeId(IdCidadeMaraba, CodigoMaraba));
        fonte.CidadeIds.Add(DadosDne.CidadeId(IdCidadeSaoPaulo, CodigoSaoPaulo));

        // Folhas.
        fonte.Distritos.Add(DadosDne.Distrito(10, "Cidade Nova", IdCidadeMaraba, latitude: "-5.36", longitude: "-49.11"));
        fonte.Distritos.Add(DadosDne.Distrito(20, "Centro", IdCidadeSaoPaulo));
        fonte.DistritoFaixas.Add(DadosDne.Faixa(10, "68500000", "68519999"));

        fonte.Bairros.Add(DadosDne.Bairro(100, "Cidade Nova", IdCidadeMaraba));
        fonte.Bairros.Add(DadosDne.Bairro(200, "Sé", IdCidadeSaoPaulo));
        fonte.BairroFaixas.Add(DadosDne.Faixa(100, "68500000", "68509999"));

        fonte.CepGrandesUsuarios.Add(DadosDne.GrandeUsuario("68500900", "Prefeitura de Marabá"));

        fonte.LogradouroComplementos.Add(DadosDne.Complemento("68500000", "lado par"));
        fonte.LogradouroComplementos.Add(DadosDne.Complemento("68500000", "lado ímpar"));

        fonte.Logradouros.Add(DadosDne.Logradouro("68500000", "Rua A", IdCidadeMaraba, tipo: "Rua", bairroIdDne: 100, distritoIdDne: 10, latitude: "-5.36867", longitude: "-49.11731", cepAtivo: "S"));
        fonte.Logradouros.Add(DadosDne.Logradouro("68500001", "Rua B", IdCidadeMaraba, distritoIdDne: null, cepAtivo: "N"));

        return fonte;
    }
}
