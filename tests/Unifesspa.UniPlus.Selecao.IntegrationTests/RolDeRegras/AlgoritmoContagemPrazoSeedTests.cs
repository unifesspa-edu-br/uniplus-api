namespace Unifesspa.UniPlus.Selecao.IntegrationTests.RolDeRegras;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Infrastructure.Core.Persistence;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Readers;
using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Seed;

/// <summary>
/// Cobertura das entradas de <c>tipo=algoritmo_contagem_prazo</c> (#1135): as
/// convenções nomeadas de contagem do prazo de interposição que o UNI-REQ-0112
/// tornou decláraveis por edital. Prova o conteúdo do seed (as quatro
/// resoluções declaradas, com exemplos nas âncoras canônicas; esquema de
/// parâmetros vazio; base legal com placeholder honesto de pendência), os
/// hashes dourados que amarram o seed ao hash gravado, e o reader por tipo.
/// </summary>
public sealed class AlgoritmoContagemPrazoSeedTests : IClassFixture<RegraCatalogoDbFixture>
{
    private readonly RegraCatalogoDbFixture _fixture;

    public AlgoritmoContagemPrazoSeedTests(RegraCatalogoDbFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Hashes congelados das convenções. Amarram a definição do seed ao hash gravado:
    /// editar o texto de uma entrada sem regenerar a migration quebra este teste.
    /// </summary>
    private const string HashExcluiDiaInicial =
        "64e9c6c59b5d9a4e25a6c2fee46689fcc1c2c439aa9d33bd148dd8c74371a562";

    private const string HashHorasUteisDesdeAncora =
        "1319f925c7af96f337a34e8b705c6a40bd6d124dcc9e7bf9f5f1fd2d43d18cc6";

    private const string HashAvancaDataUtil =
        "47fb025bcfef2680377b57369e6f7e6657677c707f4e8293b979f883401eb094";

    private static RegraCatalogoSeedItem Item(string codigo) =>
        RegraCatalogoSeed.Itens.Single(i => i.Codigo == codigo);

    private static string[] Invariantes(string codigo)
    {
        using JsonDocument documento = JsonDocument.Parse(Item(codigo).InvariantesJson);
        return [.. documento.RootElement.EnumerateArray().Select(e => e.GetString()!)];
    }

    /// <summary>
    /// O rol declarado, e não uma cópia dele. É o mesmo que o decodificador do envelope aceita.
    /// </summary>
    /// <remarks>
    /// Duplicar a lista aqui deixava o seed amarrado a uma lista privada de teste: semear uma
    /// quarta convenção deixava este arquivo vermelho, e consertá-lo acrescentando o código
    /// <b>só aqui</b> publicava um certame que o decodificador recusaria depois — envelope
    /// irreidratável, com a configuração congelada inalcançável no descarte da retificação.
    /// Apontando para o rol, o mesmo vermelho só apaga quando o código é declarado onde os dois
    /// lados o leem.
    /// </remarks>
    private static readonly IReadOnlyList<string> CodigosDeContagem = AlgoritmoContagemPrazoCodigo.Todos;

    private static readonly Dictionary<string, string> HashesDourados = new(StringComparer.Ordinal)
    {
        [AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial] = HashExcluiDiaInicial,
        [AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora] = HashHorasUteisDesdeAncora,
        [AlgoritmoContagemPrazoCodigo.AvancaDataUtil] = HashAvancaDataUtil,
    };

    /// <summary>
    /// O hash dourado de <paramref name="codigo"/>, com a falta declarada como asserção — a
    /// indexação crua morreria com <c>KeyNotFoundException</c>, que não diz a quem semeou uma
    /// convenção nova que falta congelar o hash dela aqui.
    /// </summary>
    private static string HashDouradoDe(string codigo)
    {
        HashesDourados.Should().ContainKey(
            codigo,
            $"{codigo} está no rol e precisa do hash dourado");

        return HashesDourados[codigo];
    }

    [Fact(DisplayName = "O reader devolve as três convenções de contagem, cada uma com código, versão e hash")]
    public async Task Reader_ListarPorTipo_DevolveAsConvencoes()
    {
        await using UniPlusDbContext context = _fixture.CreateDbContext();
        RegraCatalogoReader reader = new(context);

        IReadOnlyList<RegraCatalogo> algoritmos = await reader.ListarPorTipoAsync(
            TipoRegra.AlgoritmoContagemPrazo, CancellationToken.None);

        algoritmos.Should().HaveCount(3);
        algoritmos.Select(a => a.Codigo).Should().BeEquivalentTo(CodigosDeContagem);
        algoritmos.Should().OnlyContain(a => a.Versao == RegraCatalogoSeed.VersaoV1);

        foreach (RegraCatalogo algoritmo in algoritmos)
        {
            algoritmo.Hash.Should().Be(HashDouradoDe(algoritmo.Codigo));
        }
    }

    [Fact(DisplayName = "Os hashes dourados amarram a definição do seed ao hash gravado")]
    public void Seed_HashesDourados_AmarramASemente()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            Item(codigo).ComputarHash().Should().Be(
                HashDouradoDe(codigo),
                $"editar a definição de {codigo} sem regenerar a migration dessincroniza seed e banco");
        }

