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
/// (UNI-REQ-0077, ADR-0111, refinada pela ADR-0116; ampliada pela UNI-REQ-0078): seed dos fatos de sistema, leitor
/// cross-módulo, ordenação, resolução por chave natural, CHECKs de domínio/coerência, índice
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
                item.FonteValores,
                item.Formato,
                item.PontoResolucao,
                item.Binding,
                item.Escopo,
                item.ClassificacaoProtecao,
                item.FinalidadeTratamento,
                FatoCandidatoSeed.HipoteseLegal,
                sistema: true);

            resultado.IsSuccess.Should().BeTrue(
                $"o item semeado {item.Codigo} deve satisfazer as invariantes de domínio; erro: {resultado.Error?.Code}");
        }
    }

    [Fact(DisplayName = "Cada cota tem par elegibilidade + opt-in como fatos independentes (UNI-REQ-0078)")]
    public async Task Seed_CadaCotaTemParElegibilidadeEOptIn()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

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

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

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
            fato.ValoresDominioDeclarados.Should().BeEmpty(
                $"{fato.Codigo} é booleano; FatoValorDominio só vale para categórico estático");
        }
    }

    [Fact(DisplayName = "Seed materializa exatamente os fatos de sistema, batendo com a fonte única")]
    public async Task Seed_MaterializaTodosOsFatosDaFonteUnica()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        fatos.Should().HaveCount(FatoCandidatoSeed.Itens.Count).And.HaveCount(45);
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
            persistido.Formato.Should().Be(item.Formato);

            persistido.ClassificacaoProtecao.Should().Be(item.ClassificacaoProtecao);
            persistido.Escopo.Should().Be(item.Escopo);
            persistido.FinalidadeTratamento.Should().Be(item.FinalidadeTratamento);
            persistido.Sistema.Should().BeTrue($"{item.Codigo} é semeado pelo sistema");
            persistido.Ativo.Should().BeTrue();
        }
    }

    [Fact(DisplayName = "Cor ou raça, deficiência, tipo de deficiência e o que os revela são sensíveis; o nome social é público; os demais, pessoais")]
    public async Task Seed_ClassificacaoDeProtecao()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        fatos.Where(f => f.ClassificacaoProtecao == ClassificacaoProtecaoDado.Sensivel)
            .Select(f => f.Codigo).Order(StringComparer.Ordinal).Should().Equal(
                "CONCORRER_PCD", "CONCORRER_PPI", "CONCORRER_Q", "CONDICAO_ATENDIMENTO", "COR_RACA", "MODALIDADE",
                "MODALIDADE_CONVOCACAO", "PCD", "QUILOMBOLA", "TIPO_DEFICIENCIA");
        fatos.Where(f => f.ClassificacaoProtecao == ClassificacaoProtecaoDado.Publico)
            .Select(f => f.Codigo).Should().Equal("NOME_SOCIAL");
        fatos.Where(f => f.ClassificacaoProtecao is not (ClassificacaoProtecaoDado.Sensivel or ClassificacaoProtecaoDado.Publico))
            .Should().OnlyContain(f => f.ClassificacaoProtecao == ClassificacaoProtecaoDado.Pessoal);
    }

    [Theory(DisplayName = "CHECK recusa categórico sem fonte e texto sem formato via SQL cru")]
    [InlineData("CATEGORICO")]
    [InlineData("TEXTO")]
    public async Task Check_RecusaCategoricoSemFonteETextoSemFormato(string dominio)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {dominio}, {"DECLARADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "a fonte do categórico e o formato do texto são obrigatórios também no banco — NULL não pode passar pelo IN");
    }

    [Theory(DisplayName = "CHECK recusa texto público que não seja o nome social de sistema, via SQL cru")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Check_RecusaTextoPublicoForaDoNomeSocial(bool sistema)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, formato, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"TEXTO"}, {"DECLARADO"}, {"ESCALAR"}, {"LIVRE"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"PUBLICO"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, {sistema}, true, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "só o nome social de sistema foge da classificação mínima do texto");
    }

    [Fact(DisplayName = "CHECK recusa classificação de proteção fora do domínio via SQL cru")]
    public async Task Check_RecusaClassificacaoInvalida()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        Func<Task> act = async () => await ctx.Database.ExecuteSqlAsync(
            $"""
            INSERT INTO configuracao.rol_de_fatos_candidato
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"BOOLEANO"}, {"DECLARADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"NAO_PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_rol_de_fatos_candidato_classificacao_protecao só admite a escala da ADR-0081");
    }

    [Fact(DisplayName = "Duas edições concorrentes do mesmo fato: a segunda é recusada pela concorrência otimista")]
    public async Task EdicaoConcorrente_SegundaRecusada()
    {
        FatoCandidato fato = NovoFatoDoAdministrador();
        try
        {
            await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: "admin-1"))
            {
                ctx.FatosCandidato.Add(fato);
                await ctx.SaveChangesAsync();
            }

            await using ConfiguracaoDbContext primeiro = _fixture.CreateDbContext(userId: "admin-1");
            await using ConfiguracaoDbContext segundo = _fixture.CreateDbContext(userId: "admin-2");
            FatoCandidato noPrimeiro = await primeiro.FatosCandidato.SingleAsync(f => f.Id == fato.Id);
            FatoCandidato noSegundo = await segundo.FatosCandidato.SingleAsync(f => f.Id == fato.Id);

            noPrimeiro.AlterarDescritivo("Nome da primeira edição", null).IsSuccess.Should().BeTrue();
            await primeiro.SaveChangesAsync();
            noSegundo.AlterarDescritivo("Nome da segunda edição", null).IsSuccess.Should().BeTrue();

            Func<Task> act = () => segundo.SaveChangesAsync();
            await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
        }
        finally
        {
            await RemoverAsync(fato.Id);
        }
    }

    [Fact(DisplayName = "Cadastro e edição do fato registram quem criou e quem alterou")]
    public async Task CadastroEEdicao_RegistramAutoria()
    {
        FatoCandidato fato = NovoFatoDoAdministrador();
        try
        {
            await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: "admin-1"))
            {
                ctx.FatosCandidato.Add(fato);
                await ctx.SaveChangesAsync();
            }

            await using (ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: "admin-2"))
            {
                FatoCandidato rastreado = await ctx.FatosCandidato.SingleAsync(f => f.Id == fato.Id);
                rastreado.Desativar().IsSuccess.Should().BeTrue();
                await ctx.SaveChangesAsync();
            }

            await using ConfiguracaoDbContext leitura = _fixture.CreateDbContext(userId: null);
            FatoCandidato persistido = await leitura.FatosCandidato.AsNoTracking().SingleAsync(f => f.Id == fato.Id);
            persistido.CreatedBy.Should().Be("admin-1");
            persistido.UpdatedBy.Should().Be("admin-2");
        }
        finally
        {
            await RemoverAsync(fato.Id);
        }
    }

    /// <summary>O banco é compartilhado pela coleção: o fato criado pelo teste sai no fim, para não mudar o seed que os outros contam.</summary>
    private async Task RemoverAsync(Guid id)
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        await ctx.FatosCandidato.Where(f => f.Id == id).ExecuteDeleteAsync();
    }

    private static FatoCandidato NovoFatoDoAdministrador()
    {
        string codigo = CodigoUnico();
        return FatoCandidato.Criar(
            codigo, "Fato do administrador", null, DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar,
            fonteValores: null, formato: null, "INSCRICAO", $"CAMPO_FORMULARIO:{codigo}", EscopoFato.Candidato,
            ClassificacaoProtecaoDado.Pessoal, "Teste", HipoteseLegalTratamento.CumprimentoObrigacaoLegal, sistema: false).Value!;
    }

    [Fact(DisplayName = "Origem: os calculados de atributo, a modalidade e a modalidade da convocação são Derivado; os demais são Declarado (ADR-0116)")]
    public async Task Seed_OrigemReclassificadaConformeADR0116()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        string[] derivados = [.. fatos
            .Where(f => f.Origem == OrigemFato.Derivado)
            .Select(f => f.Codigo)
            .OrderBy(c => c, StringComparer.Ordinal)];

        // MODALIDADE passou a Derivado: não é resposta do candidato, e sim resultado da avaliação
        // dos fatos declarados contra as regras congeladas do processo (ADR-0116, emenda 2026-07-22).
        derivados.Should().Equal("FAIXA_ETARIA", "MODALIDADE", "MODALIDADE_CONVOCACAO", "MUNICIPIO_RESIDENCIA", "RENDA_PER_CAPITA", "UF_RESIDENCIA");
        fatos.Where(f => f.Origem != OrigemFato.Derivado)
            .Should().OnlyContain(f => f.Origem == OrigemFato.Declarado);
    }

    [Fact(DisplayName = "PontoResolucao: os fatos de sistema resolvem na inscrição, exceto a modalidade da convocação, no resultado final")]
    public async Task Seed_PontoResolucaoInscricaoParaTodos()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        fatos.Where(static f => f.Codigo != "MODALIDADE_CONVOCACAO").Should().OnlyContain(static f => f.PontoResolucao == "INSCRICAO");
        fatos.Single(static f => f.Codigo == "MODALIDADE_CONVOCACAO").PontoResolucao.Should().Be("RESULTADO_FINAL");
    }

    [Fact(DisplayName = "Cardinalidade: só MODALIDADE e CONDICAO_ATENDIMENTO são multivalorados; os demais escalares")]
    public async Task Seed_CardinalidadeCoerente()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        string[] multivalorados = [.. fatos
            .Where(f => f.Cardinalidade == CardinalidadeFato.Multivalorado)
            .Select(f => f.Codigo)
            .OrderBy(c => c, StringComparer.Ordinal)];

        multivalorados.Should().Equal("CONDICAO_ATENDIMENTO", "MODALIDADE");
        fatos.Where(f => f.Cardinalidade != CardinalidadeFato.Multivalorado)
            .Should().OnlyContain(f => f.Cardinalidade == CardinalidadeFato.Escalar);
    }

    [Fact(DisplayName = "Seed de FatoValorDominio: os categóricos estáticos de sistema têm os valores filhos esperados")]
    public async Task Seed_FatoValorDominio_MaterializaOsValoresDosCategoricosEstaticos()
    {
        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);

        // Só os valores dos fatos de sistema: o catálogo também tem fatos do administrador semeados.
        List<FatoValorDominio> valores = await ctx.FatosValorDominio.AsNoTracking()
            .Where(v => ctx.FatosCandidato.Any(f => f.Id == v.FatoCandidatoId && f.Sistema))
            .ToListAsync();
        valores.Should().HaveCount(FatoValorDominioSeed.Itens.Count).And.HaveCount(39);

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

        FatoCandidato parentesco = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "PARENTESCO");
        string[] codigosParentesco = [.. valores
            .Where(v => v.FatoCandidatoId == parentesco.Id)
            .OrderBy(v => v.Ordem)
            .Select(v => v.Codigo)];
        codigosParentesco.Should().Equal(
            "PROPRIO_CANDIDATO", "CONJUGE_OU_COMPANHEIRO", "FILHO_OU_ENTEADO", "PAI_OU_MAE", "PADRASTO_OU_MADRASTA", "IRMAO",
            "AVO", "NETO", "SOGRO", "GENRO_OU_NORA", "OUTRO_PARENTE", "NAO_PARENTE");

        FatoCandidato documentoEstrangeiro = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "DOCUMENTO_ESTRANGEIRO_TIPO");
        valores.Where(v => v.FatoCandidatoId == documentoEstrangeiro.Id).OrderBy(v => v.Ordem).Select(v => v.Codigo)
            .Should().Equal("PASSAPORTE", "RNM");

        FatoCandidato estadoCivil = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "ESTADO_CIVIL");
        valores.Where(v => v.FatoCandidatoId == estadoCivil.Id).OrderBy(v => v.Ordem).Select(v => v.Codigo)
            .Should().Equal("SOLTEIRO", "CASADO", "UNIAO_ESTAVEL", "SEPARADO", "DIVORCIADO", "VIUVO");

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

        // Os fatos de sistema entre os listados: o catálogo também tem fatos do administrador semeados.
        IReadOnlyList<FatoCandidatoView> views =
            [.. (await reader.ListarAsync()).Where(static v => FatoCandidatoSeed.Itens.Any(i => i.Codigo == v.Codigo))];

        views.Should().HaveCount(45);
        views.Select(v => v.Codigo).Should().BeInAscendingOrder(StringComparer.Ordinal);

        FatoCandidatoView corRaca = views.Single(v => v.Codigo == "COR_RACA");
        corRaca.Dominio.Should().Be("CATEGORICO");
        corRaca.Origem.Should().Be("DECLARADO");
        corRaca.Cardinalidade.Should().Be("ESCALAR");
        corRaca.PontoResolucao.Should().Be("INSCRICAO");
        corRaca.Binding.Should().Be("CAMPO_FORMULARIO:COR_RACA");
        // A view projeta os códigos dos valores declarados em ValoresDominio, que o consumidor
        // cross-módulo (PredicadoDnfValidador) usa como domínio do categórico estático.
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
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at)
            VALUES ({Guid.CreateVersion7()}, {"COR_RACA"}, {"Duplicata"}, {"BOOLEANO"}, {"DECLARADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:DUPLICATA"}, {"CANDIDATO"}, {"PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow})
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
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"OUTRO"}, {"DECLARADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow})
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
                (id, codigo, nome, dominio, origem, cardinalidade, ponto_resolucao, binding, escopo, classificacao_protecao, finalidade_tratamento, hipotese_legal, sistema, ativo, created_at)
            VALUES ({Guid.CreateVersion7()}, {CodigoUnico()}, {"X"}, {"BOOLEANO"}, {"BRUTO_INFORMADO"}, {"ESCALAR"},
                {"INSCRICAO"}, {"CAMPO_FORMULARIO:X"}, {"CANDIDATO"}, {"PESSOAL"}, {"Teste"}, {"CUMPRIMENTO_OBRIGACAO_LEGAL"}, false, true, {DateTimeOffset.UtcNow})
            """);

        await act.Should().ThrowAsync<Npgsql.PostgresException>(
            "o CHECK ck_rol_de_fatos_candidato_origem bloqueia o token legado 'BRUTO_INFORMADO' (ADR-0116 renomeou para DECLARADO)");
    }

    [Fact(DisplayName = "Seed bate com o roster literal independente da ADR-0111/ADR-0116 (autoridade), não só com a própria fonte")]
    public async Task Seed_BateComRosterIndependente()
    {
        // Expectativa literal declarada aqui — independente de FatoCandidatoSeed.Itens.
        // Falha se a fonte do seed divergir da autoridade da ADR (código, id fixo,
        // domínio, origem, cardinalidade, ponto de resolução, binding ou valores),
        // não apenas se a migration divergir da fonte.
        (string Codigo, string IdSufixo, DominioFato Dominio, OrigemFato Origem, CardinalidadeFato Cardinalidade, string Binding, string PontoResolucao)[] esperado =
        [
            ("COR_RACA", "001", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:COR_RACA", "INSCRICAO"),
            ("QUILOMBOLA", "002", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:QUILOMBOLA", "INSCRICAO"),
            ("PCD", "003", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:PCD", "INSCRICAO"),
            ("EGRESSO_ESCOLA_PUBLICA", "004", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:EGRESSO_ESCOLA_PUBLICA", "INSCRICAO"),
            ("RENDA_PER_CAPITA", "005", DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, "ATRIBUTO_CANDIDATO:RENDA_PER_CAPITA", "INSCRICAO"),
            ("FAIXA_ETARIA", "006", DominioFato.Numerico, OrigemFato.Derivado, CardinalidadeFato.Escalar, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA", "INSCRICAO"),
            ("SEXO", "007", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:SEXO", "INSCRICAO"),
            ("MODALIDADE", "008", DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Multivalorado, "REGRA_DERIVACAO:MODALIDADE", "INSCRICAO"),
            ("CONDICAO_ATENDIMENTO", "009", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado, "CAMPO_FORMULARIO:CONDICAO_ATENDIMENTO", "INSCRICAO"),
            ("NACIONALIDADE", "010", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:NACIONALIDADE", "INSCRICAO"),
            ("TIPO_DEFICIENCIA", "011", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:TIPO_DEFICIENCIA", "INSCRICAO"),
            ("BAIXA_RENDA", "012", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:BAIXA_RENDA", "INSCRICAO"),
            ("CONCORRER_PCD", "013", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:CONCORRER_PCD", "INSCRICAO"),
            ("CONCORRER_EP", "014", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:CONCORRER_EP", "INSCRICAO"),
            ("CONCORRER_PPI", "015", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:CONCORRER_PPI", "INSCRICAO"),
            ("CONCORRER_Q", "016", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:CONCORRER_Q", "INSCRICAO"),
            ("CONCORRER_RENDA", "017", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:CONCORRER_RENDA", "INSCRICAO"),
            ("ENDERECO_RESIDENCIAL", "018", DominioFato.Endereco, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:ENDERECO_RESIDENCIAL", "INSCRICAO"),
            ("DATA_NASCIMENTO", "019", DominioFato.Data, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:DATA_NASCIMENTO", "INSCRICAO"),
            ("UF_RESIDENCIA", "020", DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, "ATRIBUTO_CANDIDATO:UF_RESIDENCIA", "INSCRICAO"),
            ("MUNICIPIO_RESIDENCIA", "021", DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, "ATRIBUTO_CANDIDATO:MUNICIPIO_RESIDENCIA", "INSCRICAO"),
            ("MODALIDADE_CONVOCACAO", "022", DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Escalar, "CLASSIFICACAO:MODALIDADE_CONVOCACAO", "RESULTADO_FINAL"),
            ("MAIOR_IDADE", "023", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:MAIOR_IDADE", "INSCRICAO"),
            ("SEM_RENDA", "024", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:SEM_RENDA", "INSCRICAO"),
            ("SOB_GUARDA", "025", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:SOB_GUARDA", "INSCRICAO"),
            ("PARENTESCO", "026", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:PARENTESCO", "INSCRICAO"),
            ("NOME", "027", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:NOME", "INSCRICAO"),
            ("DESEJA_NOME_SOCIAL", "028", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:DESEJA_NOME_SOCIAL", "INSCRICAO"),
            ("NOME_SOCIAL", "029", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:NOME_SOCIAL", "INSCRICAO"),
            ("CPF", "030", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:CPF", "INSCRICAO"),
            ("RG_NUMERO", "031", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RG_NUMERO", "INSCRICAO"),
            ("RG_ORGAO_EMISSOR", "032", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR", "INSCRICAO"),
            ("RG_DATA_EMISSAO", "033", DominioFato.Data, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RG_DATA_EMISSAO", "INSCRICAO"),
            ("DOCUMENTO_ESTRANGEIRO_TIPO", "034", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:DOCUMENTO_ESTRANGEIRO_TIPO", "INSCRICAO"),
            ("DOCUMENTO_ESTRANGEIRO_NUMERO", "035", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:DOCUMENTO_ESTRANGEIRO_NUMERO", "INSCRICAO"),
            ("NOME_MAE", "036", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:NOME_MAE", "INSCRICAO"),
            ("NOME_PAI", "037", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:NOME_PAI", "INSCRICAO"),
            ("ESTADO_CIVIL", "038", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:ESTADO_CIVIL", "INSCRICAO"),
            ("EMAIL", "039", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:EMAIL", "INSCRICAO"),
            ("TELEFONE", "040", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:TELEFONE", "INSCRICAO"),
            ("RG_UF", "041", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RG_UF", "INSCRICAO"),
            ("NATURALIDADE_UF", "042", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:NATURALIDADE_UF", "INSCRICAO"),
            ("NATURALIDADE_MUNICIPIO", "043", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:NATURALIDADE_MUNICIPIO", "INSCRICAO"),
            ("TIPO_ENDERECO", "044", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:TIPO_ENDERECO", "INSCRICAO"),
            ("NOME_COMUNIDADE", "045", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:NOME_COMUNIDADE", "INSCRICAO"),
        ];

        await using ConfiguracaoDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        fatos.Should().HaveCount(esperado.Length);

        // Os campos de membro da composição familiar; todos os demais são do candidato.
        HashSet<string> deMembroDeGrupo = ["MAIOR_IDADE", "SEM_RENDA", "SOB_GUARDA", "PARENTESCO"];

        // As dependências dos derivados do sistema (ADR-0136, UNI-REQ-0075); os demais fatos não têm.
        Dictionary<string, string[]> dependencias = new(StringComparer.Ordinal)
        {
            ["FAIXA_ETARIA"] = ["DATA_NASCIMENTO"],
            ["UF_RESIDENCIA"] = ["ENDERECO_RESIDENCIAL"],
            ["MUNICIPIO_RESIDENCIA"] = ["ENDERECO_RESIDENCIAL"],
        };

        foreach ((string codigo, string idSufixo, DominioFato dominio, OrigemFato origem, CardinalidadeFato cardinalidade, string binding, string pontoResolucao) in esperado)
        {
            FatoCandidato fato = fatos.Single(f => f.Codigo == codigo);
            fato.Id.Should().Be(Guid.Parse($"fa700000-0000-7000-8000-{idSufixo.PadLeft(12, '0')}"));
            fato.Dominio.Should().Be(dominio);
            fato.Origem.Should().Be(origem);
            fato.Cardinalidade.Should().Be(cardinalidade);
            fato.PontoResolucao.Should().Be(pontoResolucao);
            fato.Binding.Should().Be(binding);
            fato.Escopo.Should().Be(deMembroDeGrupo.Contains(codigo) ? EscopoFato.MembroGrupo : EscopoFato.Candidato);
            fato.Dependencias.Should().Equal(dependencias.GetValueOrDefault(codigo) ?? []);
        }
    }

    private static string CodigoUnico() => $"FATO_{Guid.NewGuid().ToString("N")[..12].ToUpperInvariant()}";
}
