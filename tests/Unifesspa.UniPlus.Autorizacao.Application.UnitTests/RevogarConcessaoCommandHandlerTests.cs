namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Concessoes;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class RevogarConcessaoCommandHandlerTests
{
    [Theory(DisplayName = "Sem emissor ou sub do autor, a revogação é recusada antes de tocar a concessão")]
    [InlineData(null, "diogo.souza")]
    [InlineData("", "diogo.souza")]
    [InlineData("https://idp.test/realms/unifesspa", null)]
    [InlineData("https://idp.test/realms/unifesspa", "  ")]
    public async Task Handle_AutorNaoIdentificado_Recusa(string? emissor, string? sub)
    {
        Result resultado = await RevogarConcessaoCommandHandler.Handle(
            new RevogarConcessaoCommand(Guid.NewGuid(), "Mudou de função."),
            new RepositorioIntocavel(),
            new UnidadeDeTrabalhoIntocavel(),
            new Usuario(emissor, sub),
            TimeProvider.System,
            CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Single().Error.Code.Should().Be(ConcessaoErrorCodes.AutorNaoIdentificado);
    }

    private sealed class Usuario(string? issuer, string? userId) : IUserContext
    {
        public bool IsAuthenticated => true;
        public string? UserId => userId;
        public string? Issuer => issuer;
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

    private sealed class RepositorioIntocavel : IConcessaoRepository
    {
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

        public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
    }
}
