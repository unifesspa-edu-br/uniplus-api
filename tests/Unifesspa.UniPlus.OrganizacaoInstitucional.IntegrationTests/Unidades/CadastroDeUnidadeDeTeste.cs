namespace Unifesspa.UniPlus.OrganizacaoInstitucional.IntegrationTests.Unidades;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Enums;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

/// <summary>
/// Os campos que o cadastro de uma Unidade informa e que a busca compara. Slug, sigla e código nascem únicos,
/// para que testes que compartilham a base não colidem entre si.
/// </summary>
internal sealed record CadastroDeUnidadeDeTeste(string Nome, string Slug, string Sigla, string Codigo, string? Alias)
{
    private static readonly DateOnly Inicio = new(2026, 1, 1);

    public IdentificadoresDaUnidade Identificadores => new(Slug, Sigla, Codigo, Alias);

    public static CadastroDeUnidadeDeTeste ComNome(string nome, string? alias = null)
    {
        string sufixo = Guid.NewGuid().ToString("N")[..12];
        return new CadastroDeUnidadeDeTeste(nome, $"u-{sufixo}", $"S{sufixo}", $"C{sufixo}", alias);
    }

    public Unidade Criar() =>
        Unidade.Criar(
            Nome, Identificadores, CidadeDaUnidade.Nenhuma, new VigenciaDaUnidade(Inicio, null), null,
            TipoUnidade.Centro, false).Value!;

    /// <summary>Aplica estes campos à Unidade já gravada, como o cadastro faz ao atualizar.</summary>
    public Result AplicarEm(Unidade unidade) =>
        unidade.Atualizar(
            Nome,
            new MudancaDeIdentificadores(Identificadores, Inicio.AddDays(1), "ajuste do cadastro"),
            CidadeDaUnidade.Nenhuma, null, null, TipoUnidade.Centro, false);
}