        HashesDourados.Values.Should().OnlyHaveUniqueItems(
            "convenções diferentes têm definições — e hashes — diferentes");
    }

    [Fact]
    public void Seed_ExcluiDiaInicial_ExplicaQuandoTerminaOPrazo()
    {
        Invariantes(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial)
            .Should().Contain(i => i.Contains("à meia-noite que inicia o dia seguinte", StringComparison.Ordinal)
                && i.Contains("esse instante não está incluído no prazo", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "A coincidência declarada entre convenções nomeia uma entrada que existe no catálogo")]
    public void Seed_CoincidenciaDeclarada_NomeiaEntradaExistente()
    {
        string invarianteHoras = Invariantes(AlgoritmoContagemPrazoCodigo.AvancaDataUtil)
            .Single(i => i.StartsWith("em horas:", StringComparison.Ordinal));

        invarianteHoras.Should().Contain(
            "contagem de horas úteis a partir do evento que inicia o prazo",
            "a coincidência em horas é declarada em linguagem clara");

        RegraCatalogoSeed.Itens.Should().Contain(
            i => i.Codigo == AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora
                && i.Tipo == TipoRegra.AlgoritmoContagemPrazo,
            "a convenção citada tem de existir no catálogo, sob o mesmo tipo");
    }

    public static TheoryData<string> ConvencoesDoRol => new(CodigosDeContagem);

    [Theory(DisplayName = "Cada entrada declara quatro resoluções com exemplos")]
    [MemberData(nameof(ConvencoesDoRol))]
    public void Seed_DeclaraAsQuatroResolucoes_ComExemplosDeInicioDoPrazo(string codigo)
    {
        RegraCatalogoSeedItem item = Item(codigo);
        item.Tipo.Should().Be(TipoRegra.AlgoritmoContagemPrazo);
        item.Versao.Should().Be(RegraCatalogoSeed.VersaoV1);
        string[] invariantes = Invariantes(codigo);
        invariantes.Should().HaveCount(4);
        invariantes.Should().ContainSingle(i => i.StartsWith("evento que inicia o prazo fora da meia-noite:", StringComparison.Ordinal));
        invariantes.Should().ContainSingle(i => i.StartsWith("evento que inicia o prazo em dia não útil:", StringComparison.Ordinal));
        invariantes.Should().ContainSingle(i => i.StartsWith("em horas:", StringComparison.Ordinal));
        invariantes.Should().ContainSingle(i => i.StartsWith("em dias úteis:", StringComparison.Ordinal));
        string conteudo = string.Join('\n', invariantes);
        conteudo.Should().Contain("sexta às 18h").And.Contain("domingo às 18h");
    }

    [Theory(DisplayName = "As convenções descrevem o início do prazo em linguagem clara")]
    [InlineData(AlgoritmoContagemPrazoCodigo.ExcluiDiaInicial)]
    [InlineData(AlgoritmoContagemPrazoCodigo.HorasUteisDesdeAncora)]
    [InlineData(AlgoritmoContagemPrazoCodigo.AvancaDataUtil)]
    public void Seed_Invariantes_UsamLinguagemClara(string codigo)
    {
        string texto = string.Join(' ', Invariantes(codigo));
        texto.Should().NotContain("âncora").And.NotContain("janela");
        texto.Should().Contain("evento que inicia o prazo");
    }

    [Fact(DisplayName = "O esquema de parâmetros é vazio: o algoritmo é escolhido, não parametrizado")]
    public void Seed_DeclaracaoDaRegraVazio()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            using JsonDocument esquema = JsonDocument.Parse(Item(codigo).DeclaracaoDaRegraJson);

            esquema.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
            esquema.RootElement.EnumerateObject().Should().BeEmpty(
                $"a entrada {codigo} não recebe parâmetros do administrador — ela é escolhida, não parametrizada");
        }
    }

    [Fact(DisplayName = "A base legal das três entradas é a constante compartilhada do fundamento declarado pelo edital")]
    public void Seed_BaseLegal_FundamentoDeclaradoPeloEdital()
    {
        foreach (string codigo in CodigosDeContagem)
        {
            Item(codigo).BaseLegal.Should().Be(
                AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital,
                $"a base legal da entrada {codigo} é a constante compartilhada do fundamento aprovado (UNI-REQ-0095)");
        }
    }

    [Fact(DisplayName = "O fundamento declara os limites da decisão")]
    public void BaseLegal_NaoExcedeADecisao()
    {
        string fundamento = AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital;
        fundamento.Should().Contain("edital").And.Contain("UNI-REQ-0095").And.Contain("juridicamente orientada");
        fundamento.Should().Contain("não é parecer formal nem jurisprudência consolidada");
    }

    [Theory(DisplayName = "O fundamento não atribui autoridade além da decisão")]
    [InlineData("conforme jurisprudência")]
    [InlineData("segundo jurisprudência")]
    [InlineData("precedente")]
    [InlineData("STF")]
    [InlineData("STJ")]
    [InlineData("art.")]
    [InlineData("Art.")]
    [InlineData("§")]
    [InlineData("Lei nº")]
    [InlineData("Decreto")]
    public void BaseLegal_ReivindicacaoNaoSustentada_NaoConsta(string texto) =>
        AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital.Should().NotContain(texto);

    [Fact(DisplayName = "O fundamento nega dispor sobre efeito suspensivo, em vez de silenciar")]
    public void BaseLegal_NegaDisporSobreEfeitoSuspensivo()
    {
        // Silêncio deixaria o leitor supor que a decisão do prazo alcança a suspensividade.
        // A negativa é explícita porque a dependência daquela garantia continua aberta.
        string fundamento = AlgoritmoContagemPrazoCodigo.BaseLegalDeclaradaPeloEdital;

        fundamento.Should().Contain("dispõe sobre efeito suspensivo",
            "a negativa é explícita — silêncio deixaria supor que a decisão do prazo a alcança");
        fundamento.Should().Contain("UNI-REQ-0117", "a negativa aponta onde a dependência é rastreada");
    }
}
