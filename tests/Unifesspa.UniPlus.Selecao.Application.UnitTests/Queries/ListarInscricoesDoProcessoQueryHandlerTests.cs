namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// A listagem projeta cada campo pela permissão da sua classe de dado e grava um registro por inscrição exibida, numa só
/// mensagem, com a decisão de classe mais alta exercida (ADR-0153).
/// </summary>
public sealed class ListarInscricoesDoProcessoQueryHandlerTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly Guid Curso = Guid.CreateVersion7();
    private static readonly string CodigoPessoais = PermissaoSelecao.DadosPessoaisConsultar.Codigo;
    private static readonly string CodigoAcesso = PermissaoSelecao.InscricoesConsultar.Codigo;

    private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
    private readonly IInscricaoRepository _inscricoes = Substitute.For<IInscricaoRepository>();
    private readonly IDecisoesDaRequisicao _requisicao = Substitute.For<IDecisoesDaRequisicao>();

    public ListarInscricoesDoProcessoQueryHandlerTests() =>
        _processos.ObterUnidadeAdministradoraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(DecisoesDeAcessoDeTeste.UnidadeDona);

    [Fact(DisplayName = "Diogo com dados pessoais lista 20 inscrições com nome e CPF mascarado e grava 20 registros com a decisão de dados pessoais")]
    public async Task ComDadosPessoais_ListaEGravaUmRegistroPorInscricao()
    {
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        ResumoDaInscricaoDoProcesso[] vinte = [.. Enumerable.Range(1, 20).Select(Resumo)];
        Paginar(vinte);

        ListarInscricoesDoProcessoResult resultado = await Listar(diogo);

        InscricaoDoProcessoDto primeira = resultado.Items[0];
        primeira.Nome.Should().Be(PersonasDeTeste.NomeFernandaDrumond);
        primeira.Cpf.Should().Be(new CpfMascaradoDto(PersonasDeTeste.FernandaDrumond.Mascarado, PodeRevelar: false));
        primeira.NumeroInscricao.Should().BeNull("o número é dado interno e Diogo não tem essa permissão");
        await diogo.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoDeAcesso>(d => d.Registro!.Permissao == CodigoPessoais),
            ResourceTiposSelecao.Inscricao,
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 20 && ids.SequenceEqual(vinte.Select(static i => i.Id))),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Avaliação cega: com dados públicos e sem dados pessoais a linha traz curso e situação, sem nome nem CPF")]
    public async Task SemDadosPessoais_NaoTrazNomeNemCpf()
    {
        DecisoesDeAcessoDeTeste leandro = new("leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(leandro);

        InscricaoDoProcessoDto linha = resultado.Items.Single();
        linha.Id.Should().NotBeNull();
        linha.Situacao.Should().Be(SituacaoInscricao.Submetida);
        linha.OfertaCursoId.Should().Be(Curso);
        (linha.Nome, linha.Cpf, linha.NumeroInscricao, linha.SubmetidaEm).Should().Be((null, null, null, null));
    }

    [Fact(DisplayName = "Só com a permissão de acesso a linha não traz nada útil, e o registro vai pela decisão do acesso ao resource")]
    public async Task SoComAcessoAoResource_LinhaVaziaERegistroDoAcesso()
    {
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.InscricoesConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(kevin);

        InscricaoDoProcessoDto linha = resultado.Items.Single();
        (linha.Id, linha.Situacao, linha.OfertaCursoId, linha.Nome, linha.Cpf).Should().Be((null, null, null, null, null));
        await kevin.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoDeAcesso>(d => d.Registro!.Permissao == CodigoAcesso),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Falha ao registrar recusa a consulta: a página não sai")]
    public async Task FalhaAoRegistrar_RecusaAConsulta()
    {
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);
        diogo.Verificador
            .RegistrarParaCadaAsync(default!, default!, default!, default, default)
            .ReturnsForAnyArgs(Task.FromException(new RegistroDeAcessoNaoGravadoException(new InvalidOperationException("broker"))));

        Func<Task> listar = () => Listar(diogo);

        await listar.Should().ThrowAsync<RegistroDeAcessoNaoGravadoException>();
    }

    private Task<ListarInscricoesDoProcessoResult> Listar(DecisoesDeAcessoDeTeste persona) =>
        ListarInscricoesDoProcessoQueryHandler.Handle(
            new ListarInscricoesDoProcessoQuery(ProcessoId, null, 100, PaginationDirection.Next),
            persona.Verificador, new ProjetorDeItens(persona.Verificador, _requisicao), _processos, _inscricoes, CancellationToken.None);

    private void Paginar(IReadOnlyList<ResumoDaInscricaoDoProcesso> pagina) =>
        _inscricoes.ListarEnviadasDoProcessoAsync(ProcessoId, null, 100, PaginationDirection.Next, Arg.Any<CancellationToken>())
            .Returns((pagina, (Guid?)null, (Guid?)null));

    private static ResumoDaInscricaoDoProcesso Resumo(int numero) => new(
        Guid.CreateVersion7(), $"{numero:000000}", SituacaoInscricao.Submetida, Curso, null,
        PersonasDeTeste.NomeFernandaDrumond, PersonasDeTeste.FernandaDrumond, DateTimeOffset.UtcNow);
}
