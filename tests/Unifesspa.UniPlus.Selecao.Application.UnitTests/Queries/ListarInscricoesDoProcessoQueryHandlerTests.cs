namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
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
    private static readonly string CodigoRevelar = PermissaoSelecao.DadosIdentificadoresRevelar.Codigo;
    private static readonly string CodigoPublicos = PermissaoSelecao.DadosPublicosConsultar.Codigo;

    private readonly IProcessoSeletivoRepository _processos;
    private readonly IRegistroCodecsEnvelope _codecs;
    private readonly IInscricaoRepository _inscricoes = Substitute.For<IInscricaoRepository>();
    private readonly IDecisoesDaRequisicao _requisicao = Substitute.For<IDecisoesDaRequisicao>();

    public ListarInscricoesDoProcessoQueryHandlerTests() => (_processos, _codecs) = ProcessoComNome("PESSOAL");

    private static (IProcessoSeletivoRepository Processos, IRegistroCodecsEnvelope Codecs) ProcessoComNome(string classificacaoDoNome)
    {
        InscricaoAdministrativaDeTeste.Mundo mundo = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond, processoId: ProcessoId)
            .Fato("NOME", classificacaoDoNome, TipoRenderizacao.Texto, null)
            .Montar();

        return (mundo.Processos, mundo.Codecs);
    }

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

    [Fact(DisplayName = "Só com dados públicos o registro de cada linha leva dados-publicos, e não o acesso ao resource")]
    public async Task SoComDadosPublicos_RegistraDadosPublicos()
    {
        DecisoesDeAcessoDeTeste leandro = new("leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);
        Paginar([Resumo(1)]);

        await Listar(leandro);

        await leandro.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoDeAcesso>(d => d.Registro!.Permissao == CodigoPublicos),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem dados pessoais a página é buscada sem ler nome nem CPF")]
    public async Task SemDadosPessoais_NaoLeNomeNemCpf()
    {
        DecisoesDeAcessoDeTeste leandro = new("leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);
        Paginar([Resumo(1)]);

        await Listar(leandro);

        await _inscricoes.Received(1).ListarEnviadasDoProcessoAsync(
            ProcessoId, null, 100, PaginationDirection.Next, false, false, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Se o processo congelou o nome como sensível, dados pessoais não bastam para ver o nome")]
    public async Task NomeCongeladoComoSensivel_NaoSaiComDadosPessoais()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoComNome("SENSIVEL");
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await ListarInscricoesDoProcessoQueryHandler.Handle(
            new ListarInscricoesDoProcessoQuery(ProcessoId, null, 100, PaginationDirection.Next),
            diogo.Verificador, new ProjetorDeItens(diogo.Verificador, _requisicao), processos, _inscricoes, codecs, CancellationToken.None);

        resultado.Items.Single().Nome.Should().BeNull();
    }

    [Fact(DisplayName = "Se o processo congelou o nome como interno, a linha que exibe o CPF registra a decisão de dados pessoais")]
    public async Task NomeCongeladoComoInterno_RegistraDadosPessoais()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoComNome("INTERNO");
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);

        await ListarInscricoesDoProcessoQueryHandler.Handle(
            new ListarInscricoesDoProcessoQuery(ProcessoId, null, 100, PaginationDirection.Next),
            diogo.Verificador, new ProjetorDeItens(diogo.Verificador, _requisicao), processos, _inscricoes, codecs, CancellationToken.None);

        await diogo.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoDeAcesso>(d => d.Registro!.Permissao == CodigoPessoais),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome congelado como sensível com a base legal congelada sai para quem tem dados sensíveis, e o repositório é chamado para lê-lo")]
    public async Task NomeSensivelComBaseLegal_SaiComDadosSensiveis()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoComNome("SENSIVEL");
        DecisoesDeAcessoDeTeste persona = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona, processos, codecs);

        resultado.Items.Single().Nome.Should().Be(PersonasDeTeste.NomeFernandaDrumond);
        await _inscricoes.Received(1).ListarEnviadasDoProcessoAsync(
            ProcessoId, null, 100, PaginationDirection.Next, true, false, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome sensível sem versão resolvida não tem base legal: não sai, não é lido nem registra dados sensíveis, ainda que a pessoa os tenha")]
    public async Task NomeSensivelSemBaseLegal_NaoSaiNemELido()
    {
        IProcessoSeletivoRepository semVersoes = Substitute.For<IProcessoSeletivoRepository>();
        semVersoes.ObterUnidadeAdministradoraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(DecisoesDeAcessoDeTeste.UnidadeDona);
        semVersoes.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);
        DecisoesDeAcessoDeTeste persona = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona, semVersoes, _codecs);

        resultado.Items.Single().Nome.Should().BeNull();
        await _inscricoes.Received(1).ListarEnviadasDoProcessoAsync(
            ProcessoId, null, 100, PaginationDirection.Next, false, true, Arg.Any<CancellationToken>());
        await persona.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoDeAcesso>(d => d.Registro!.Permissao == CodigoPessoais),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome congelado como identificador não sai na listagem, não é lido e não registra revelação, ainda que a pessoa possa revelar")]
    public async Task NomeIdentificador_NaoSaiNemRegistraRevelacao()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoComNome("IDENTIFICADOR");
        DecisoesDeAcessoDeTeste persona = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresRevelar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona, processos, codecs);

        resultado.Items.Single().Nome.Should().BeNull();
        await _inscricoes.Received(1).ListarEnviadasDoProcessoAsync(
            ProcessoId, null, 100, PaginationDirection.Next, false, true, Arg.Any<CancellationToken>());
        await persona.Verificador.DidNotReceive().RegistrarParaCadaAsync(
            Arg.Is<DecisaoDeAcesso>(d => d.Registro!.Permissao == CodigoRevelar),
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo sem versão que se resolva trata o nome como sensível")]
    public async Task SemVersao_NomeVaiComoSensivel()
    {
        IProcessoSeletivoRepository semVersoes = Substitute.For<IProcessoSeletivoRepository>();
        semVersoes.ObterUnidadeAdministradoraAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns(DecisoesDeAcessoDeTeste.UnidadeDona);
        semVersoes.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await ListarInscricoesDoProcessoQueryHandler.Handle(
            new ListarInscricoesDoProcessoQuery(ProcessoId, null, 100, PaginationDirection.Next),
            diogo.Verificador, new ProjetorDeItens(diogo.Verificador, _requisicao), semVersoes, _inscricoes, _codecs, CancellationToken.None);

        resultado.Items.Single().Nome.Should().BeNull();
    }

    private Task<ListarInscricoesDoProcessoResult> Listar(DecisoesDeAcessoDeTeste persona) => Listar(persona, _processos, _codecs);

    private Task<ListarInscricoesDoProcessoResult> Listar(
        DecisoesDeAcessoDeTeste persona, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) =>
        ListarInscricoesDoProcessoQueryHandler.Handle(
            new ListarInscricoesDoProcessoQuery(ProcessoId, null, 100, PaginationDirection.Next),
            persona.Verificador, new ProjetorDeItens(persona.Verificador, _requisicao), processos, _inscricoes, codecs, CancellationToken.None);

    private void Paginar(IReadOnlyList<ResumoDaInscricaoDoProcesso> pagina) =>
        _inscricoes.ListarEnviadasDoProcessoAsync(
                ProcessoId, null, 100, PaginationDirection.Next, Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((pagina, (Guid?)null, (Guid?)null));

    private static ResumoDaInscricaoDoProcesso Resumo(int numero) => new(
        Guid.CreateVersion7(), $"{numero:000000}", SituacaoInscricao.Submetida, Curso, null,
        PersonasDeTeste.NomeFernandaDrumond, PersonasDeTeste.FernandaDrumond, DateTimeOffset.UtcNow);
}
