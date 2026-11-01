namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Services;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A proteção do nome de um processo é a classe mais alta entre as versões, e a base legal só vale quando todas a
/// congelaram. Versão que não se lê vale como sensível sem base legal.
/// </summary>
public sealed class ClassificacaoDoNomeNoProcessoTests
{
    private const string BasePessoal = VersoesDoNomeDeTeste.Hipotese;

    private static Guid ProcessoId { get; } = Guid.CreateVersion7();

    [Fact(DisplayName = "Versões mistas: vale a classe mais alta, com a base legal de todas as versões")]
    public async Task VersoesMistas_ValeAMaisAlta()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = VersoesDoNomeDeTeste.Processo(ProcessoId,
            VersoesDoNomeDeTeste.VersaoComNome("PESSOAL", BasePessoal), VersoesDoNomeDeTeste.VersaoComNome("SENSIVEL", BasePessoal));

        (ClassificacaoDeProtecao classificacao, string? baseLegal) = await ClassificacaoDoNomeNoProcesso
            .ResolverAsync(ProcessoId, processos, codecs, default);

        classificacao.Should().Be(ClassificacaoDeProtecao.Sensivel);
        baseLegal.Should().Be(BasePessoal);
    }

    [Fact(DisplayName = "Versão ilegível ao lado de uma legível vale como sensível sem base legal")]
    public async Task VersaoIlegivel_ValeComoSensivelSemBaseLegal()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = VersoesDoNomeDeTeste.Processo(ProcessoId,
            VersoesDoNomeDeTeste.VersaoComNome("PESSOAL", BasePessoal), VersoesDoNomeDeTeste.VersaoIlegivel());

        (ClassificacaoDeProtecao classificacao, string? baseLegal) = await ClassificacaoDoNomeNoProcesso
            .ResolverAsync(ProcessoId, processos, codecs, default);

        (classificacao, baseLegal).Should().Be((ClassificacaoDeProtecao.Sensivel, (string?)null));
    }

    [Fact(DisplayName = "Processo sem versão vale como sensível sem base legal")]
    public async Task SemVersao_ValeComoSensivelSemBaseLegal()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = VersoesDoNomeDeTeste.Processo(ProcessoId);

        (ClassificacaoDeProtecao classificacao, string? baseLegal) = await ClassificacaoDoNomeNoProcesso
            .ResolverAsync(ProcessoId, processos, codecs, default);

        (classificacao, baseLegal).Should().Be((ClassificacaoDeProtecao.Sensivel, (string?)null));
    }

    [Theory(DisplayName = "A classe do nome entra nos pares, salvo o identificador e o sensível sem base legal")]
    [InlineData(ClassificacaoDeProtecao.Pessoal, null, true)]
    [InlineData(ClassificacaoDeProtecao.Interno, null, true)]
    [InlineData(ClassificacaoDeProtecao.Sensivel, "CONSENTIMENTO", true)]
    [InlineData(ClassificacaoDeProtecao.Sensivel, null, false)]
    [InlineData(ClassificacaoDeProtecao.Sensivel, " ", false)]
    [InlineData(ClassificacaoDeProtecao.Identificador, "CONSENTIMENTO", false)]
    public void EntraNosPares(ClassificacaoDeProtecao classificacao, string? baseLegal, bool entra) =>
        ClassificacaoDoNomeNoProcesso.EntraNosPares(classificacao, baseLegal).Should().Be(entra);
}
