namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

using static Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries.ApoioDoFormularioApresentado;

/// <summary>O <see cref="ObterFormularioApresentadoQueryHandler"/> resolve o formulário pela divulgação do certame: sem divulgação não há formulário, e a versão consultada é a que ela aponta.</summary>
public sealed class ObterFormularioApresentadoDivulgacaoTests
{
    /// <summary>Repositório sem linha: o processo não é público, seja qual for o motivo.</summary>
    private static ICertameDivulgadoRepository RepositorioSemDivulgacao()
    {
        ICertameDivulgadoRepository repositorio = Substitute.For<ICertameDivulgadoRepository>();
        repositorio.ObterParaLeituraAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((CertameDivulgado?)null);
        return repositorio;
    }

    [Fact(DisplayName = "Certame sem divulgação não serve formulário, e a recusa não diz por quê")]
    public async Task Handle_SemDivulgacao_RetornaNaoEncontrado()
    {
        // Inexistente, em rascunho, sem versão vigente e com ato não confirmado caem todos aqui,
        // pelo mesmo caminho: nenhum deles tem linha. Distinguir deixou de ser possível, em vez de
        // ser possível e proibido — e numa rota anônima essa diferença importa, porque a recusa
        // que distingue responde a um estranho se um identificador é um processo em rascunho.
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();

        Result<FormularioApresentadoDoProcessoDto> resultado = await HandleAsync(
            repository, processoId, RepositorioSemDivulgacao());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("ProcessoSeletivo.NaoEncontrado");
    }

    [Fact(DisplayName = "Sem divulgação, a versão de configuração nem chega a ser consultada")]
    public async Task Handle_QuandoNaoHaDivulgacao_NaoDeveConsultarAVersao()
    {
        // A publicidade é a primeira pergunta, não um filtro aplicado depois. Consultar a versão
        // antes reabriria o oráculo por outro caminho: o tempo de resposta, e qualquer recusa que
        // dependesse do que a consulta encontrasse.
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();

        await HandleAsync(repository, processoId, RepositorioSemDivulgacao());

        await repository.DidNotReceive().ObterVersoesPorAtoCriadorAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Divulgação apontando versão inexistente NÃO colapsa no não encontrado")]
    public async Task Handle_QuandoDivulgacaoApontaVersaoInexistente_DeveAflorarODefeito()
    {
        // A linha só nasce junto da versão, então isto é corrupção, não ausência. Responder não
        // encontrado esconderia o defeito atrás de uma resposta plausível — e aqui não há oráculo
        // a proteger: a existência da linha já disse que o processo é público.
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        Result<FormularioApresentadoDoProcessoDto> resultado = await HandleAsync(repository, processoId);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be("Snapshot.VigenteAusente");
    }

    [Fact(DisplayName = "O formulário é projetado da versão que a divulgação aponta, pelo ato criador")]
    public async Task Handle_QuandoHaDivulgacao_DeveResolverPelaVersaoQueElaAponta()
    {
        // O ponto da issue: sob retificação pendente, a versão vigente POR RELÓGIO é a nova, que
        // ainda não tem publicidade. Servir o formulário dela faria o candidato ler um edital e
        // preencher o de outro. A consulta tem de partir do ato que a divulgação carrega.
        Guid processoId = Guid.CreateVersion7();
        IProcessoSeletivoRepository repository = Substitute.For<IProcessoSeletivoRepository>();
        repository.ObterVersoesPorAtoCriadorAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([]);

        await HandleAsync(repository, processoId);

        await repository.Received(1).ObterVersoesPorAtoCriadorAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(IdDoAtoDaDivulgacao)),
            Arg.Any<CancellationToken>());
    }
}
