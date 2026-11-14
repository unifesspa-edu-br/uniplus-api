namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Os dublês dos handlers da instalação: repositórios, unidade de trabalho, catálogo e quem está no token.</summary>
internal static class DoublesDaInstalacao
{
    public const string Emissor = "https://idp.test/realms/unifesspa";

    public static readonly Identidade Executor = new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001");

    /// <summary>Os dois gestores que toda instalação de teste nomeia, com e-mail no domínio institucional de teste.</summary>
    public static IdentidadeInformada[] Gestores() =>
    [
        new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"),
        new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local"),
    ];

    /// <summary>Uma instalação do executor de teste aguardando o documento.</summary>
    public static Instalacao Iniciada(long tamanho = 1024) =>
        Instalacao.Iniciar(Executor, Gestores(), Emissor, "123", tamanho, DateTimeOffset.UtcNow).Value!;

    /// <summary>Uma instalação do executor de teste aguardando o código, já enviado.</summary>
    public static Instalacao AguardandoCodigo()
    {
        Instalacao instalacao = Iniciada();
        instalacao.ConfirmarDocumento("etag");
        instalacao.RegistrarDocumentoVerificado("sha", DateTimeOffset.UtcNow);
        instalacao.EnviarCodigo([1, 2, 3], DateTimeOffset.UtcNow);

        return instalacao;
    }

    internal class RepositorioSemInstalacao : IInstalacaoRepository
    {
        public virtual Task<Instalacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<Instalacao?>(null);

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

    internal sealed class RepositorioComPendencia(Instalacao? anterior) : RepositorioSemInstalacao
    {
        public Instalacao? Adicionada { get; private set; }

        public override Task<Instalacao?> ObterPendenteDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default) =>
            Task.FromResult(anterior);

        public override Task AdicionarAsync(Instalacao entity, CancellationToken cancellationToken = default)
        {
            Adicionada = entity;
            return Task.CompletedTask;
        }
    }

    internal sealed class RepositorioDoExecutor(Instalacao instalacao) : RepositorioSemInstalacao
    {
        public override Task<Instalacao?> ObterDoExecutorAsync(Guid id, Identidade executor, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instalacao?>(instalacao);
    }

    internal sealed class RepositorioComInstalacao(Instalacao instalacao) : RepositorioSemInstalacao
    {
        public override Task<Instalacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<Instalacao?>(instalacao);
    }

    /// <summary>O repositório da confirmação: devolve a instalação aguardando código e guarda a tentativa gravada.</summary>
    internal sealed class RepositorioDaCorrida(Instalacao? instalacao) : RepositorioSemInstalacao
    {
        public TentativaDeInstalacao? Tentativa { get; private set; }

        public override Task<Instalacao?> ObterAguardandoCodigoDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default) =>
            Task.FromResult(instalacao);

        public override Task AdicionarTentativaAsync(TentativaDeInstalacao tentativa, CancellationToken cancellationToken = default)
        {
            Tentativa = tentativa;
            return Task.CompletedTask;
        }
    }

    /// <summary>A unidade de trabalho que conta as gravações e, opcionalmente, falha com a exceção dada.</summary>
    internal sealed class UnidadeDeTrabalho(Exception? falha = null) : IUnitOfWork
    {
        public int Gravacoes { get; private set; }

        public bool Descartou { get; private set; }

        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
        {
            Gravacoes++;
            return falha is null ? Task.FromResult(1) : throw falha;
        }

        public void DescartarAlteracoesNaoSalvas() => Descartou = true;

        public Task ForcarChecagemImediataDeConstraintsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public static UnidadeDeTrabalho ComConflito() => new(new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException());

        public static UnidadeDeTrabalho ComConcessaoDuplicada() =>
            new(new Microsoft.EntityFrameworkCore.DbUpdateException("duplicada", new ViolacaoDeIndiceUnico("ux_concessoes_ativa_unica")));
    }

    internal sealed class UnidadeDeTrabalhoIntocavel : IUnitOfWork
    {
        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public void DescartarAlteracoesNaoSalvas() => throw new InvalidOperationException();

        public Task ForcarChecagemImediataDeConstraintsAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    internal sealed class ViolacaoDeIndiceUnico(string constraintName) : Exception
    {
        public string SqlState { get; } = "23505";

        public string ConstraintName { get; } = constraintName;
    }

    internal sealed class DominiosDeTeste : IDominiosInstitucionais
    {
        public bool Contem(string dominio) => dominio == "uniplus-test.local";
    }

    internal sealed class UrlFixa : IArmazenamentoDosDocumentosDaAutorizacao
    {
        public Task<string> GerarUrlDeEnvioAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
            Task.FromResult("https://armazenamento.test/envio");

        public Task<string> GerarUrlDeLeituraAsync(string chaveDefinitiva, string nomeDoArquivo, TimeSpan expiracao, CancellationToken cancellationToken = default) =>
            Task.FromResult("https://armazenamento.test/leitura");
    }

    internal sealed class CatalogoInstitucional : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => false;

        public ClassificacaoDeProtecao Sensibilidade(string codigo) => ClassificacaoDeProtecao.Interno;
    }

    internal sealed class ResumoFixo : IResumoDeCodigoDeConfirmacao
    {
        public Task<byte[]> ResumirAsync(Guid instalacaoId, string codigo, CancellationToken cancellationToken = default) =>
            Task.FromResult<byte[]>([1, 2, 3]);
    }

    internal sealed class Usuario(Identidade identidade) : IUserContext
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
        public string? ClienteOidc => null;
        public IReadOnlyList<string> Roles => [];
        public bool IsPlataformaAdmin => false;
        public bool HasRole(string role) => false;
        public IReadOnlyList<string> GetResourceRoles(string resourceName) => [];
    }
}
