namespace Unifesspa.UniPlus.Configuracao.IntegrationTests.FatosCandidato;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Configuracao.Application.Persistence;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Repositories;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Persistence.Seed;
using Unifesspa.UniPlus.Configuracao.Infrastructure.Readers;
using Unifesspa.UniPlus.Configuracao.IntegrationTests.Infrastructure;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;

/// <summary>
/// O seed dos fatos de sistema no banco: o que a factory aceita, os pares de cota, a identidade semeada,
/// a classificação de proteção e as opções fixas dos fatos de sexo, gênero e orientação.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class FatoCandidatoSementeTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public FatoCandidatoSementeTests(ConfiguracaoDbFixture fixture)
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

    [Theory(DisplayName = "CA-01 — os fatos de sistema de número de documento são semeados como Identificador")]
    [InlineData("CPF")]
    [InlineData("RG_NUMERO")]
    [InlineData("PASSAPORTE_NUMERO")]
    [InlineData("RNM_NUMERO")]
    public void Seed_FatoDeNumeroDeDocumento_EhIdentificador(string codigo) =>
        FatoCandidatoSeed.Itens.Single(i => i.Codigo == codigo).ClassificacaoProtecao
            .Should().Be(ClassificacaoDeProtecao.Identificador);

    [Fact(DisplayName = "Cada cota tem par elegibilidade + opt-in como fatos independentes (UNI-REQ-0078)")]
    public async Task Seed_CadaCotaTemParElegibilidadeEOptIn()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.SemeadoPeloSistema).ToListAsync();

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
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.SemeadoPeloSistema).ToListAsync();

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
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

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
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.SemeadoPeloSistema).ToListAsync();

        fatos.Should().HaveCount(FatoCandidatoSeed.Itens.Count).And.HaveCount(58);
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
            persistido.SemeadoPeloSistema.Should().BeTrue($"{item.Codigo} é semeado pelo sistema");
            persistido.Ativo.Should().BeTrue();
        }
    }

    [Fact(DisplayName = "Sexo civil, identidade de gênero e orientação sexual são fatos de sistema com as opções fixas da issue, e o sexo passa a biológico")]
    public async Task Seed_SexoCivilIdentidadeDeGeneroEOrientacaoSexual_TemAsOpcoesFixas()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        FatoCandidatoReader reader = new(ctx);

        Dictionary<string, string[]> esperado = new(StringComparer.Ordinal)
        {
            ["SEXO_CIVIL"] = ["FEMININO", "MASCULINO"],
            ["IDENTIDADE_GENERO"] = ["PESSOA_CIS", "PESSOA_TRANSGENERO", "TRAVESTI", "TRANSEXUAL", "PESSOA_NAO_BINARIA", "PREFIRO_NAO_INFORMAR"],
            ["ORIENTACAO_SEXUAL"] = ["HETEROSSEXUAL", "HOMOSSEXUAL", "BISSEXUAL", "ASSEXUAL", "PANSEXUAL", "PREFIRO_NAO_INFORMAR"],
        };

        foreach ((string codigo, string[] opcoes) in esperado)
        {
            FatoCandidato fato = await ctx.FatosCandidato.AsNoTracking().Include(f => f.ValoresDominioDeclarados).SingleAsync(f => f.Codigo == codigo);
            fato.SemeadoPeloSistema.Should().BeTrue($"{codigo} é fato de sistema: o administrador não acrescenta nem desativa opção");
            fato.ValoresDominioDeclarados.OrderBy(v => v.Ordem).Select(v => v.Codigo).Should().Equal(opcoes);
            fato.ValoresDominioDeclarados.Should().OnlyContain(v => v.Ativo);

            Result acrescimo = fato.AdicionarValorDominio("OUTRA", "Outra opção", 9, true);
            acrescimo.IsFailure.Should().BeTrue($"o vocabulário de {codigo} é normativo");
        }

        FatoCandidatoView genero = (await reader.ObterPorCodigoAsync("IDENTIDADE_GENERO"))!;
        genero.ValoresDominioDeclarados!.Single(v => v.Codigo == "PESSOA_NAO_BINARIA").Descricao.Should().Be("Pessoa não binária");
        genero.ValoresDominioDeclarados!.Single(v => v.Codigo == "PREFIRO_NAO_INFORMAR").Descricao.Should().Be("Prefiro não informar");

        FatoCandidato sexo = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "SEXO");
        sexo.Nome.Should().Be("Sexo biológico");
    }

    [Fact(DisplayName = "O sexo civil é pessoal; a identidade de gênero e a orientação sexual são sensíveis")]
    public async Task Seed_SexoCivilIdentidadeDeGeneroEOrientacaoSexual_TemAClassificacaoDaIssue()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Dictionary<string, ClassificacaoDeProtecao> classificacoes = await ctx.FatosCandidato.AsNoTracking()
            .Where(f => f.Codigo == "SEXO" || f.Codigo == "SEXO_CIVIL" || f.Codigo == "IDENTIDADE_GENERO" || f.Codigo == "ORIENTACAO_SEXUAL")
            .ToDictionaryAsync(f => f.Codigo, f => f.ClassificacaoProtecao);

        classificacoes.Should().BeEquivalentTo(new Dictionary<string, ClassificacaoDeProtecao>
        {
            ["SEXO"] = ClassificacaoDeProtecao.Pessoal,
            ["SEXO_CIVIL"] = ClassificacaoDeProtecao.Pessoal,
            ["IDENTIDADE_GENERO"] = ClassificacaoDeProtecao.Sensivel,
            ["ORIENTACAO_SEXUAL"] = ClassificacaoDeProtecao.Sensivel,
        });
    }

    [Fact(DisplayName = "O NOME, que vem do login, não tem formato: abreviatura com ponto, uma palavra só e dígito passam")]
    public async Task Seed_NomeDoLogin_NaoPassaPorRegraDeFormato()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        FatoCandidato nome = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "NOME");

        nome.Formato.Should().Be(FormatoTexto.Livre);
        string token = FormatosTexto.ParaTokenCanonico(nome.Formato!.Value);
        foreach (string civil in new[] { "Maria S. Silva", "Madonna", "Helena Souza Barros 2º" })
        {
            new FormatoDeTexto(token).FormaCanonica(civil).Should().Be(civil);
        }
    }

    [Fact(DisplayName = "O nome social, o da mãe e o do pai, digitados pelo candidato, seguem com o formato de nome de pessoa")]
    public async Task Seed_NomesDigitados_SeguemComFormatoDeNomeDePessoa()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        Dictionary<string, FormatoTexto?> formatos = await ctx.FatosCandidato.AsNoTracking()
            .Where(f => f.Codigo == "NOME_SOCIAL" || f.Codigo == "NOME_MAE" || f.Codigo == "NOME_PAI")
            .ToDictionaryAsync(f => f.Codigo, f => f.Formato);

        formatos.Values.Should().OnlyContain(f => f == FormatoTexto.NomePessoa).And.HaveCount(3);
    }

    [Fact(DisplayName = "Cor ou raça, deficiência, tipo de deficiência e atendimento são sensíveis; a modalidade e as opções de concorrer, internas; CPF, RG e documento do estrangeiro são identificador; os demais, pessoais")]
    public async Task Seed_ClassificacaoDeProtecao()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.SemeadoPeloSistema).ToListAsync();

        fatos.Where(f => f.ClassificacaoProtecao == ClassificacaoDeProtecao.Sensivel)
            .Select(f => f.Codigo).Order(StringComparer.Ordinal).Should().Equal(
                "CONDICAO_ATENDIMENTO", "COR_RACA", "IDENTIDADE_GENERO", "INDIGENA", "ORIENTACAO_SEXUAL", "PCD", "QUILOMBOLA",
                "RECURSO_ACESSIBILIDADE", "TIPO_DEFICIENCIA");
        fatos.Where(f => f.ClassificacaoProtecao == ClassificacaoDeProtecao.Interno)
            .Select(f => f.Codigo).Order(StringComparer.Ordinal).Should().Equal(
                "CONCORRER_EP", "CONCORRER_PCD", "CONCORRER_PPI", "CONCORRER_Q", "CONCORRER_RENDA", "CONCORRER_VAGA_POR_ACRESCIMO",
                "MODALIDADE", "MODALIDADE_CONVOCACAO", "MODALIDADE_SEGUNDA_OPCAO");
        fatos.Should().NotContain(f => f.ClassificacaoProtecao == ClassificacaoDeProtecao.Publico);
        fatos.Single(f => f.Codigo == "NOME_SOCIAL").ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Pessoal);
        fatos.Where(f => f.ClassificacaoProtecao == ClassificacaoDeProtecao.Identificador)
            .Select(f => f.Codigo).Order(StringComparer.Ordinal).Should().Equal(
                "CPF", "PASSAPORTE_NUMERO", "RG_NUMERO", "RNM_NUMERO");
        fatos.Where(f => f.ClassificacaoProtecao is not (
                ClassificacaoDeProtecao.Sensivel or ClassificacaoDeProtecao.Interno or ClassificacaoDeProtecao.Publico or ClassificacaoDeProtecao.Identificador))
            .Should().OnlyContain(f => f.ClassificacaoProtecao == ClassificacaoDeProtecao.Pessoal);
    }

    [Fact(DisplayName = "A modalidade da 2ª opção de curso é fato de sistema derivado, interno e multivalorado, como a modalidade")]
    public async Task Seed_ModalidadeDaSegundaOpcao_EhDerivadaEInterna()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        FatoCandidato fato = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "MODALIDADE_SEGUNDA_OPCAO");

        fato.SemeadoPeloSistema.Should().BeTrue();
        fato.Origem.Should().Be(OrigemFato.Derivado);
        fato.Cardinalidade.Should().Be(CardinalidadeFato.Multivalorado);
        fato.FonteValores.Should().Be(FonteValoresFato.Modalidade);
        fato.ClassificacaoProtecao.Should().Be(ClassificacaoDeProtecao.Interno);
    }
}
