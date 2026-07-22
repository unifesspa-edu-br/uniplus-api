namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Integração ponta-a-ponta do catálogo <c>rol_de_fatos_candidato</c> contra Postgres real
/// (UNI-REQ-0077, ADR-0111, refinada pela ADR-0116; ampliada pela UNI-REQ-0078): seed dos dezessete fatos, leitor
/// cross-módulo, ordenação, resolução por chave natural, sobrevivência do
/// <c>valores_dominio</c> nulo ao round-trip, CHECKs de domínio/coerência, índice
/// único total do código e o seed de <c>fato_valor_dominio</c>.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit collection fixture exige tipo de teste público.")]
[SuppressMessage(
    "Security",
    "CA2100:Review SQL queries for security vulnerabilities",
    Justification = "SQL fixo escrito no próprio teste; os valores externos entram por parâmetro interpolado (DbParameter).")]
public sealed class FatoCandidatoPersistenceTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public FatoCandidatoPersistenceTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Todo item do seed passa pela factory de domínio — a linha materializada é construível")]
    public void Seed_TodoItemEhAceitoPelaFactory()
    {
        // O seed materializa linhas direto pela migration, sem passar pela factory.
        // Este teste fecha essa lacuna: garante que cada item semeado satisfaz as
        // invariantes de FatoCandidato.Criar (formato de código, tamanho de nome,
        // coerência binding × origem, ponto de resolução canônico).
        foreach (FatoCandidatoSeedItem item in FatoCandidatoSeed.Itens)
        {
            Result<FatoCandidato> resultado = FatoCandidato.Criar(
                item.Codigo,
                item.Nome,
                item.Descricao,
                item.Dominio,
                item.Origem,
                item.Cardinalidade,
                item.ValoresDominio,
                item.PontoResolucao,
                item.Binding);

            resultado.IsSuccess.Should().BeTrue(
                $"o item semeado {item.Codigo} deve satisfazer as invariantes de domínio; erro: {resultado.Error?.Code}");
        }
    }

    [Fact(DisplayName = "Cada cota tem par elegibilidade + opt-in como fatos independentes (UNI-REQ-0078)")]
    public async Task Seed_CadaCotaTemParElegibilidadeEOptIn()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        // Os quatro blocos do formulário de cotas. A elegibilidade de PPI é COR_RACA
        // (categórico); as demais são booleanas. O opt-in é sempre booleano.
        (string Elegibilidade, string OptIn)[] pares =
        [
            ("PCD", "CONCORRER_PCD"),
            ("EGRESSO_ESCOLA_PUBLICA", "CONCORRER_EP"),
            ("COR_RACA", "CONCORRER_PPI"),
            ("QUILOMBOLA", "CONCORRER_Q"),
            ("BAIXA_RENDA", "CONCORRER_RENDA"),
        ];

        foreach ((string elegibilidade, string optIn) in pares)
        {
            FatoCandidato fatoElegibilidade = fatos.Single(f => f.Codigo == elegibilidade);
            FatoCandidato fatoOptIn = fatos.Single(f => f.Codigo == optIn);

            fatoElegibilidade.Id.Should().NotBe(fatoOptIn.Id,
                $"{elegibilidade} e {optIn} são fatos independentes — a elegibilidade sozinha não coloca na cota");
            fatoOptIn.Dominio.Should().Be(DominioFato.Booleano);
            fatoOptIn.Origem.Should().Be(OrigemFato.Declarado,
                "o opt-in é seleção direta do candidato, ainda que expresse vontade e não elegibilidade");
            fatoOptIn.Cardinalidade.Should().Be(CardinalidadeFato.Escalar);
        }
    }

    [Fact(DisplayName = "Fatos reutilizados mantêm a identidade semeada — o vocabulário não duplica código")]
    public async Task Seed_FatosPreexistentesSaoReutilizadosSemDuplicar()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        // Quatro fatos já existiam antes desta leva e são REUTILIZADOS, não recadastrados:
        // renomeá-los violaria a imutabilidade de código da ADR-0111.
        (string Codigo, string IdSufixo)[] reutilizados =
        [
            ("COR_RACA", "001"),
            ("QUILOMBOLA", "002"),
            ("PCD", "003"),
            ("EGRESSO_ESCOLA_PUBLICA", "004"),
        ];

        foreach ((string codigo, string idSufixo) in reutilizados)
        {
            FatoCandidato fato = fatos.Single(f => f.Codigo == codigo);
            fato.Id.Should().Be(Guid.Parse($"fa700000-0000-7000-8000-{idSufixo.PadLeft(12, '0')}"),
                $"{codigo} preserva o Guid determinístico da semeadura original");
        }

        fatos.Select(f => f.Codigo).Should().OnlyHaveUniqueItems("o catálogo nunca tem dois fatos com o mesmo código");
        fatos.Select(f => f.Id).Should().OnlyHaveUniqueItems();

        // Nenhum código adjacente foi criado como sinônimo dos reutilizados.
        fatos.Select(f => f.Codigo).Should().NotContain(["PCD_AUTODECLARADO", "ESCOLA_PUBLICA"]);

        // BAIXA_RENDA não substitui RENDA_PER_CAPITA: coexistem com naturezas distintas.
        fatos.Single(f => f.Codigo == "BAIXA_RENDA").Origem.Should().Be(OrigemFato.Declarado);
        fatos.Single(f => f.Codigo == "RENDA_PER_CAPITA").Origem.Should().Be(OrigemFato.Derivado);
    }

    [Fact(DisplayName = "Fato booleano não recebe FatoValorDominio (ADR-0111/ADR-0116)")]
    public async Task Seed_BooleanoNaoRecebeValorDominio()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> booleanos = await ctx.FatosCandidato.AsNoTracking()
            .Where(f => f.Dominio == DominioFato.Booleano)
            .Include(f => f.ValoresDominioDeclarados)
            .ToListAsync();

        // Prende a leva nova: sem os seis fatos desta story o conjunto não os contém.
        booleanos.Select(f => f.Codigo).Should().Contain(
            ["BAIXA_RENDA", "CONCORRER_PCD", "CONCORRER_EP", "CONCORRER_PPI", "CONCORRER_Q", "CONCORRER_RENDA"]);
        foreach (FatoCandidato fato in booleanos)
        {
            fato.ValoresDominio.Should().BeNull($"{fato.Codigo} é booleano — domínio intrínseco SIM/NÃO");
            fato.ValoresDominioDeclarados.Should().BeEmpty(
                $"{fato.Codigo} é booleano; FatoValorDominio só vale para categórico estático");
        }
    }

    [Fact(DisplayName = "Seed materializa exatamente os dezessete fatos do vocabulário, batendo com a fonte única")]
    public async Task Seed_MaterializaTodosOsFatosDaFonteUnica()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        fatos.Should().HaveCount(FatoCandidatoSeed.Itens.Count).And.HaveCount(17);
        fatos.Select(f => f.Codigo).Should().OnlyHaveUniqueItems();

        foreach (FatoCandidatoSeedItem item in FatoCandidatoSeed.Itens)
        {
            FatoCandidato persistido = fatos.Single(f => f.Codigo == item.Codigo);
            persistido.Nome.Should().Be(item.Nome);
            persistido.Dominio.Should().Be(item.Dominio);
            persistido.Origem.Should().Be(item.Origem);
            persistido.Cardinalidade.Should().Be(item.Cardinalidade);
            persistido.PontoResolucao.Should().Be(item.PontoResolucao);
            persistido.Binding.Should().Be(item.Binding);

            if (item.ValoresDominio is null)
            {
                persistido.ValoresDominio.Should().BeNull($"{item.Codigo} tem valores nulos na fonte");
            }
            else
            {
                persistido.ValoresDominio.Should().Equal(item.ValoresDominio);
            }
        }
    }

    [Fact(DisplayName = "Origem: só FAIXA_ETARIA e RENDA_PER_CAPITA são Derivado; todos os demais são Declarado (ADR-0116)")]
    public async Task Seed_OrigemReclassificadaConformeADR0116()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        string[] derivados = [.. fatos
            .Where(f => f.Origem == OrigemFato.Derivado)
            .Select(f => f.Codigo)
            .OrderBy(c => c, StringComparer.Ordinal)];

        derivados.Should().Equal("FAIXA_ETARIA", "RENDA_PER_CAPITA");
        fatos.Where(f => f.Origem != OrigemFato.Derivado)
            .Should().OnlyContain(f => f.Origem == OrigemFato.Declarado);
    }

    [Fact(DisplayName = "PontoResolucao: todos os dezessete fatos resolvem em INSCRICAO")]
    public async Task Seed_PontoResolucaoInscricaoParaTodos()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        fatos.Should().OnlyContain(f => f.PontoResolucao == "INSCRICAO");
    }

    [Fact(DisplayName = "Cardinalidade: só MODALIDADE e CONDICAO_ATENDIMENTO são multivalorados; os demais escalares")]
    public async Task Seed_CardinalidadeCoerente()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        string[] multivalorados = [.. fatos
            .Where(f => f.Cardinalidade == CardinalidadeFato.Multivalorado)
            .Select(f => f.Codigo)
            .OrderBy(c => c, StringComparer.Ordinal)];

        multivalorados.Should().Equal("CONDICAO_ATENDIMENTO", "MODALIDADE");
        fatos.Where(f => f.Cardinalidade != CardinalidadeFato.Multivalorado)
            .Should().OnlyContain(f => f.Cardinalidade == CardinalidadeFato.Escalar);
    }

    [Fact(DisplayName = "valores_dominio nulo sobrevive ao round-trip jsonb (≠ lista vazia) — inclusive para os categóricos que migraram para FatoValorDominio")]
    public async Task ValoresDominioNulo_SobreviveRoundTrip()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        FatoCandidato booleano = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "QUILOMBOLA");
        booleano.ValoresDominio.Should().BeNull("booleano nunca enumera valores — o nulo é significante");

        FatoCandidato escopoProcesso = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "MODALIDADE");
        escopoProcesso.ValoresDominio.Should().BeNull("categórico de escopo-processo tem valores nulos, não vazios");

        FatoCandidato tipoDeficiencia = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "TIPO_DEFICIENCIA");
        tipoDeficiencia.ValoresDominio.Should().BeNull(
            "TIPO_DEFICIENCIA é categórico de escopo-processo — o domínio vem do cadastro TipoDeficiencia, não deste jsonb");

        FatoCandidato corRaca = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "COR_RACA");
        corRaca.ValoresDominio.Should().BeNull(
            "COR_RACA migrou o conjunto fechado para FatoValorDominio (ADR-0116) — o jsonb antigo fica nulo para não duplicar a informação");
    }

    [Fact(DisplayName = "Seed de FatoValorDominio: COR_RACA (6), SEXO (3) e NACIONALIDADE (3) têm os valores filhos esperados")]
    public async Task Seed_FatoValorDominio_MaterializaAsDozeLinhas()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoValorDominio> valores = await ctx.FatosValorDominio.AsNoTracking().ToListAsync();
        valores.Should().HaveCount(FatoValorDominioSeed.Itens.Count).And.HaveCount(12);

        FatoCandidato corRaca = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "COR_RACA");
        string[] codigosCorRaca = [.. valores
            .Where(v => v.FatoCandidatoId == corRaca.Id)
            .OrderBy(v => v.Ordem)
            .Select(v => v.Codigo)];
        codigosCorRaca.Should().Equal("BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO");
        valores.Where(v => v.FatoCandidatoId == corRaca.Id).Should().OnlyContain(v => v.Descricao != null && v.Ativo);

        FatoCandidato sexo = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "SEXO");
        string[] codigosSexo = [.. valores
            .Where(v => v.FatoCandidatoId == sexo.Id)
            .OrderBy(v => v.Ordem)
            .Select(v => v.Codigo)];
        codigosSexo.Should().Equal("FEMININO", "MASCULINO", "INTERSEXO");

        FatoCandidato nacionalidade = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "NACIONALIDADE");
        string[] codigosNacionalidade = [.. valores
            .Where(v => v.FatoCandidatoId == nacionalidade.Id)
            .OrderBy(v => v.Ordem)
            .Select(v => v.Codigo)];
        codigosNacionalidade.Should().Equal("NATO", "NATURALIZADO", "ESTRANGEIRO");

        FatoCandidato modalidade = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "MODALIDADE");
        valores.Should().NotContain(v => v.FatoCandidatoId == modalidade.Id,
            "MODALIDADE é escopo-processo — não tem FatoValorDominio filhos");
    }

    [Fact(DisplayName = "Índice único (FatoCandidatoId, Codigo) de fato_valor_dominio recusa código duplicado no mesmo fato")]
    public async Task FatoValorDominio_IndiceUnico_RecusaDuplicataNoMesmoFato()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        FatoCandidato corRaca = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "COR_RACA");

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.fato_valor_dominio (id, fato_candidato_id, codigo, descricao, ordem, ativo, created_at)
            VALUES ({Guid.CreateVersion7()}, {corRaca.Id}, {"PRETA"}, {"Duplicata"}, 99, true, {DateTimeOffset.UtcNow})
            """);

        Npgsql.PostgresException ex = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        ex.SqlState.Should().Be("23505");
        ex.ConstraintName.Should().Be("ux_fato_valor_dominio_fato_codigo");
    }

    [Fact(DisplayName = "Reader (resolvível de fora do assembly) lista ordenado por código e projeta a view com PontoResolucao/Binding")]
    public async Task Reader_Lista_OrdenadoPorCodigo()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new FatoCandidatoReader(ctx);

        IReadOnlyList<FatoCandidatoView> views = await reader.ListarAsync();

        views.Should().HaveCount(17);
        views.Select(v => v.Codigo).Should().BeInAscendingOrder(StringComparer.Ordinal);

        FatoCandidatoView corRaca = views.Single(v => v.Codigo == "COR_RACA");
        corRaca.Dominio.Should().Be("CATEGORICO");
        corRaca.Origem.Should().Be("DECLARADO");
        corRaca.Cardinalidade.Should().Be("ESCALAR");
        corRaca.PontoResolucao.Should().Be("INSCRICAO");
        corRaca.Binding.Should().Be("CAMPO_INSCRICAO:COR_RACA");
        // O jsonb legado é nulo na entidade (ADR-0116), mas a view projeta os códigos
        // declarados de volta para ValoresDominio — do contrário o consumidor
        // cross-módulo (PredicadoDnfValidador) classificaria COR_RACA como categórico
        // de escopo-processo/dinâmico em vez de estático, rejeitando um gatilho válido.
        corRaca.ValoresDominio.Should().Equal("BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO");
        corRaca.ValoresDominioDeclarados.Should().NotBeNull().And.HaveCount(6);
        corRaca.ValoresDominioDeclarados!.Select(v => v.Codigo).Should().Contain("PRETA");

        FatoCandidatoView faixaEtaria = views.Single(v => v.Codigo == "FAIXA_ETARIA");
        faixaEtaria.Origem.Should().Be("DERIVADO");
        faixaEtaria.Binding.Should().Be("ATRIBUTO_CANDIDATO:FAIXA_ETARIA");
        faixaEtaria.ValoresDominioDeclarados.Should().BeNull();

        FatoCandidatoView modalidade = views.Single(v => v.Codigo == "MODALIDADE");
        modalidade.ValoresDominioDeclarados.Should().BeNull("MODALIDADE é escopo-processo, sem FatoValorDominio filhos");
    }

    [Fact(DisplayName = "Reader resolve por chave natural e devolve null para código inexistente")]
    public async Task Reader_ObterPorCodigo_ResolveOuNull()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        var reader = new FatoCandidatoReader(ctx);

        FatoCandidatoView? sexo = await reader.ObterPorCodigoAsync("SEXO");
        sexo.Should().NotBeNull();
        sexo!.ValoresDominioDeclarados.Should().NotBeNull()
            .And.HaveCount(3)
            .And.ContainSingle(v => v.Codigo == "MASCULINO");

        FatoCandidatoView? inexistente = await reader.ObterPorCodigoAsync("NAO_EXISTE");
        inexistente.Should().BeNull();
    }

    [Fact(DisplayName = "Índice único total recusa código duplicado (insert cru de um código já semeado)")]
    public async Task IndiceUnicoTotal_RecusaDuplicata()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, valores_dominio, ponto_resolucao, binding, created_at)
            VALUES ({Guid.CreateVersion7()}, {"COR_RACA"}, {"Duplicata"}, {"BOOLEANO"}, {"DECLARADO"}, {"ESCALAR"},
                NULL, {"INSCRICAO"}, {"CAMPO_INSCRICAO:DUPLICATA"}, {DateTimeOffset.UtcNow})
            """);

        Npgsql.PostgresException ex = (await act.Should().ThrowAsync<Npgsql.PostgresException>()).Which;
        ex.SqlState.Should().Be("23505");
        ex.ConstraintName.Should().Be("ux_rol_de_fatos_candidato_codigo");
    }

    [Fact(DisplayName = "CHECK de domínio recusa token fora do vocabulário via SQL cru")]
    public async Task Check_RecusaDominioInvalido()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, valores_dominio, ponto_resolucao, binding, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"TEXTO"}, {"DECLARADO"}, {"ESCALAR"},
                NULL, {"INSCRICAO"}, {"CAMPO_INSCRICAO:X"}, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>("o CHECK ck_rol_de_fatos_candidato_dominio bloqueia 'TEXTO'");
    }

    [Fact(DisplayName = "CHECK de origem recusa token fora do vocabulário via SQL cru")]
    public async Task Check_RecusaOrigemInvalida()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, valores_dominio, ponto_resolucao, binding, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"BOOLEANO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"},
                NULL, {"INSCRICAO"}, {"CAMPO_INSCRICAO:X"}, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_rol_de_fatos_candidato_origem bloqueia o token legado 'BRUTO_INFORMADO' (ADR-0116 renomeou para DECLARADO)");
    }

    [Fact(DisplayName = "CHECK de coerência recusa valores em fato não-categórico via SQL cru")]
    public async Task Check_RecusaValoresEmNaoCategorico()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, valores_dominio, ponto_resolucao, binding, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"BOOLEANO"}, {"DECLARADO"}, {"ESCALAR"},
                {"[\"SIM\"]"}::jsonb, {"INSCRICAO"}, {"CAMPO_INSCRICAO:X"}, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência só admite valores em fato CATEGORICO");
    }

    [Fact(DisplayName = "CHECK de coerência recusa array vazio em fato categórico via SQL cru")]
    public async Task Check_RecusaArrayVazioEmCategorico()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, valores_dominio, ponto_resolucao, binding, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"CATEGORICO"}, {"DECLARADO"}, {"ESCALAR"},
                {"[]"}::jsonb, {"INSCRICAO"}, {"CAMPO_INSCRICAO:X"}, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK de coerência exige array não vazio quando valores_dominio é informado");
    }

    [Fact(DisplayName = "CHECK de coerência recusa elemento não-string no array via SQL cru")]
    public async Task Check_RecusaElementoNaoString()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, valores_dominio, ponto_resolucao, binding, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"CATEGORICO"}, {"DECLARADO"}, {"ESCALAR"},
                {"[1]"}::jsonb, {"INSCRICAO"}, {"CAMPO_INSCRICAO:X"}, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "um elemento numérico quebraria a desserialização de IReadOnlyList<string> no reader");
    }

    [Fact(DisplayName = "CHECK de coerência recusa string em branco no array via SQL cru")]
    public async Task Check_RecusaStringEmBranco()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, valores_dominio, ponto_resolucao, binding, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"CATEGORICO"}, {"DECLARADO"}, {"ESCALAR"},
                {"[\"  \"]"}::jsonb, {"INSCRICAO"}, {"CAMPO_INSCRICAO:X"}, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "a factory recusa item em branco; o CHECK espelha a invariante");
    }

    [Fact(DisplayName = "ValueComparer distingue nulo de lista vazia e preserva o nulo no snapshot")]
    public void ValueComparer_NuloDistintoDeVazio()
    {
        using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Microsoft.EntityFrameworkCore.ChangeTracking.ValueComparer comparer =
            ctx.Model.FindEntityType(typeof(FatoCandidato))!
                .FindProperty(nameof(FatoCandidato.ValoresDominio))!
                .GetValueComparer();

        IReadOnlyList<string> vazia = [];

        comparer.Equals(null, vazia).Should().BeFalse("nulo (escopo-processo) nunca é igual à lista vazia (ADR-0111)");
        comparer.Equals(null, null).Should().BeTrue();
        comparer.Snapshot(null).Should().BeNull("o snapshot de um nulo não pode virar lista vazia");
    }

    [Fact(DisplayName = "Seed bate com o roster literal independente da ADR-0111/ADR-0116 (autoridade), não só com a própria fonte")]
    public async Task Seed_BateComRosterIndependente()
    {
        // Expectativa literal declarada aqui — independente de FatoCandidatoSeed.Itens.
        // Falha se a fonte do seed divergir da autoridade da ADR (código, id fixo,
        // domínio, origem, cardinalidade, ponto de resolução, binding ou valores),
        // não apenas se a migration divergir da fonte.
        (string Codigo, string IdSufixo, DominioFato Dominio, OrigemFato Origem, CardinalidadeFato Cardinalidade, string Binding)[] esperado =
        [
            ("COR_RACA", "001", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:COR_RACA"),
            ("QUILOMBOLA", "002", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:QUILOMBOLA"),
            ("PCD", "003", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:PCD"),
            ("EGRESSO_ESCOLA_PUBLICA", "004", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:EGRESSO_ESCOLA_PUBLICA"),
            ("RENDA_PER_CAPITA", "005", DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA"),
            ("FAIXA_ETARIA", "006", DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA"),
            ("SEXO", "007", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:SEXO"),
            ("MODALIDADE", "008", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado, "CAMPO_INSCRICAO:MODALIDADE"),
            ("CONDICAO_ATENDIMENTO", "009", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado, "CAMPO_INSCRICAO:CONDICAO_ATENDIMENTO"),
            ("NACIONALIDADE", "010", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:NACIONALIDADE"),
            ("TIPO_DEFICIENCIA", "011", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:TIPO_DEFICIENCIA"),
            ("BAIXA_RENDA", "012", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:BAIXA_RENDA"),
            ("CONCORRER_PCD", "013", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:CONCORRER_PCD"),
            ("CONCORRER_EP", "014", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:CONCORRER_EP"),
            ("CONCORRER_PPI", "015", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:CONCORRER_PPI"),
            ("CONCORRER_Q", "016", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:CONCORRER_Q"),
            ("CONCORRER_RENDA", "017", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_INSCRICAO:CONCORRER_RENDA"),
        ];

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().ToListAsync();

        fatos.Should().HaveCount(esperado.Length);

        foreach ((string codigo, string idSufixo, DominioFato dominio, OrigemFato origem, CardinalidadeFato cardinalidade, string binding) in esperado)
        {
            FatoCandidato fato = fatos.Single(f => f.Codigo == codigo);
            fato.Id.Should().Be(Guid.Parse($"fa700000-0000-7000-8000-{idSufixo.PadLeft(12, '0')}"));
            fato.Dominio.Should().Be(dominio);
            fato.Origem.Should().Be(origem);
            fato.Cardinalidade.Should().Be(cardinalidade);
            fato.PontoResolucao.Should().Be("INSCRICAO");
            fato.Binding.Should().Be(binding);
        }
    }

    private static string CodigoUnico() => $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
