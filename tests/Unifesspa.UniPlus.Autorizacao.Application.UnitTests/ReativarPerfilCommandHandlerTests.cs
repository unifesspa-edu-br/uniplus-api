namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands.Perfis;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Errors;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Kernel.Results;

public sealed class ReativarPerfilCommandHandlerTests
{
    private const string Consultar = "selecao:processos:consultar";

    [Fact(DisplayName = "Perfil desativado é reativado e a alteração é gravada")]
    public async Task Handle_PerfilDesativado_ReativaEGrava()
    {
        Perfil perfil = PerfilComum();
        perfil.Desativar();
        UnidadeDeTrabalho unidade = new();

        Result resultado = await Reativar(perfil, unidade);

        resultado.IsSuccess.Should().BeTrue();
        perfil.Ativo.Should().BeTrue();
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "Perfil inexistente devolve NaoEncontrado e nada é gravado")]
    public async Task Handle_PerfilInexistente_NaoEncontrado()
    {
        UnidadeDeTrabalho unidade = new();

        Result resultado = await Reativar(null, unidade);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.NaoEncontrado);
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Perfil de governança recusa a reativação, descarta o rastreamento e não grava")]
    public async Task Handle_PerfilDeGovernanca_RecusaEDescarta()
    {
        Perfil perfil = PerfilComum();
        typeof(Perfil).GetProperty(nameof(Perfil.Governanca))!
            .SetValue(perfil, Domain.Enums.TipoDeGovernanca.GestorDeAcessos);
        UnidadeDeTrabalho unidade = new();

        Result resultado = await Reativar(perfil, unidade);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.GovernancaImutavel);
        unidade.Descartes.Should().Be(1);
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "Conflito de concorrência na gravação descarta o rastreamento e devolve o conflito")]
    public async Task Handle_ConflitoDeConcorrencia_DescartaEDevolveConflito()
    {
        Perfil perfil = PerfilComum();
        perfil.Desativar();
        UnidadeDeTrabalho unidade = new() { Falha = new Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException() };

        Result resultado = await Reativar(perfil, unidade);

        resultado.Error!.Code.Should().Be(PerfilErrorCodes.ConflitoDeConcorrencia);
        unidade.Descartes.Should().Be(1);
    }

    private static Task<Result> Reativar(Perfil? perfil, UnidadeDeTrabalho unidade) =>
        ReativarPerfilCommandHandler.Handle(
            new ReativarPerfilCommand(perfil?.Id ?? Guid.NewGuid()),
            new PerfilNaMemoria(perfil),
            unidade,
            CancellationToken.None);

    private static Perfil PerfilComum() =>
        Perfil.Criar("Analista", "Descrição válida.", [Consultar], new CatalogoLivre()).Value!;

    private sealed class CatalogoLivre : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => false;
    }

    private sealed class UnidadeDeTrabalho : IAutorizacaoUnitOfWork
    {
        public int Gravacoes { get; private set; }

        public int Descartes { get; private set; }

        public Exception? Falha { get; init; }

        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
        {
            if (Falha is not null)
            {
                throw Falha;
            }

            Gravacoes++;
            return Task.FromResult(1);
        }

        public void DescartarAlteracoesNaoSalvas() => Descartes++;
    }

    private sealed class PerfilNaMemoria(Perfil? perfil) : IPerfilRepository
    {
        public Task<Perfil?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(perfil is not null && perfil.Id == id ? perfil : null);

        public Task<IReadOnlyList<Perfil>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Perfil entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Perfil entity) => throw new InvalidOperationException();
        public void Remover(Perfil entity) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarAtivosPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<Perfil?> ObterParaEditarAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> NomeExisteAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
