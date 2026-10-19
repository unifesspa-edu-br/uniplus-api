namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Cada oferta de curso tem a distribuição de vagas dela, com as modalidades e as regras dela: a
/// avaliação da inscrição lê só as da oferta que o candidato escolheu, e a união de todas só quando
/// ele ainda não escolheu.
/// </summary>
public sealed class ContextoDaVersaoFixadaResolverTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid OfertaA = Guid.CreateVersion7();
    private static readonly Guid OfertaB = Guid.CreateVersion7();

    [Fact(DisplayName = "Quem escolheu a oferta A não concorre à modalidade que só a oferta B tem")]
    public async Task OfertaEscolhida_NaoRecebeAModalidadeExclusivaDaOutra()
    {
        ResultadoDaAvaliacaoDaInscricao daA = await AvaliarAsync(OfertaA, ofertaA: ["AC"], ofertaB: ["AC", "AC_FEM"]);

        CodigosDasModalidades(daA).Should().Equal("AC");
        CodigosDaDerivada(daA).Should().Equal("AC");
    }

    [Fact(DisplayName = "Quem escolheu a oferta B concorre à modalidade que só ela tem")]
    public async Task OfertaEscolhida_RecebeAsModalidadesDaPropriaOferta()
    {
        ResultadoDaAvaliacaoDaInscricao daB = await AvaliarAsync(OfertaB, ofertaA: ["AC"], ofertaB: ["AC", "AC_FEM"]);

        CodigosDasModalidades(daB).Should().Equal("AC", "AC_FEM");
        CodigosDaDerivada(daB).Should().Equal("AC", "AC_FEM");
    }

    [Fact(DisplayName = "Sem oferta escolhida e com mais de uma distribuição, valem as modalidades de todas")]
    public async Task SemOferta_ComVariasDistribuicoes_UneAsModalidades()
    {
        ResultadoDaAvaliacaoDaInscricao semOferta = await AvaliarAsync(null, ofertaA: ["AC"], ofertaB: ["AC", "AC_FEM"]);

        CodigosDasModalidades(semOferta).Should().Equal("AC", "AC_FEM");
    }

    [Fact(DisplayName = "Num processo de uma oferta só, escolher a oferta ou não escolher dá a mesma avaliação")]
    public async Task UmaOfertaSo_EscolherNaoMudaNada()
    {
        ResultadoDaAvaliacaoDaInscricao escolhida = await AvaliarAsync(OfertaA, ofertaA: ["AC", "AC_FEM"], ofertaB: null);
        ResultadoDaAvaliacaoDaInscricao semOferta = await AvaliarAsync(null, ofertaA: ["AC", "AC_FEM"], ofertaB: null);

        CodigosDasModalidades(escolhida).Should().Equal("AC", "AC_FEM");
        CodigosDasModalidades(semOferta).Should().Equal(CodigosDasModalidades(escolhida));
    }

    [Fact(DisplayName = "Com duas opções, as modalidades saem da distribuição de cada oferta e a elegibilidade é a mesma")]
    public async Task DuasOpcoes_ModalidadesPorOpcao()
    {
        // A oferta A só tem a ampla concorrência; a B tem também a cota. A candidata cumpre a condição da cota.
        ResultadoDaAvaliacaoDaInscricao aPrimeiro = await AvaliarComOpcoesAsync(
            numeroDeOpcoes: 2, primeira: OfertaA, segunda: OfertaB, ofertaA: ["AC"], ofertaB: ["AC", "AC_FEM"]);
        ResultadoDaAvaliacaoDaInscricao bPrimeiro = await AvaliarComOpcoesAsync(
            numeroDeOpcoes: 2, primeira: OfertaB, segunda: OfertaA, ofertaA: ["AC"], ofertaB: ["AC", "AC_FEM"]);

        CodigosDasModalidades(aPrimeiro).Should().Equal("AC");
        aPrimeiro.ModalidadesSegundaOpcao!.Itens.Select(static m => m.Codigo).Should().Equal("AC", "AC_FEM");
        CodigosDasModalidades(bPrimeiro).Should().Equal("AC", "AC_FEM");
        bPrimeiro.ModalidadesSegundaOpcao!.Itens.Select(static m => m.Codigo).Should().Equal("AC");
    }

    [Fact(DisplayName = "A 2ª opção só conta se a versão fixada declara duas opções")]
    public async Task VersaoComUmaOpcao_IgnoraASegunda()
    {
        Result<ContextoDaVersaoFixada> contexto = await ResolverAsync(
            numeroDeOpcoes: 1, primeira: OfertaA, segunda: OfertaB, ofertaA: ["AC"], ofertaB: ["AC", "AC_FEM"]);

        contexto.Value!.Admitidas.NumeroDeOpcoesDeCurso.Should().Be(1);
        contexto.Value.ConfiguracaoDaSegundaOpcao.Should().BeNull();
        contexto.Value.Configuracao.ModalidadesOfertadas.Select(static m => m.Codigo).Should().Equal("AC");
    }

    [Fact(DisplayName = "A 2ª opção igual à 1ª, desconhecida ou sem 1ª não é avaliada: a recusa dela é do agregado")]
    public async Task SegundaOpcaoInvalida_NaoEAvaliada()
    {
        Result<ContextoDaVersaoFixada> igual = await ResolverAsync(2, OfertaA, OfertaA, ["AC"], ["AC", "AC_FEM"]);
        Result<ContextoDaVersaoFixada> desconhecida = await ResolverAsync(2, OfertaA, Guid.CreateVersion7(), ["AC"], ["AC", "AC_FEM"]);
        Result<ContextoDaVersaoFixada> semPrimeira = await ResolverAsync(2, null, OfertaB, ["AC"], ["AC", "AC_FEM"]);

        igual.Value!.ConfiguracaoDaSegundaOpcao.Should().BeNull();
        desconhecida.Value!.ConfiguracaoDaSegundaOpcao.Should().BeNull();
        semPrimeira.Value!.ConfiguracaoDaSegundaOpcao.Should().BeNull();
        semPrimeira.Value.Configuracao.ModalidadesOfertadas.Select(static m => m.Codigo).Distinct().Order(StringComparer.Ordinal)
            .Should().Equal("AC", "AC_FEM");
    }

    [Fact(DisplayName = "Com duas opções, o formulário vale para a união das distribuições das duas ofertas")]
    public async Task DuasOpcoes_ConfiguracaoDaUniaoDasOfertas()
    {
        Result<ContextoDaVersaoFixada> contexto = await ResolverAsync(
            numeroDeOpcoes: 2, primeira: OfertaA, segunda: OfertaB, ofertaA: ["AC"], ofertaB: ["AC_FEM"]);

        contexto.Value!.Configuracao.ModalidadesOfertadas.Select(static m => m.Codigo).Order(StringComparer.Ordinal)
            .Should().Equal("AC", "AC_FEM");
        contexto.Value.ConfiguracaoDaPrimeiraOpcao.ModalidadesOfertadas.Select(static m => m.Codigo).Should().Equal("AC");
    }

    private static string[] CodigosDasModalidades(ResultadoDaAvaliacaoDaInscricao avaliacao) =>
        [.. avaliacao.Modalidades.Itens.Select(static m => m.Codigo)];

    private static string[] CodigosDaDerivada(ResultadoDaAvaliacaoDaInscricao avaliacao) =>
        [.. (RespostaDeCampo.Codigos(avaliacao.Formulario.Fatos[AvaliacaoDaInscricao.FatoDaModalidade].Valor!.Value) ?? []).Order(StringComparer.Ordinal)];

    private static async Task<Result<ContextoDaVersaoFixada>> ResolverAsync(
        int numeroDeOpcoes, Guid? primeira, Guid? segunda, string[] ofertaA, string[] ofertaB)
    {
        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        GrafoConfiguracao grafo = OfertasDeTeste.ComOpcoesDeCurso(
            OfertasDeTeste.ComDistribuicao(grafoBase, OfertasDeTeste.Distribuicao(OfertaA, ofertaA), OfertasDeTeste.Distribuicao(OfertaB, ofertaB)),
            numeroDeOpcoes);

        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(repositorio, codecs, versao, grafo);

        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            versao.Id, ProcessoId, repositorio, codecs, primeira, segunda, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);
        return contexto;
    }

    /// <summary>A candidata que cumpre a condição de <c>AC_FEM</c>, avaliada nas duas opções.</summary>
    private static async Task<ResultadoDaAvaliacaoDaInscricao> AvaliarComOpcoesAsync(
        int numeroDeOpcoes, Guid? primeira, Guid? segunda, string[] ofertaA, string[] ofertaB)
    {
        Result<ContextoDaVersaoFixada> contexto = await ResolverAsync(numeroDeOpcoes, primeira, segunda, ofertaA, ofertaB);

        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = AvaliacaoDaInscricaoPorOpcao.Avaliar(
            contexto.Value!,
            new EntradaDaAvaliacaoDaInscricao(
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    [OfertasDeTeste.FatoDaAcaoAfirmativa] = JsonSerializer.SerializeToElement(OfertasDeTeste.ValorDaAcaoAfirmativa),
                },
                new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
                new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                Envio: null,
                NaFaseDaInscricao: true));
        avaliacao.IsSuccess.Should().BeTrue(avaliacao.Error?.Message);
        return avaliacao.Value!;
    }

    /// <summary>
    /// Avalia, na versão fixada, a candidata que cumpre a condição de <c>AC_FEM</c> depois de escolher
    /// <paramref name="ofertaEscolhida"/>.
    /// </summary>
    private static async Task<ResultadoDaAvaliacaoDaInscricao> AvaliarAsync(
        Guid? ofertaEscolhida, string[] ofertaA, string[]? ofertaB)
    {
        List<ConfiguracaoDistribuicaoVagas> distribuicao = [OfertasDeTeste.Distribuicao(OfertaA, ofertaA)];
        if (ofertaB is not null)
        {
            distribuicao.Add(OfertasDeTeste.Distribuicao(OfertaB, ofertaB));
        }

        (VersaoConfiguracao versao, GrafoConfiguracao grafoBase) = DivulgacaoDeTeste.VersaoConforme(ProcessoId);
        GrafoConfiguracao grafo = OfertasDeTeste.ComDistribuicao(grafoBase, [.. distribuicao]);

        IProcessoSeletivoRepository repositorio = Substitute.For<IProcessoSeletivoRepository>();
        IRegistroCodecsEnvelope codecs = Substitute.For<IRegistroCodecsEnvelope>();
        DivulgacaoDeTeste.ConfigurarVersao(repositorio, codecs, versao, grafo);

        Result<ContextoDaVersaoFixada> contexto = await ContextoDaVersaoFixadaResolver.ResolverAsync(
            versao.Id, ProcessoId, repositorio, codecs, ofertaEscolhida, null, CancellationToken.None);
        contexto.IsSuccess.Should().BeTrue(contexto.Error?.Message);

        Result<ResultadoDaAvaliacaoDaInscricao> avaliacao = AvaliacaoDaInscricao.Avaliar(
            contexto.Value!.Configuracao,
            new EntradaDaAvaliacaoDaInscricao(
                new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    [OfertasDeTeste.FatoDaAcaoAfirmativa] = JsonSerializer.SerializeToElement(OfertasDeTeste.ValorDaAcaoAfirmativa),
                },
                new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
                new Dictionary<string, FatoResolvido>(StringComparer.Ordinal),
                new HashSet<string>(StringComparer.Ordinal),
                Envio: null,
                NaFaseDaInscricao: true));
        avaliacao.IsSuccess.Should().BeTrue(avaliacao.Error?.Message);
        return avaliacao.Value!;
    }
}
