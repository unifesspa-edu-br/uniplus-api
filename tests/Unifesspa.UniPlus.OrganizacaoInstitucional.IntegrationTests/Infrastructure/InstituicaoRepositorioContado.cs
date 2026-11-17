namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Infrastructure;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Interfaces;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Infrastructure.Persistence.Repositories;

/// <summary>Delega tudo ao repositório real da Instituição e conta cada chamada recebida.</summary>
public sealed class InstituicaoRepositorioContado(InstituicaoRepository real, ConsultasAoCadastroContadas contagem)
    : IInstituicaoRepository
{
    public Task<Instituicao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.ObterPorIdAsync(id, cancellationToken);
    }

    public Task<Instituicao?> ObterParaLeituraAsync(CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.ObterParaLeituraAsync(cancellationToken);
    }

    public Task AdicionarAsync(Instituicao instituicao, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.AdicionarAsync(instituicao, cancellationToken);
    }

    public void Remover(Instituicao instituicao)
    {
        contagem.Registrar();
        real.Remover(instituicao);
    }

    public Task<bool> ExisteAlgumaVivaAsync(CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.ExisteAlgumaVivaAsync(cancellationToken);
    }

    public Task<bool> ExisteComUnidadeRaizAsync(Guid unidadeId, CancellationToken cancellationToken)
    {
        contagem.Registrar();
        return real.ExisteComUnidadeRaizAsync(unidadeId, cancellationToken);
    }
}
