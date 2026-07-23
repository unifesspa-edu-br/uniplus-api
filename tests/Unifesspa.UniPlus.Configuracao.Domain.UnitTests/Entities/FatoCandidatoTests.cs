namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using System.Reflection;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Domain.Entities;
using Unifesspa.UniPlus.Kernel.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Invariantes de domínio do catálogo <c>FatoCandidato</c> (ADR-0111, refinada pela
/// ADR-0116): a factory valida código, domínio, origem, cardinalidade, ponto de
/// resolução, binding e a coerência de <c>ValoresDominio</c> com o domínio; a
/// entidade não expõe mutação além de <c>AdicionarValorDominio</c>.
/// </summary>
public sealed class FatoCandidatoTests
{
    private const string BindingCorRaca = "CAMPO_INSCRICAO:COR_RACA";
    private const string PontoResolucaoInscricao = "INSCRICAO";

    private static Result<FatoCandidato> Criar(
        string codigo = "COR_RACA",
        string nome = "Cor ou raça",
        string? descricao = null,
        DominioFato dominio = DominioFato.Categorico,
        OrigemFato origem = OrigemFato.Declarado,
        CardinalidadeFato cardinalidade = CardinalidadeFato.Escalar,
        IReadOnlyList<string>? valoresDominio = null,
        string pontoResolucao = PontoResolucaoInscricao,
        string binding = BindingCorRaca) =>
        FatoCandidato.Criar(codigo, nome, descricao, dominio, origem, cardinalidade, valoresDominio, pontoResolucao, binding);

    [Fact(DisplayName = "Criar categórico válido preenche os campos com Guid v7")]
    public void Criar_CategoricoValido_Preenche()
    {
        FatoCandidato fato = Criar(descricao: "Cor ou raça autodeclarada").Value!;

        fato.Id.Should().NotBe(Guid.Empty);
        fato.Codigo.Should().Be("COR_RACA");
        fato.Nome.Should().Be("Cor ou raça");
        fato.Descricao.Should().Be("Cor ou raça autodeclarada");
        fato.Dominio.Should().Be(DominioFato.Categorico);
        fato.Origem.Should().Be(OrigemFato.Declarado);
        fato.Cardinalidade.Should().Be(CardinalidadeFato.Escalar);
        fato.PontoResolucao.Should().Be("INSCRICAO");
        fato.Binding.Should().Be(BindingCorRaca);
    }

    [Theory(DisplayName = "Código ausente, em branco ou fora do formato é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("cor_raca")]      // minúsculo
    [InlineData("1COR")]          // começa por dígito
    [InlineData("COR-RACA")]      // hífen
    [InlineData("A")]             // curto demais (< 2)
    public void Criar_CodigoInvalido_Falha(string codigo)
    {
        Result<FatoCandidato> resultado = Criar(codigo: codigo);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().StartWith("FatoCandidato.Codigo");
    }

    [Fact(DisplayName = "Nome ausente é rejeitado")]
    public void Criar_SemNome_Falha()
    {
        Result<FatoCandidato> resultado = Criar(nome: "   ");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.NomeObrigatorio);
    }

    [Fact(DisplayName = "Domínio Nenhum (não decidível — ex.: 'texto') é rejeitado")]
    public void Criar_DominioNenhum_Falha()
    {
        Result<FatoCandidato> resultado = Criar(dominio: DominioFato.Nenhum, valoresDominio: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.DominioObrigatorio);
    }

    [Fact(DisplayName = "Domínio fora do roster (cast inválido) é rejeitado como inválido, não aceito")]
    public void Criar_DominioForaDoRoster_Falha()
    {
        Result<FatoCandidato> resultado = Criar(dominio: (DominioFato)999, valoresDominio: null);

        resultado.IsFailure.Should().BeTrue("a factory não pode delegar a rejeição ao converter (que lançaria 500)");
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.DominioInvalido);
    }

    [Fact(DisplayName = "Origem fora do roster (cast inválido) é rejeitada como inválida")]
    public void Criar_OrigemForaDoRoster_Falha()
    {
        Result<FatoCandidato> resultado = Criar(origem: (OrigemFato)999, valoresDominio: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.OrigemInvalida);
    }

    [Fact(DisplayName = "Cardinalidade fora do roster (cast inválido) é rejeitada como inválida")]
    public void Criar_CardinalidadeForaDoRoster_Falha()
    {
        Result<FatoCandidato> resultado = Criar(cardinalidade: (CardinalidadeFato)999, valoresDominio: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.CardinalidadeInvalida);
    }

    [Fact(DisplayName = "Origem Nenhuma é rejeitada")]
    public void Criar_OrigemNenhuma_Falha()
    {
        Result<FatoCandidato> resultado = Criar(origem: OrigemFato.Nenhuma);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.OrigemObrigatoria);
    }

    [Fact(DisplayName = "Cardinalidade Nenhuma é rejeitada")]
    public void Criar_CardinalidadeNenhuma_Falha()
    {
        Result<FatoCandidato> resultado = Criar(cardinalidade: CardinalidadeFato.Nenhuma);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.CardinalidadeObrigatoria);
    }

    [Theory(DisplayName = "Fato não-categórico com valores de domínio é rejeitado")]
    [InlineData(DominioFato.Booleano)]
    [InlineData(DominioFato.Numerico)]
    public void Criar_NaoCategoricoComValores_Falha(DominioFato dominio)
    {
        Result<FatoCandidato> resultado = Criar(dominio: dominio, valoresDominio: ["SIM", "NAO"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.ValoresDominioNaoPermitidosForaDeCategorico);
    }

    [Theory(DisplayName = "Fato não-categórico sem valores é aceito")]
    [InlineData(DominioFato.Booleano)]
    [InlineData(DominioFato.Numerico)]
    public void Criar_NaoCategoricoSemValores_Aceita(DominioFato dominio)
    {
        FatoCandidato fato = Criar(
            codigo: "PCD", nome: "PcD", dominio: dominio, valoresDominio: null,
            binding: "CAMPO_INSCRICAO:PCD").Value!;

        fato.ValoresDominio.Should().BeNull();
    }

    [Fact(DisplayName = "Categórico sem valores é aceito (escopo-processo) e preserva o nulo")]
    public void Criar_CategoricoSemValores_AceitaEscopoProcesso()
    {
        FatoCandidato fato = Criar(
            codigo: "MODALIDADE",
            nome: "Modalidade de concorrência",
            dominio: DominioFato.Categorico,
            origem: OrigemFato.Derivado,
            cardinalidade: CardinalidadeFato.Multivalorado,
            valoresDominio: null,
            binding: "REGRA_DERIVACAO:MODALIDADE").Value!;

        fato.ValoresDominio.Should().BeNull("categórico sem valores é de escopo-processo, não lista vazia");
    }

    [Fact(DisplayName = "Categórico com lista vazia de valores é rejeitado (nulo ≠ vazio)")]
    public void Criar_CategoricoListaVazia_Falha()
    {
        Result<FatoCandidato> resultado = Criar(valoresDominio: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.ValoresDominioComItemEmBranco);
    }

    [Theory(DisplayName = "Valores com item em branco são rejeitados")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_ValoresComItemEmBranco_Falha(string branco)
    {
        Result<FatoCandidato> resultado = Criar(valoresDominio: ["BRANCA", branco]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.ValoresDominioComItemEmBranco);
    }

    [Fact(DisplayName = "Valores com duplicata são rejeitados")]
    public void Criar_ValoresComDuplicata_Falha()
    {
        Result<FatoCandidato> resultado = Criar(valoresDominio: ["BRANCA", "PRETA", "BRANCA"]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.ValoresDominioComDuplicata);
    }

    // ─── PontoResolucao (ADR-0116) ──────────────────────────────────────────

    [Theory(DisplayName = "Ponto de resolução ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_PontoResolucaoAusente_Falha(string ponto)
    {
        Result<FatoCandidato> resultado = Criar(pontoResolucao: ponto);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoObrigatorio);
    }

    [Fact(DisplayName = "Ponto de resolução fora do conjunto canônico das quatorze fases é rejeitado")]
    public void Criar_PontoResolucaoForaDoCanonico_Falha()
    {
        Result<FatoCandidato> resultado = Criar(pontoResolucao: "FASE_INEXISTENTE");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.PontoResolucaoInvalido);
    }

    // ─── Binding (ADR-0116) ─────────────────────────────────────────────────

    [Theory(DisplayName = "Binding ausente ou em branco é rejeitado")]
    [InlineData("")]
    [InlineData("   ")]
    public void Criar_BindingAusente_Falha(string binding)
    {
        Result<FatoCandidato> resultado = Criar(binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingObrigatorio);
    }

    [Theory(DisplayName = "Binding sem separador, ou com prefixo/referência vazios, é rejeitado como formato inválido")]
    [InlineData("CAMPO_INSCRICAO_COR_RACA")]
    [InlineData(":COR_RACA")]
    [InlineData("CAMPO_INSCRICAO:")]
    public void Criar_BindingFormatoInvalido_Falha(string binding)
    {
        Result<FatoCandidato> resultado = Criar(binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingFormatoInvalido);
    }

    [Theory(DisplayName = "Binding com prefixo incoerente com a origem é rejeitado")]
    [InlineData(OrigemFato.Declarado, "ATRIBUTO_CANDIDATO:COR_RACA")]
    [InlineData(OrigemFato.Declarado, "REGRA_DERIVACAO:MODALIDADE")]
    [InlineData(OrigemFato.Derivado, "CAMPO_INSCRICAO:FAIXA_ETARIA")]
    [InlineData(OrigemFato.Integracao, "CAMPO_INSCRICAO:ANO_ENEM")]
    [InlineData(OrigemFato.Integracao, "REGRA_DERIVACAO:ALGO")]
    public void Criar_BindingPrefixoIncoerenteComOrigem_Falha(OrigemFato origem, string binding)
    {
        Result<FatoCandidato> resultado = Criar(origem: origem, binding: binding);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingPrefixoIncoerenteComOrigem);
    }

    [Fact(DisplayName = "REGRA_DERIVACAO cuja referência não é o código do próprio fato é recusado")]
    public void Criar_BindingRegraDerivacaoReferenciaOutroFato_Falha()
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "FAIXA_ETARIA", origem: OrigemFato.Derivado, binding: "REGRA_DERIVACAO:MODALIDADE");

        resultado.IsFailure.Should().BeTrue(
            "a regra de derivação referenciada é a do próprio fato — apontar para outro congelaria metadado de derivação alheio");
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.BindingReferenciaRegraIncoerente);
    }

    [Fact(DisplayName = "REGRA_DERIVACAO cuja referência é o código do próprio fato é aceito")]
    public void Criar_BindingRegraDerivacaoReferenciaProprioFato_Aceita()
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "MODALIDADE", dominio: DominioFato.Categorico, valoresDominio: null,
            cardinalidade: CardinalidadeFato.Multivalorado,
            origem: OrigemFato.Derivado, binding: "REGRA_DERIVACAO:MODALIDADE");

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "A mensagem de prefixo incoerente lista os dois prefixos aceitos quando a origem é derivada")]
    public void Criar_BindingPrefixoIncoerente_Derivado_MensagemListaOsDois()
    {
        Result<FatoCandidato> resultado = Criar(origem: OrigemFato.Derivado, binding: "CAMPO_INSCRICAO:X");

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Message.Should().Contain("ATRIBUTO_CANDIDATO")
            .And.Contain("REGRA_DERIVACAO",
                "a origem derivada aceita dois prefixos — a mensagem precisa apontar ambos, não um só");
    }

    [Theory(DisplayName = "Binding com prefixo coerente com a origem é aceito")]
    [InlineData(OrigemFato.Declarado, "CAMPO_INSCRICAO:COR_RACA")]
    [InlineData(OrigemFato.Derivado, "ATRIBUTO_CANDIDATO:FAIXA_ETARIA")]
    // REGRA_DERIVACAO referencia o próprio fato: a referência bate com o código FATO_QUALQUER.
    [InlineData(OrigemFato.Derivado, "REGRA_DERIVACAO:FATO_QUALQUER")]
    [InlineData(OrigemFato.Integracao, "INTEGRACAO:ANO_ENEM")]
    public void Criar_BindingPrefixoCoerenteComOrigem_Aceita(OrigemFato origem, string binding)
    {
        Result<FatoCandidato> resultado = Criar(
            codigo: "FATO_QUALQUER", dominio: DominioFato.Numerico, valoresDominio: null,
            origem: origem, binding: binding);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        resultado.Value!.Binding.Should().Be(binding);
    }

    // ─── AdicionarValorDominio (ADR-0116) ───────────────────────────────────

    [Fact(DisplayName = "AdicionarValorDominio em categórico Declarado exige e aceita descrição")]
    public void AdicionarValorDominio_CategoricoDeclarado_Aceita()
    {
        FatoCandidato fato = Criar(valoresDominio: null).Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", "Autodeclaração de cor/raça preta.", 0, ativo: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        fato.ValoresDominioDeclarados.Should().ContainSingle(v =>
            v.Codigo == "PRETA" && v.Descricao == "Autodeclaração de cor/raça preta." && v.Ordem == 0 && v.Ativo);
    }

    [Fact(DisplayName = "AdicionarValorDominio fora de categórico é rejeitado")]
    public void AdicionarValorDominio_ForaDeCategorico_Falha()
    {
        FatoCandidato fato = Criar(
            codigo: "PCD", dominio: DominioFato.Booleano, valoresDominio: null,
            binding: "CAMPO_INSCRICAO:PCD").Value!;

        Result resultado = fato.AdicionarValorDominio("SIM", "Descrição", 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.NaoPermitidoForaDeCategorico);
    }

    [Fact(DisplayName = "AdicionarValorDominio com código duplicado (normalizado, ordinal) é rejeitado")]
    public void AdicionarValorDominio_CodigoDuplicado_Falha()
    {
        FatoCandidato fato = Criar(valoresDominio: null).Value!;
        fato.AdicionarValorDominio("PRETA", "Descrição", 0, ativo: true).IsSuccess.Should().BeTrue();

        Result resultado = fato.AdicionarValorDominio("  PRETA  ", "Outra descrição", 1, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.CodigoDuplicado);
    }

    [Fact(DisplayName = "AdicionarValorDominio sem descrição quando a origem é Declarado é rejeitado")]
    public void AdicionarValorDominio_SemDescricaoDeclarado_Falha()
    {
        FatoCandidato fato = Criar(valoresDominio: null).Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", null, 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.DescricaoObrigatoria);
    }

    [Fact(DisplayName = "AdicionarValorDominio sem descrição quando a origem é Derivado é aceito")]
    public void AdicionarValorDominio_SemDescricaoDerivado_Aceita()
    {
        FatoCandidato fato = Criar(
            codigo: "FATO_DERIVADO", dominio: DominioFato.Categorico, valoresDominio: null,
            origem: OrigemFato.Derivado, binding: "ATRIBUTO_CANDIDATO:FATO_DERIVADO").Value!;

        Result resultado = fato.AdicionarValorDominio("X", null, 0, ativo: true);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
    }

    [Fact(DisplayName = "AdicionarValorDominio com código em branco é rejeitado")]
    public void AdicionarValorDominio_CodigoEmBranco_Falha()
    {
        FatoCandidato fato = Criar(valoresDominio: null).Value!;

        Result resultado = fato.AdicionarValorDominio("   ", "Descrição", 0, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.CodigoObrigatorio);
    }

    [Fact(DisplayName = "AdicionarValorDominio com ordem negativa é rejeitado")]
    public void AdicionarValorDominio_OrdemNegativa_Falha()
    {
        FatoCandidato fato = Criar(valoresDominio: null).Value!;

        Result resultado = fato.AdicionarValorDominio("PRETA", "Descrição", -1, ativo: true);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoValorDominioErrorCodes.OrdemInvalida);
    }

    // ─── Imutabilidade (seed-governado, EntityBase puro, sem mutação além de AdicionarValorDominio) ──

    [Fact(DisplayName = "FatoCandidato deriva de EntityBase puro — não é soft-deletable")]
    public void FatoCandidato_EhEntityBasePuro()
    {
        typeof(EntityBase).IsAssignableFrom(typeof(FatoCandidato)).Should().BeTrue();
        typeof(ISoftDeletable).IsAssignableFrom(typeof(FatoCandidato)).Should().BeFalse(
            "o catálogo é append-only e seed-governado — nunca removido logicamente");
    }

    [Fact(DisplayName = "Todas as propriedades do FatoCandidato têm setter não-público (imutável)")]
    public void FatoCandidato_PropriedadesImutaveis()
    {
        PropertyInfo[] propriedades = typeof(FatoCandidato)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo propriedade in propriedades)
        {
            MethodInfo? setter = propriedade.GetSetMethod(nonPublic: false);
            setter.Should().BeNull($"a propriedade '{propriedade.Name}' não pode ter setter público (entidade imutável)");
        }
    }

    [Fact(DisplayName = "A única mutação de instância do FatoCandidato é AdicionarValorDominio (ADR-0116)")]
    public void FatoCandidato_UnicaMutacaoEhAdicionarValorDominio()
    {
        MethodInfo[] metodos = typeof(FatoCandidato)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName) // exclui getters/setters de propriedade
            .ToArray();

        metodos.Select(m => m.Name).Should().BeEquivalentTo(
            [nameof(FatoCandidato.AdicionarValorDominio)],
            "a factory estática Criar constrói o agregado, e a única mutação em runtime "
            + "é montar o conjunto de valores de domínio (ex.: pelo seed) — nunca em runtime de requisição");
    }
}
