namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Pagination;

/// <summary>Dependências em memória dos handlers que operam sobre o perfil e as versões dele.</summary>
internal static class DoublesDePerfis
{
    internal sealed class CatalogoDeTeste : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => !codigo.StartsWith("x:", StringComparison.Ordinal);

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("unidade:", StringComparison.Ordinal);

        public ClassificacaoDeProtecao Sensibilidade(string codigo) =>
            codigo.Contains("dados-pessoais", StringComparison.Ordinal) ? ClassificacaoDeProtecao.Pessoal : ClassificacaoDeProtecao.Interno;
    }

    internal sealed class Usuario(Identidade? identidade) : IUserContext
    {
        public bool IsAuthenticated => identidade is not null;
        public string? UserId => identidade?.Sub;
        public string? Issuer => identidade?.Emissor;
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

    internal sealed class UnidadeDeTrabalho : IAutorizacaoUnitOfWork
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

    internal sealed class Armazenamento : IArmazenamentoDosDocumentosDaAutorizacao
    {
        public List<string> ChavesDeEnvio { get; } = [];

        public List<string> ChavesDeLeitura { get; } = [];

        public Task<string> GerarUrlDeEnvioAsync(string chaveDeEnvio, string contentType, TimeSpan expiracao, CancellationToken cancellationToken = default)
        {
            ChavesDeEnvio.Add(chaveDeEnvio);
            return Task.FromResult($"https://armazenamento.test/envio/{chaveDeEnvio}?assinatura={ChavesDeEnvio.Count}");
        }

        public Task<string> GerarUrlDeLeituraAsync(string chaveDefinitiva, string nomeDoArquivo, TimeSpan expiracao, CancellationToken cancellationToken = default)
        {
            ChavesDeLeitura.Add(chaveDefinitiva);
            return Task.FromResult($"https://armazenamento.test/leitura/{chaveDefinitiva}");
        }
    }

    internal sealed class PerfisNaMemoria : IPerfilRepository
    {
        private readonly List<Perfil> _perfis = [];

        public PerfisNaMemoria(params Perfil[] perfis) => _perfis.AddRange(perfis);

        public List<LeituraDoDocumentoDaVersao> Leituras { get; } = [];

        public Perfil Unico() => _perfis.Single();

        public Task<Perfil?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(_perfis.Find(p => p.Id == id));

        public Task<Perfil?> ObterParaEditarAsync(Guid id, CancellationToken cancellationToken = default) =>
            ObterPorIdAsync(id, cancellationToken);

        public Task<bool> NomeExisteAsync(string nome, Guid? exceto, CancellationToken cancellationToken = default) =>
            Task.FromResult(_perfis.Any(p => p.Id != exceto && string.Equals(p.Nome, nome, StringComparison.OrdinalIgnoreCase)));

        public Task AdicionarAsync(Perfil entity, CancellationToken cancellationToken = default)
        {
            _perfis.Add(entity);
            return Task.CompletedTask;
        }

        public Task<Guid?> ObterPerfilIdDoDocumentoAsync(Guid documentoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(_perfis.Find(p => p.Versoes.Any(v => v.Documentos.Any(d => d.Id == documentoId)))?.Id);

        public Task AdicionarLeituraAsync(LeituraDoDocumentoDaVersao leitura, CancellationToken cancellationToken = default)
        {
            Leituras.Add(leitura);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<Perfil>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Perfil entity) => throw new InvalidOperationException();
        public void Remover(Perfil entity) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<Perfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPediveisPaginadoAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    internal sealed class ConcessoesNaMemoria(
        bool temConcessaoNaoRevogada = false,
        IReadOnlyList<Concessao>? paraMigrar = null,
        IReadOnlyList<TitularComEmail>? titulares = null) : IConcessaoRepository
    {
        public Task<bool> ExisteNaoRevogadaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) =>
            Task.FromResult(temConcessaoNaoRevogada);

        public Task<Concessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Concessao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Concessao entity) => throw new InvalidOperationException();
        public void Remover(Concessao entity) => throw new InvalidOperationException();
        public Task<Perfil?> ObterParaConcederAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<Concessao>> ObterParaMigrarAsync(Guid perfilId, Guid versaoId, CancellationToken cancellationToken = default) =>
            Task.FromResult(paraMigrar ?? []);
        public Task<ConcessaoComPerfil?> ObterComPerfilAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Concessao?> ObterNaoRevogadaIgualAsync(Identidade beneficiario, Guid perfilId, EscopoDaConcessao escopo, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<bool> TemConcessaoAtivaDoPerfilAsync(Identidade pessoa, Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<TitularComEmail>> ListarTitularesAtivosComEmailAsync(Guid perfilId, DateTimeOffset agora, CancellationToken cancellationToken = default) =>
            Task.FromResult(titulares ?? throw new InvalidOperationException());

        public Task<(IReadOnlyList<ConcessaoComPerfil> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarPaginadoAsync(
            FiltroDeConcessoes filtro, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();

        public Task<TipoDeGovernanca?> ObterGovernancaDoPerfilAsync(Guid perfilId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task TravarGovernancaParaEscritaAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<int> ContarGestoresAtivosAsync(Guid perfilId, Guid exceto, DateTimeOffset agora, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }
}
