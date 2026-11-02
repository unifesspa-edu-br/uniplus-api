namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Queries;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Authorization.Abstractions;
using Unifesspa.UniPlus.Authorization.Contracts;
using Unifesspa.UniPlus.Authorization.Projecao;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Administrativos;
using Unifesspa.UniPlus.Selecao.Application.Permissoes;
using Unifesspa.UniPlus.Selecao.Application.Queries.InscricoesDoProcesso;
using Unifesspa.UniPlus.Selecao.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
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
    private static readonly Guid VersaoDoProcesso = Guid.CreateVersion7();
    private static readonly string CodigoPessoais = PermissaoSelecao.DadosPessoaisConsultar.Codigo;
    private static readonly string CodigoAcesso = PermissaoSelecao.InscricoesConsultar.Codigo;
    private static readonly string CodigoRevelar = PermissaoSelecao.DadosIdentificadoresRevelar.Codigo;
    private static readonly string CodigoSensiveis = PermissaoSelecao.DadosSensiveisConsultar.Codigo;
    private static readonly string CodigoPublicos = PermissaoSelecao.DadosPublicosConsultar.Codigo;
    private static readonly string CodigoIdentificadores = PermissaoSelecao.DadosIdentificadoresConsultar.Codigo;

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

    [Fact(DisplayName = "Diogo com dados pessoais lista 20 inscrições com nome, sem CPF, e grava 20 registros com a decisão de dados pessoais")]
    public async Task ComDadosPessoais_ListaEGravaUmRegistroPorInscricao()
    {
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        ResumoDaInscricaoDoProcesso[] vinte = [.. Enumerable.Range(1, 20).Select(Resumo)];
        Paginar(vinte);

        ListarInscricoesDoProcessoResult resultado = await Listar(diogo);

        InscricaoDoProcessoDto primeira = resultado.Items[0];
        primeira.Nome!.Valor.Should().Be(PersonasDeTeste.NomeFernandaDrumond);
        primeira.Cpf.Should().BeNull("o CPF mascarado sai pela permissão do identificador, e não pela de dados pessoais");
        primeira.NumeroInscricao.Should().BeNull("o número é dado interno e Diogo não tem essa permissão");
        await diogo.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoPessoais),
            ResourceTiposSelecao.Inscricao,
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 20 && ids.SequenceEqual(vinte.Select(static i => i.Id))),
            true,
            Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com dados internos e sem dados públicos a linha traz id, número e data")]
    public async Task ComDadosInternos_TrazIdNumeroEData()
    {
        DecisoesDeAcessoDeTeste erick = new("erick.araujo", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar);
        Paginar([Resumo(1)]);

        InscricaoDoProcessoDto linha = (await Listar(erick)).Items.Single();

        linha.Id.Should().NotBeEmpty();
        linha.NumeroInscricao.Should().Be("000001");
        linha.SubmetidaEm.Should().NotBeNull();
        (linha.Situacao, linha.Ofertas).Should().Be((null, null));
    }

    [Fact(DisplayName = "Isabella com o identificador mascarado lista o CPF mascarado sem revelação, e o registro de cada linha leva o identificador")]
    public async Task ComIdentificador_ListaOCpfMascarado()
    {
        DecisoesDeAcessoDeTeste isabella = new("isabella.nunes", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(isabella);

        resultado.Items.Single().Cpf.Should().Be(new CpfMascaradoDto(PersonasDeTeste.FernandaDrumond.Mascarado, PodeRevelar: false));
        await _inscricoes.Received(1).ListarEnviadasDoProcessoAsync(
            ProcessoId, null, 100, PaginationDirection.Next, false, true, Arg.Any<CancellationToken>());
        await isabella.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoIdentificadores),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com o identificador e os dados sensíveis permitidos e o nome sensível, o registro de cada linha leva a classe sensível")]
    public async Task ComIdentificadorESensivel_RegistraASensivel()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoComNome("SENSIVEL");
        DecisoesDeAcessoDeTeste persona = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar, PermissaoSelecao.DadosSensiveisConsultar);
        Paginar([Resumo(1)]);

        await Listar(persona, processos, codecs);

        await persona.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoSensiveis),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Com o identificador permitido e o nome sensível sem permissão, o registro de cada linha leva o identificador")]
    public async Task ComIdentificadorSemSensivel_RegistraOIdentificador()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoComNome("SENSIVEL");
        DecisoesDeAcessoDeTeste persona = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);
        Paginar([Resumo(1)]);

        await Listar(persona, processos, codecs);

        await persona.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoIdentificadores),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Avaliação cega: com dados públicos e sem dados pessoais a linha traz curso e situação, sem nome nem CPF")]
    public async Task SemDadosPessoais_NaoTrazNomeNemCpf()
    {
        DecisoesDeAcessoDeTeste leandro = new("leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(leandro);

        InscricaoDoProcessoDto linha = resultado.Items.Single();
        linha.Situacao.Should().Be(SituacaoInscricao.Submetida);
        linha.Ofertas.Should().Equal(new OfertaEscolhidaDto(Curso, null, 1));
        (linha.Nome, linha.Cpf, linha.NumeroInscricao, linha.SubmetidaEm).Should().Be((null, null, null, null));
    }

    [Fact(DisplayName = "Só com a permissão de acesso a linha traz apenas o id, e o registro vai pela decisão do acesso ao resource")]
    public async Task SoComAcessoAoResource_LinhaVaziaERegistroDoAcesso()
    {
        DecisoesDeAcessoDeTeste kevin = new("kevin.carvalho", PermissaoSelecao.InscricoesConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(kevin);

        InscricaoDoProcessoDto linha = resultado.Items.Single();
        linha.Id.Should().NotBeEmpty();
        linha.Processo.Nome.Should().Be("PSR 2027");
        (linha.Situacao, linha.Ofertas, linha.Nome, linha.Cpf, linha.NumeroInscricao).Should().Be((null, null, null, null, null));
        await kevin.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoAcesso),
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
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoPublicos),
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
            diogo.Verificador, new ProjetorDeItens(diogo.Verificador, _requisicao), new PortaDePermissaoPorPar<PermissaoSelecao>(), processos, _inscricoes, codecs, CancellationToken.None);

        resultado.Items.Single().Nome.Should().BeNull();
    }

    [Theory(DisplayName = "Com o nome social coletado e pessoal como o civil, a avaliação cega não recebe nome algum e dados pessoais recebem")]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public async Task NomeSocialPessoal_SaiSoComDadosPessoais(bool comDadosPessoais, bool recebeNome)
    {
        InscricaoAdministrativaDeTeste.Mundo mundo = new InscricaoAdministrativaDeTeste(PersonasDeTeste.FernandaDrumond, processoId: ProcessoId)
            .Fato("NOME", "PESSOAL", TipoRenderizacao.Texto, null)
            .Fato("NOME_SOCIAL", "PESSOAL", TipoRenderizacao.Texto, null)
            .Montar();
        DecisoesDeAcessoDeTeste persona = comDadosPessoais
            ? new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar, PermissaoSelecao.DadosPessoaisConsultar)
            : new("leandro.viana", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona, mundo.Processos, mundo.Codecs);

        (resultado.Items.Single().Nome is not null).Should().Be(recebeNome);
    }

    [Fact(DisplayName = "Se o processo congelou o nome como interno, a linha que exibe o CPF registra a decisão do identificador")]
    public async Task NomeCongeladoComoInterno_RegistraDadosPessoais()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoComNome("INTERNO");
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);
        Paginar([Resumo(1)]);

        await ListarInscricoesDoProcessoQueryHandler.Handle(
            new ListarInscricoesDoProcessoQuery(ProcessoId, null, 100, PaginationDirection.Next),
            diogo.Verificador, new ProjetorDeItens(diogo.Verificador, _requisicao), new PortaDePermissaoPorPar<PermissaoSelecao>(), processos, _inscricoes, codecs, CancellationToken.None);

        await diogo.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoIdentificadores),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome congelado como sensível com a base legal congelada sai para quem tem dados sensíveis, e o repositório é chamado para lê-lo")]
    public async Task NomeSensivelComBaseLegal_SaiComDadosSensiveis()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = ProcessoComNome("SENSIVEL");
        DecisoesDeAcessoDeTeste persona = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona, processos, codecs);

        resultado.Items.Single().Nome!.Valor.Should().Be(PersonasDeTeste.NomeFernandaDrumond);
        await _inscricoes.Received(1).ListarEnviadasDoProcessoAsync(
            ProcessoId, null, 100, PaginationDirection.Next, true, false, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome sensível sem versão resolvida não tem base legal: não sai, não é lido nem registra dados sensíveis, ainda que a pessoa os tenha")]
    public async Task NomeSensivelSemBaseLegal_NaoSaiNemELido()
    {
        IProcessoSeletivoRepository semVersoes = Substitute.For<IProcessoSeletivoRepository>();
        semVersoes.ObterUnidadeENomeAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new UnidadeENomeDoProcesso(DecisoesDeAcessoDeTeste.UnidadeDona, "PSR 2027"));
        semVersoes.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);
        DecisoesDeAcessoDeTeste persona = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona, semVersoes, _codecs);

        resultado.Items.Single().Nome.Should().BeNull();
        await _inscricoes.Received(1).ListarEnviadasDoProcessoAsync(
            ProcessoId, null, 100, PaginationDirection.Next, false, true, Arg.Any<CancellationToken>());
        await persona.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoIdentificadores),
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
            ProcessoId, null, 100, PaginationDirection.Next, false, false, Arg.Any<CancellationToken>());
        await persona.Verificador.DidNotReceive().RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoRevelar),
            Arg.Any<string>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<bool>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Processo sem versão que se resolva trata o nome como sensível")]
    public async Task SemVersao_NomeVaiComoSensivel()
    {
        IProcessoSeletivoRepository semVersoes = Substitute.For<IProcessoSeletivoRepository>();
        semVersoes.ObterUnidadeENomeAsync(ProcessoId, Arg.Any<CancellationToken>())
            .Returns(new UnidadeENomeDoProcesso(DecisoesDeAcessoDeTeste.UnidadeDona, "PSR 2027"));
        semVersoes.ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>()).Returns([]);
        DecisoesDeAcessoDeTeste diogo = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await ListarInscricoesDoProcessoQueryHandler.Handle(
            new ListarInscricoesDoProcessoQuery(ProcessoId, null, 100, PaginationDirection.Next),
            diogo.Verificador, new ProjetorDeItens(diogo.Verificador, _requisicao), new PortaDePermissaoPorPar<PermissaoSelecao>(), semVersoes, _inscricoes, _codecs, CancellationToken.None);

        resultado.Items.Single().Nome.Should().BeNull();
    }

    [Fact(DisplayName = "Versões mistas (pessoal e sensível com base legal): pede o par sensível, o nome sai e o registro por inscrição é o da classe mais alta")]
    public async Task VersoesMistas_PedemOParSensivelERegistramAClasseMaisAlta()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = VersoesDoNomeDeTeste.Processo(
            ProcessoId,
            VersoesDoNomeDeTeste.VersaoComNome("PESSOAL", VersoesDoNomeDeTeste.Hipotese),
            VersoesDoNomeDeTeste.VersaoComNome("SENSIVEL", VersoesDoNomeDeTeste.Hipotese));
        DecisoesDeAcessoDeTeste persona = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona, processos, codecs);

        resultado.Items.Single().Nome!.Valor.Should().Be(PersonasDeTeste.NomeFernandaDrumond);
        await persona.Verificador.Received(1).AvaliarAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoSensiveis), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
        await persona.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoSensiveis),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Versão ilegível ao lado de uma legível: o par sensível não é pedido, o nome não sai e o registro por inscrição é o do identificador")]
    public async Task VersaoIlegivel_NaoPedeOParSensivelNemSaiONome()
    {
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = VersoesDoNomeDeTeste.Processo(
            ProcessoId, VersoesDoNomeDeTeste.VersaoComNome("PESSOAL", VersoesDoNomeDeTeste.Hipotese), VersoesDoNomeDeTeste.VersaoIlegivel());
        DecisoesDeAcessoDeTeste persona = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar, PermissaoSelecao.DadosIdentificadoresConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona, processos, codecs);

        resultado.Items.Single().Nome.Should().BeNull();
        await persona.Verificador.DidNotReceive().AvaliarAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoSensiveis), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
        await persona.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoIdentificadores),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Nome pessoal: pede só os pares do DTO, o nome sai com dados pessoais e o registro por inscrição é o de dados pessoais")]
    public async Task NomePessoal_PedeSoOsParesDoDto()
    {
        DecisoesDeAcessoDeTeste persona = new(
            "diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosSensiveisConsultar, PermissaoSelecao.DadosPessoaisConsultar);
        Paginar([Resumo(1)]);

        ListarInscricoesDoProcessoResult resultado = await Listar(persona);

        resultado.Items.Single().Nome!.Valor.Should().Be(PersonasDeTeste.NomeFernandaDrumond);
        await persona.Verificador.DidNotReceive().AvaliarAsync(
            Arg.Is<PermissionRequirement>(r => r.Permissao == CodigoSensiveis), Arg.Any<ResourceContext?>(), Arg.Any<CancellationToken>());
        await persona.Verificador.Received(1).RegistrarParaCadaAsync(
            Arg.Is<DecisaoPermitida>(d => d.Registro.Permissao == CodigoPessoais),
            ResourceTiposSelecao.Inscricao, Arg.Any<IReadOnlyCollection<Guid>>(), true, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Página com inscrições de duas versões: cada linha nomeia as ofertas pela sua versão, com uma leitura de versões e nenhuma por inscrição")]
    public async Task DuasVersoes_CadaLinhaUsaOsNomesDaSuaVersao()
    {
        (VersaoConfiguracao Versao, Result<EnvelopeReidratado> Envelope) primeira = VersoesDoNomeDeTeste.VersaoComNome("PESSOAL", VersoesDoNomeDeTeste.Hipotese);
        (VersaoConfiguracao Versao, Result<EnvelopeReidratado> Envelope) segunda = VersoesDoNomeDeTeste.VersaoComNome("PESSOAL", VersoesDoNomeDeTeste.Hipotese);
        (IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) = VersoesDoNomeDeTeste.Processo(ProcessoId, primeira, segunda);
        Guid ofertaDaPrimeira = VersoesDoNomeDeTeste.OfertaDa(primeira.Envelope);
        Guid ofertaDaSegunda = VersoesDoNomeDeTeste.OfertaDa(segunda.Envelope);
        DecisoesDeAcessoDeTeste persona = new("diogo.souza", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosPublicosConsultar);
        _inscricoes.ListarEnviadasDoProcessoAsync(
                ProcessoId, null, 100, PaginationDirection.Next, Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((
                (IReadOnlyList<ResumoDaInscricaoDoProcesso>)
                [
                    Resumo(1) with { VersaoFormularioId = primeira.Versao.Id, OfertaCursoId = ofertaDaPrimeira, OfertaCursoSegundaOpcaoId = ofertaDaSegunda },
                    Resumo(2) with { VersaoFormularioId = segunda.Versao.Id, OfertaCursoId = ofertaDaSegunda },
                ],
                (Guid?)null,
                (Guid?)null));

        IReadOnlyList<InscricaoDoProcessoDto> linhas = (await Listar(persona, processos, codecs)).Items;

        linhas[0].Ofertas!.Select(static o => (o.Nome is not null, o.Ordem)).Should().Equal((true, 1), (false, 2));
        linhas[1].Ofertas!.Select(static o => (o.Nome is not null, o.Ordem)).Should().Equal((true, 1));
        await processos.Received(1).ObterVersoesAsync(ProcessoId, Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Sem dados públicos as ofertas da linha são omitidas e o processo sai")]
    public async Task SemDadosPublicos_OmiteAsOfertas()
    {
        DecisoesDeAcessoDeTeste erick = new("erick.araujo", PermissaoSelecao.InscricoesConsultar, PermissaoSelecao.DadosInternosConsultar);
        Paginar([Resumo(1)]);

        InscricaoDoProcessoDto linha = (await Listar(erick)).Items.Single();

        linha.Ofertas.Should().BeNull();
        linha.Processo.Should().Be(new ProcessoDaInscricaoDto(ProcessoId, "PSR 2027"));
    }

    private Task<ListarInscricoesDoProcessoResult> Listar(DecisoesDeAcessoDeTeste persona) => Listar(persona, _processos, _codecs);

    private Task<ListarInscricoesDoProcessoResult> Listar(
        DecisoesDeAcessoDeTeste persona, IProcessoSeletivoRepository processos, IRegistroCodecsEnvelope codecs) =>
        ListarInscricoesDoProcessoQueryHandler.Handle(
            new ListarInscricoesDoProcessoQuery(ProcessoId, null, 100, PaginationDirection.Next),
            persona.Verificador, new ProjetorDeItens(persona.Verificador, _requisicao), new PortaDePermissaoPorPar<PermissaoSelecao>(), processos, _inscricoes, codecs, CancellationToken.None);

    private void Paginar(IReadOnlyList<ResumoDaInscricaoDoProcesso> pagina) =>
        _inscricoes.ListarEnviadasDoProcessoAsync(
                ProcessoId, null, 100, PaginationDirection.Next, Arg.Any<bool>(), Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns((pagina, (Guid?)null, (Guid?)null));

    private static ResumoDaInscricaoDoProcesso Resumo(int numero) => new(
        Guid.CreateVersion7(), $"{numero:000000}", SituacaoInscricao.Submetida, VersaoDoProcesso, Curso, null,
        PersonasDeTeste.NomeFernandaDrumond, PersonasDeTeste.FernandaDrumond, DateTimeOffset.UtcNow);
}
