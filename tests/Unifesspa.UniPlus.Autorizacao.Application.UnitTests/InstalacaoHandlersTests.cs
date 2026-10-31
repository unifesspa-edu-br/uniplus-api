namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Instalacoes;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Verificador.Contratos;

public sealed class InstalacaoHandlersTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";

    [Fact(DisplayName = "O código de confirmação tem oito dígitos")]
    public void Gerar_TemOitoDigitos()
    {
        for (int i = 0; i < 50; i++)
        {
            CodigoDeConfirmacao.TemAForma(CodigoDeConfirmacao.Gerar()).Should().BeTrue();
        }
    }

    [Theory(DisplayName = "Só texto com a forma de código chega ao cálculo do resumo")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("1234567a")]
    [InlineData("１２３４５６７８")]
    public void TemAForma_FormaInvalida_Recusa(string? texto)
    {
        CodigoDeConfirmacao.TemAForma(texto).Should().BeFalse();
    }

    [Theory(DisplayName = "O veredicto indefinido, ou verificado sem o SHA-256, é defeito do contrato")]
    [InlineData(VeredictoDaVerificacao.NaoDefinido, null)]
    [InlineData(VeredictoDaVerificacao.Verificado, null)]
    [InlineData(VeredictoDaVerificacao.Verificado, "  ")]
    public async Task ResultadoMalformado_LancaAntesDeTocarNoRepositorio(VeredictoDaVerificacao veredicto, string? hash)
    {
        ResultadoDaVerificacaoDeArquivo resultado = new(Guid.NewGuid(), veredicto, MotivoDaRecusa.NaoSeAplica, hash, null, null);

        Func<Task> acao = () => RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler.Handle(
            resultado, new RepositorioSemInstalacao(), new PedidosComAnexo(null), new DoublesDeAvisos.PerfilQualquer(), DoublesDeAvisos.Avisos(), new UnidadeDeTrabalhoIntocavel(), TimeProvider.System, NullLogger.Instance, CancellationToken.None);

        await acao.Should().ThrowAsync<ResultadoDaVerificacaoInvalidoException>();
    }

    [Fact(DisplayName = "Resultado de instalação que não existe é descartado sem gravar")]
    public async Task ResultadoDeInstalacaoInexistente_Descarta()
    {
        ResultadoDaVerificacaoDeArquivo resultado = new(
            Guid.NewGuid(), VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null);

        await RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler.Handle(
            resultado, new RepositorioSemInstalacao(), new PedidosComAnexo(null), new DoublesDeAvisos.PerfilQualquer(), DoublesDeAvisos.Avisos(), new UnidadeDeTrabalhoIntocavel(), TimeProvider.System, NullLogger.Instance, CancellationToken.None);
    }

    [Fact(DisplayName = "Resultado repetido de instalação que já saiu da verificação é ignorado sem gravar")]
    public async Task ResultadoRepetido_Ignora()
    {
        Instalacao instalacao = Instalacao.Iniciar(
            new Identidade(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001"),
            [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
            Emissor, "123", 1024, DateTimeOffset.UtcNow).Value!;
        ResultadoDaVerificacaoDeArquivo resultado = new(
            instalacao.Id, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null);

        await RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler.Handle(
            resultado, new RepositorioComInstalacao(instalacao), new PedidosComAnexo(null), new DoublesDeAvisos.PerfilQualquer(), DoublesDeAvisos.Avisos(), new UnidadeDeTrabalhoIntocavel(), TimeProvider.System, NullLogger.Instance, CancellationToken.None);

        instalacao.Situacao.Should().Be(Domain.Enums.SituacaoDaInstalacao.AguardandoDocumento);
    }

    [Theory(DisplayName = "Código certo com gestor já instalado cancela a instalação, grava a tentativa como já havia gestor e recusa")]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Confirmar_OutraInstalacaoVenceuACorrida_RecusaSemGravarComoAceita(bool conflitoNaGravacao)
    {
        Identidade executor = new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001");
        Instalacao instalacao = Instalacao.Iniciar(
            executor,
            [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
            Emissor, "123", 1024, DateTimeOffset.UtcNow).Value!;
        instalacao.ConfirmarDocumento("etag");
        instalacao.RegistrarDocumentoVerificado("sha", DateTimeOffset.UtcNow);
        instalacao.EnviarCodigo([1, 2, 3], DateTimeOffset.UtcNow);
        RepositorioDaCorrida instalacoes = new(instalacao);
        UnidadeDeTrabalhoDaCorrida unidade = new(conflitoNaGravacao);

        Result resultado = await ConfirmarInstalacaoCommandHandler.Handle(
            new ConfirmarInstalacaoCommand("12345678"),
            instalacoes,
            new ConcessoesComGestorAtivo(),
            new ResumoFixo(),
            new CatalogoInstitucional(),
            unidade,
            new Usuario(executor),
            TimeProvider.System,
            NullLogger.Instance,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Error.Code.Should().Be(
            conflitoNaGravacao ? InstalacaoErrorCodes.JaHaGestorAoIniciar : InstalacaoErrorCodes.JaHaGestor);
        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.Cancelada);
        instalacoes.Tentativa!.Resultado.Should().Be(ResultadoDaTentativaDeInstalacao.JaHaviaGestor);
        unidade.Descartou.Should().Be(conflitoNaGravacao);
    }

    [Fact(DisplayName = "Concessão duplicada na gravação descarta a confirmação e recusa sem a marca de recusa gravada")]
    public async Task Confirmar_ConcessaoDuplicadaNaGravacao_RecusaComCodigoQueNaoPrendeAChave()
    {
        Identidade executor = new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001");
        Instalacao instalacao = Instalacao.Iniciar(
            executor,
            [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
            Emissor, "123", 1024, DateTimeOffset.UtcNow).Value!;
        instalacao.ConfirmarDocumento("etag");
        instalacao.RegistrarDocumentoVerificado("sha", DateTimeOffset.UtcNow);
        instalacao.EnviarCodigo([1, 2, 3], DateTimeOffset.UtcNow);
        UnidadeDeTrabalhoComConcessaoDuplicada unidade = new();

        Result resultado = await ConfirmarInstalacaoCommandHandler.Handle(
            new ConfirmarInstalacaoCommand("12345678"),
            new RepositorioDaCorrida(instalacao),
            new ConcessoesSemGestorComPerfil(),
            new ResumoFixo(),
            new CatalogoInstitucional(),
            unidade,
            new Usuario(executor),
            TimeProvider.System,
            NullLogger.Instance,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Error.Code.Should().Be(InstalacaoErrorCodes.JaHaGestorAoIniciar);
        unidade.Descartou.Should().BeTrue();
    }

    [Fact(DisplayName = "O pedido ao verificador usa o tamanho declarado do PDF como limite")]
    public async Task ConfirmarDocumento_PedidoUsaOTamanhoDeclaradoComoLimite()
    {
        Identidade executor = new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001");
        Instalacao instalacao = Instalacao.Iniciar(
            executor,
            [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
            Emissor, "123", 2048, DateTimeOffset.UtcNow).Value!;

        (Result<InstalacaoDto> resposta, IEnumerable<object> mensagens) = await ConfirmarDocumentoDaInstalacaoCommandHandler.Handle(
            new ConfirmarDocumentoDaInstalacaoCommand(instalacao.Id, "etag"),
            new RepositorioDoExecutor(instalacao),
            new UnidadeDeTrabalhoDaCorrida(false),
            new Usuario(executor),
            CancellationToken.None);

        resposta.IsSuccess.Should().BeTrue();
        mensagens.Should().ContainSingle().Which.Should().BeOfType<PedidoDeVerificacaoDeArquivo>()
            .Which.TamanhoMaximoBytes.Should().Be(2048);
    }

    [Fact(DisplayName = "Iniciar de novo cancela a pendência anterior e grava a nova numa única gravação")]
    public async Task Iniciar_ComPendenciaAnterior_GravaCancelamentoENovaJuntos()
    {
        Identidade executor = new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001");
        Instalacao anterior = Instalacao.Iniciar(
            executor,
            [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
            Emissor, "123", 1024, DateTimeOffset.UtcNow).Value!;
        RepositorioComPendencia instalacoes = new(anterior);
        UnidadeDeTrabalhoContadora unidade = new();

        Result<InstalacaoIniciadaDto> resultado = await IniciarInstalacaoCommandHandler.Handle(
            new IniciarInstalacaoCommand(
                [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
                "456",
                2048),
            instalacoes,
            new ConcessoesSemGestor(),
            new UrlFixa(),
            new DominiosDeTeste(),
            unidade,
            new Usuario(executor),
            TimeProvider.System,
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        anterior.Situacao.Should().Be(SituacaoDaInstalacao.Cancelada);
        instalacoes.Adicionada.Should().NotBeNull();
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Gestor com e-mail fora do domínio institucional recusa o início, sem gravar nada")]
    public async Task Iniciar_GestorComEmailNaoInstitucional_Recusa()
    {
        Identidade executor = new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001");
        UnidadeDeTrabalhoContadora unidade = new();

        Result<InstalacaoIniciadaDto> resultado = await IniciarInstalacaoCommandHandler.Handle(
            new IniciarInstalacaoCommand(
                [
                    new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@gmail.com"),
                    new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local"),
                ],
                "456",
                2048),
            new RepositorioComPendencia(null!),
            new ConcessoesSemGestor(),
            new UrlFixa(),
            new DominiosDeTeste(),
            unidade,
            new Usuario(executor),
            TimeProvider.System,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Error.Code.Should().Be(InstalacaoErrorCodes.GestoresInvalidos);
    }

    private sealed class RepositorioComPendencia(Instalacao anterior) : RepositorioSemInstalacao
    {
        public Instalacao? Adicionada { get; private set; }

        public override Task<Instalacao?> ObterPendenteDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instalacao?>(anterior);

        public override Task AdicionarAsync(Instalacao entity, CancellationToken cancellationToken = default)
        {
            Adicionada = entity;
            return Task.CompletedTask;
        }
    }

    private sealed class UnidadeDeTrabalhoContadora : IAutorizacaoUnitOfWork
    {
        public int Gravacoes { get; private set; }

        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
        {
            Gravacoes++;
            return Task.FromResult(1);
        }

        public void DescartarAlteracoesNaoSalvas()
        {
        }
    }

    private sealed class DominiosDeTeste : IDominiosInstitucionais
    {
        public bool Contem(string dominio) => dominio == "uniplus-test.local";
    }

    private sealed class UrlFixa : IArmazenamentoDosDocumentosDaAutorizacao
    {
        public Task<string> GerarUrlDeEnvioAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
            Task.FromResult("https://armazenamento.test/envio");

        public Task<string> GerarUrlDeLeituraAsync(string chaveDefinitiva, string nomeDoArquivo, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
            Task.FromResult("https://armazenamento.test/leitura");
    }

    private sealed class CatalogoInstitucional : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => false;
    }

    private sealed class ConcessoesSemGestor : IConcessaoRepository
    {
        public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Concessao entity) => throw new InvalidOperationException();
        public void Remover(Concessao entity) => throw new InvalidOperationException();
        public Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterNaoRevogadaIgualAsync(Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> TemConcessaoAtivaDoPerfilAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            FiltroDeConcessoes filtro, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> ExisteNaoRevogadaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class RepositorioDoExecutor(Instalacao instalacao) : RepositorioSemInstalacao
    {
        public override Task<Instalacao?> ObterDoExecutorAsync(Guid id, Identidade executor, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instalacao?>(instalacao);
    }

    private sealed class RepositorioDaCorrida(Instalacao instalacao) : RepositorioSemInstalacao
    {
        public TentativaDeInstalacao? Tentativa { get; private set; }

        public override Task<Instalacao?> ObterAguardandoCodigoDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instalacao?>(instalacao);

        public override Task AdicionarTentativaAsync(TentativaDeInstalacao tentativa, CancellationToken cancellationToken = default)
        {
            Tentativa = tentativa;
            return Task.CompletedTask;
        }
    }

    private sealed class UnidadeDeTrabalhoDaCorrida(bool conflita) : IAutorizacaoUnitOfWork
    {
        public bool Descartou { get; private set; }

        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default) =>
            conflita ? throw new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException() : Task.FromResult(1);

        public void DescartarAlteracoesNaoSalvas() => Descartou = true;
    }

    private sealed class ConcessoesSemGestorComPerfil : IConcessaoRepository
    {
        public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            Task.FromResult(0);

        public Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
            Task.FromResult<Perfil?>(Perfil.Criar("Gestor de acessos", "Governa as concessões de acesso.", ["selecao:processos:consultar"], new CatalogoInstitucional()).Value);

        public Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Concessao entity) => throw new InvalidOperationException();
        public void Remover(Concessao entity) => throw new InvalidOperationException();
        public Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterNaoRevogadaIgualAsync(Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> TemConcessaoAtivaDoPerfilAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            FiltroDeConcessoes filtro, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> ExisteNaoRevogadaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class UnidadeDeTrabalhoComConcessaoDuplicada : IAutorizacaoUnitOfWork
    {
        public bool Descartou { get; private set; }

        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default) =>
            throw new Microsoft.EntityFrameworkCore.DbUpdateException("duplicada", new ViolacaoDeIndiceUnico("ux_concessoes_ativa_unica"));

        public void DescartarAlteracoesNaoSalvas() => Descartou = true;
    }

    private sealed class ViolacaoDeIndiceUnico(string constraintName) : Exception
    {
        public string SqlState { get; } = "23505";

        public string ConstraintName { get; } = constraintName;
    }

    private sealed class ResumoFixo : IResumoDeCodigoDeConfirmacao
    {
        public Task<byte[]> ResumirAsync(Guid instalacaoId, string codigo, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]>([1, 2, 3]);
    }

    private sealed class ConcessoesComGestorAtivo : IConcessaoRepository
    {
        public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            Task.FromResult(1);

        public Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Concessao entity) => throw new InvalidOperationException();
        public void Remover(Concessao entity) => throw new InvalidOperationException();
        public Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterNaoRevogadaIgualAsync(Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> TemConcessaoAtivaDoPerfilAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            FiltroDeConcessoes filtro, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<bool> ExisteNaoRevogadaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class Usuario(Identidade identidade) : IUserContext
    {
        public bool IsAuthenticated => true;
        public string? UserId => identidade.Sub;
        public string? Issuer => identidade.Emissor;
        public string? Jti => null;
        public string? Name => null;
        public string? NomeCompleto => null;
        public string? Email => null;
        public bool EmailVerificado => false;
        public string? Cpf => null;
        public string? NomeSocial => null;
        public IReadOnlyList<string> Roles => [];
        public bool IsPlataformaAdmin => false;
        public bool HasRole(string role) => false;
        public IReadOnlyList<string> GetResourceRoles(string resourceName) => [];
    }

    private sealed class UnidadeDeTrabalhoIntocavel : IAutorizacaoUnitOfWork
    {
        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public void DescartarAlteracoesNaoSalvas() => throw new InvalidOperationException();
    }

    private class RepositorioSemInstalacao : IInstalacaoRepository
    {
        public virtual Task<Instalacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instalacao?>(null);

        public Task<IReadOnlyList<Instalacao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public virtual Task AdicionarAsync(Instalacao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public void Atualizar(Instalacao entity) => throw new InvalidOperationException();

        public void Remover(Instalacao entity) => throw new InvalidOperationException();

        public virtual Task<Instalacao?> ObterPendenteDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public virtual Task<Instalacao?> ObterAguardandoCodigoDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public virtual Task<Instalacao?> ObterDoExecutorAsync(Guid id, Identidade executor, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<Instalacao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public virtual Task AdicionarTentativaAsync(TentativaDeInstalacao tentativa, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<IReadOnlyList<TentativaDeInstalacao>> ListarTentativasAsync(Guid instalacaoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<TentativaDeInstalacao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarTentativasSemPendenciaAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class RepositorioComInstalacao(Instalacao instalacao) : RepositorioSemInstalacao
    {
        public override Task<Instalacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instalacao?>(instalacao);
    }
}
