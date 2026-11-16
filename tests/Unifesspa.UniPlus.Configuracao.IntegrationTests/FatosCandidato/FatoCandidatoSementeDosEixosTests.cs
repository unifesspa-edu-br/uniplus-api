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
/// O seed dos fatos de sistema: as regras padrão, a origem, o ponto de resolução, a cardinalidade,
/// os valores de domínio dos categóricos estáticos e o roster independente da ADR-0111/ADR-0116.
/// </summary>
[Collection(ConfiguracaoDbCollection.Name)]
public sealed class FatoCandidatoSementeDosEixosTests
{
    private readonly ConfiguracaoDbFixture _fixture;

    public FatoCandidatoSementeDosEixosTests(ConfiguracaoDbFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact(DisplayName = "Seed: as regras padrão dos derivados semeados passam pela conferência do catálogo")]
    public async Task Seed_RegrasPadrao_PassamPelaConferenciaDoCatalogo()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Include(static f => f.ValoresDominioDeclarados).ToListAsync();
        List<PrecedenciaFase> precedencias = await ctx.PrecedenciasFase.AsNoTracking().ToListAsync();
        CatalogoDeFatos catalogo = new(fatos, precedencias);

        foreach (FatoCandidato derivado in fatos.Where(static f => f.RegrasPadrao.Count > 0))
        {
            Result conferencia = ValidadorRegrasPadrao.Validar(derivado, derivado.RegrasPadrao, catalogo);

            conferencia.IsSuccess.Should().BeTrue(
                $"a regra padrão semeada de '{derivado.Codigo}' tem de valer no catálogo — "
                + string.Join("; ", conferencia.Errors.Select(static e => e.Error.Message)));
        }
    }

    [Fact(DisplayName = "Origem: os calculados de atributo, a modalidade e a modalidade da convocação são Derivado; os demais são Declarado (ADR-0116)")]
    public async Task Seed_OrigemReclassificadaConformeADR0116()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        string[] derivados = [.. fatos
            .Where(f => f.Origem == OrigemFato.Derivado)
            .Select(f => f.Codigo)
            .OrderBy(c => c, StringComparer.Ordinal)];

        // MODALIDADE passou a Derivado: não é resposta do candidato, e sim resultado da avaliação
        // dos fatos declarados contra as regras congeladas do processo (ADR-0116, emenda 2026-07-22).
        // O egresso de escola pública deriva das duas perguntas da origem escolar (UNI-REQ-0148).
        derivados.Should().Equal(
            "EGRESSO_ESCOLA_PUBLICA", "FAIXA_ETARIA", "MODALIDADE", "MODALIDADE_CONVOCACAO", "MODALIDADE_SEGUNDA_OPCAO",
            "MUNICIPIO_RESIDENCIA", "OPTOU_PELAS_COTAS_DA_LEI", "RENDA_PER_CAPITA", "UF_RESIDENCIA");
        fatos.Where(f => f.Origem != OrigemFato.Derivado)
            .Should().OnlyContain(f => f.Origem == OrigemFato.Declarado);
    }

    [Fact(DisplayName = "PontoResolucao: os fatos de sistema resolvem na inscrição, exceto a modalidade da convocação, no resultado final, e a renda do pedido de isenção, na solicitação de isenção")]
    public async Task Seed_PontoResolucaoInscricaoParaTodos()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        fatos.Where(static f => f.Codigo is not "MODALIDADE_CONVOCACAO" and not "RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO").Should().OnlyContain(static f => f.PontoResolucao == "INSCRICAO");
        fatos.Single(static f => f.Codigo == "MODALIDADE_CONVOCACAO").PontoResolucao.Should().Be("RESULTADO_FINAL");
        fatos.Single(static f => f.Codigo == "RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO").PontoResolucao.Should().Be("SOLICITACAO_ISENCAO");
    }

    [Fact(DisplayName = "Cardinalidade: só MODALIDADE, MODALIDADE_SEGUNDA_OPCAO, CONDICAO_ATENDIMENTO e RECURSO_ACESSIBILIDADE são multivalorados; os demais escalares")]
    public async Task Seed_CardinalidadeCoerente()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        List<FatoCandidato> fatos = await ctx.FatosCandidato.AsNoTracking().Where(static f => f.Sistema).ToListAsync();

        string[] multivalorados = [.. fatos
            .Where(f => f.Cardinalidade == CardinalidadeFato.Multivalorado)
            .Select(f => f.Codigo)
            .OrderBy(c => c, StringComparer.Ordinal)];

        multivalorados.Should().Equal("CONDICAO_ATENDIMENTO", "MODALIDADE", "MODALIDADE_SEGUNDA_OPCAO", "RECURSO_ACESSIBILIDADE");
        fatos.Where(f => f.Cardinalidade != CardinalidadeFato.Multivalorado)
            .Should().OnlyContain(f => f.Cardinalidade == CardinalidadeFato.Escalar);
    }

    [Fact(DisplayName = "Seed de FatoValorDominio: os categóricos estáticos de sistema têm os valores filhos esperados")]
    public async Task Seed_FatoValorDominio_MaterializaOsValoresDosCategoricosEstaticos()
    {
        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);

        // Só os valores dos fatos de sistema: o catálogo também tem fatos do administrador semeados.
        List<FatoValorDominio> valores = await ctx.FatosValorDominio.AsNoTracking()
            .Where(v => ctx.Set<FatoCandidato>().Any(f => f.Id == v.FatoCandidatoId && f.Sistema))
            .ToListAsync();
        valores.Should().HaveCount(FatoValorDominioSeed.Itens.Count).And.HaveCount(80);

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

        FatoCandidato estadoCivil = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "ESTADO_CIVIL");
        valores.Where(v => v.FatoCandidatoId == estadoCivil.Id).OrderBy(v => v.Ordem).Select(v => v.Codigo)
            .Should().Equal("SOLTEIRO", "CASADO", "UNIAO_ESTAVEL", "SEPARADO", "DIVORCIADO", "VIUVO");

        // O órgão emissor do RG: a lista fechada termina em "Outro", que pede o nome do órgão.
        FatoCandidato orgaoEmissor = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "RG_ORGAO_EMISSOR");
        valores.Where(v => v.FatoCandidatoId == orgaoEmissor.Id).OrderBy(v => v.Ordem).Select(v => v.Codigo)
            .Should().Equal("SSP", "PC", "DETRAN", "IFP", "PM", "CBM", "EB", "MB", "FAB", "DPF", "MRE", "OUTRO");

        FatoCandidato formaDeConclusao = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "FORMA_CONCLUSAO_EM");
        valores.Where(v => v.FatoCandidatoId == formaDeConclusao.Id).OrderBy(v => v.Ordem).Select(v => v.Codigo)
            .Should().Equal("REGULAR", "EJA", "ENCCEJA", "PROFICIENCIA", "ENEM");

        // A parte em escola privada com bolsa integral fica separada da parte fora da rede pública sem
        // ela, e "Concluí por certificação" é o último valor.
        FatoCandidato ondeCursou = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "ONDE_CURSOU_EM");
        valores.Where(v => v.FatoCandidatoId == ondeCursou.Id).OrderBy(v => v.Ordem).Select(v => v.Codigo)
            .Should().Equal(
                "SOMENTE_PUBLICA", "COMUNITARIA_CAMPO", "PRIVADA_BOLSA_INTEGRAL", "PRIVADA_BOLSA_PARCIAL", "PRIVADA",
                "PUBLICA_E_PRIVADA_BOLSA_INTEGRAL", "PUBLICA_E_FORA_DA_REDE", "PUBLICA_E_COMUNITARIA", "SISTEMA_S", "EXTERIOR",
                "MAIS_DE_UM_FORA_DA_REDE", "CERTIFICACAO");

        FatoCandidato modalidade = await ctx.FatosCandidato.AsNoTracking().SingleAsync(f => f.Codigo == "MODALIDADE");
        valores.Should().NotContain(v => v.FatoCandidatoId == modalidade.Id,
            "MODALIDADE é escopo-processo — não tem FatoValorDominio filhos");
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
            ("EGRESSO_ESCOLA_PUBLICA", "004", DominioFato.Booleano, OrigemFato.Derivado, CardinalidadeFato.Escalar, "REGRA_DERIVACAO:EGRESSO_ESCOLA_PUBLICA", "INSCRICAO"),
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
            ("RG_ORGAO_EMISSOR", "032", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR", "INSCRICAO"),
            ("RG_DATA_EMISSAO", "033", DominioFato.Data, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RG_DATA_EMISSAO", "INSCRICAO"),
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
            ("FORMA_CONCLUSAO_EM", "046", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:FORMA_CONCLUSAO_EM", "INSCRICAO"),
            ("ONDE_CURSOU_EM", "047", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:ONDE_CURSOU_EM", "INSCRICAO"),
            ("RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO", "048", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RENDA_ATE_UM_SALARIO_MINIMO_E_MEIO", "SOLICITACAO_ISENCAO"),
            ("EDICAO_ENEM", "049", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:EDICAO_ENEM", "INSCRICAO"),
            ("INDIGENA", "050", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:INDIGENA", "INSCRICAO"),
            ("PASSAPORTE_NUMERO", "051", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:PASSAPORTE_NUMERO", "INSCRICAO"),
            ("RNM_NUMERO", "052", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RNM_NUMERO", "INSCRICAO"),
            ("RECURSO_ACESSIBILIDADE", "053", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Multivalorado, "CAMPO_FORMULARIO:RECURSO_ACESSIBILIDADE", "INSCRICAO"),
            ("OPTOU_PELAS_COTAS_DA_LEI", "054", DominioFato.Booleano, OrigemFato.Derivado, CardinalidadeFato.Escalar, "REGRA_DERIVACAO:OPTOU_PELAS_COTAS_DA_LEI", "INSCRICAO"),
            ("CONCORRER_VAGA_POR_ACRESCIMO", "055", DominioFato.Booleano, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:CONCORRER_VAGA_POR_ACRESCIMO", "INSCRICAO"),
            ("RG_ORGAO_EMISSOR_OUTRO", "056", DominioFato.Texto, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:RG_ORGAO_EMISSOR_OUTRO", "INSCRICAO"),
            ("SEXO_CIVIL", "057", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:SEXO_CIVIL", "INSCRICAO"),
            ("IDENTIDADE_GENERO", "058", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:IDENTIDADE_GENERO", "INSCRICAO"),
            ("ORIENTACAO_SEXUAL", "059", DominioFato.Categorico, OrigemFato.Declarado, CardinalidadeFato.Escalar, "CAMPO_FORMULARIO:ORIENTACAO_SEXUAL", "INSCRICAO"),
            ("MODALIDADE_SEGUNDA_OPCAO", "060", DominioFato.Categorico, OrigemFato.Derivado, CardinalidadeFato.Multivalorado, "ATRIBUTO_CANDIDATO:MODALIDADE_SEGUNDA_OPCAO", "INSCRICAO"),
        ];

        await using UniPlusDbContext ctx = _fixture.CreateDbContext(userId: null);
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

        // O egresso de escola pública chega ao banco com a regra padrão que deriva das duas perguntas.
        fatos.Single(static f => f.Codigo == "EGRESSO_ESCOLA_PUBLICA").RegrasPadrao.SelectMany(static r => r.FatosCitados).Distinct()
            .Should().BeEquivalentTo(["FORMA_CONCLUSAO_EM", "ONDE_CURSOU_EM"]);
    }
}
