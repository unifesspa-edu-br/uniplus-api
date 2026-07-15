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
/// Invariantes de domínio do catálogo <c>FatoCandidato</c> (ADR-0111): a factory
/// valida código, domínio, natureza, cardinalidade e a coerência de
/// <c>ValoresDominio</c> com o domínio; a entidade é imutável (seed-governada,
/// sem soft-delete, sem mutação em runtime).
/// </summary>
public sealed class FatoCandidatoTests
{
    private static readonly IReadOnlyList<string> ValoresCorRaca =
        ["BRANCA", "PRETA", "PARDA", "AMARELA", "INDIGENA", "NAO_INFORMADO"];

    private static Result<FatoCandidato> Criar(
        string codigo = "COR_RACA",
        string nome = "Cor ou raça",
        string? descricao = null,
        DominioFato dominio = DominioFato.Categorico,
        NaturezaFato natureza = NaturezaFato.BrutoInformado,
        CardinalidadeFato cardinalidade = CardinalidadeFato.Escalar,
        IReadOnlyList<string>? valoresDominio = null) =>
        FatoCandidato.Criar(codigo, nome, descricao, dominio, natureza, cardinalidade, valoresDominio);

    [Fact(DisplayName = "Criar categórico estático válido preenche os campos com Guid v7")]
    public void Criar_CategoricoValido_Preenche()
    {
        FatoCandidato fato = Criar(descricao: "Cor ou raça autodeclarada", valoresDominio: ValoresCorRaca).Value!;

        fato.Id.Should().NotBe(Guid.Empty);
        fato.Codigo.Should().Be("COR_RACA");
        fato.Nome.Should().Be("Cor ou raça");
        fato.Descricao.Should().Be("Cor ou raça autodeclarada");
        fato.Dominio.Should().Be(DominioFato.Categorico);
        fato.Natureza.Should().Be(NaturezaFato.BrutoInformado);
        fato.Cardinalidade.Should().Be(CardinalidadeFato.Escalar);
        fato.ValoresDominio.Should().Equal(ValoresCorRaca);
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

    [Fact(DisplayName = "Natureza fora do roster (cast inválido) é rejeitada como inválida")]
    public void Criar_NaturezaForaDoRoster_Falha()
    {
        Result<FatoCandidato> resultado = Criar(natureza: (NaturezaFato)999, valoresDominio: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.NaturezaInvalida);
    }

    [Fact(DisplayName = "Cardinalidade fora do roster (cast inválido) é rejeitada como inválida")]
    public void Criar_CardinalidadeForaDoRoster_Falha()
    {
        Result<FatoCandidato> resultado = Criar(cardinalidade: (CardinalidadeFato)999, valoresDominio: null);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.CardinalidadeInvalida);
    }

    [Fact(DisplayName = "Natureza Nenhuma é rejeitada")]
    public void Criar_NaturezaNenhuma_Falha()
    {
        Result<FatoCandidato> resultado = Criar(natureza: NaturezaFato.Nenhuma);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(FatoCandidatoErrorCodes.NaturezaObrigatoria);
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
        FatoCandidato fato = Criar(codigo: "PCD", nome: "PcD", dominio: dominio, valoresDominio: null).Value!;

        fato.ValoresDominio.Should().BeNull();
    }

    [Fact(DisplayName = "Categórico sem valores é aceito (escopo-processo) e preserva o nulo")]
    public void Criar_CategoricoSemValores_AceitaEscopoProcesso()
    {
        FatoCandidato fato = Criar(
            codigo: "MODALIDADE",
            nome: "Modalidade",
            dominio: DominioFato.Categorico,
            cardinalidade: CardinalidadeFato.Multivalorado,
            valoresDominio: null).Value!;

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

    // ─── Imutabilidade (seed-governado, EntityBase puro, sem mutação) ──────────

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

    [Fact(DisplayName = "FatoCandidato não declara método de instância de mutação em runtime")]
    public void FatoCandidato_SemMetodoDeMutacao()
    {
        MethodInfo[] metodos = typeof(FatoCandidato)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(m => !m.IsSpecialName) // exclui getters/setters de propriedade
            .ToArray();

        metodos.Should().BeEmpty(
            "a única forma de nascer é a factory estática Criar; não há mutação de estado após a criação");
    }
}
