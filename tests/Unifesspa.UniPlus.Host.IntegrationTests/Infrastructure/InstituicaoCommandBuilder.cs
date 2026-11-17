namespace Unifesspa.UniPlus.Host.IntegrationTests.Infrastructure;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

internal sealed class InstituicaoCommandBuilder
{
    private readonly CriarInstituicaoCommand _command = new(
        CodigoEmec: "3990",
        Nome: "Instituição de teste",
        Sigla: "TESTE",
        OrganizacaoAcademica: "Universidade",
        CategoriaAdministrativa: "Pública Federal",
        Cnpj: null,
        Mantenedora: null,
        CodigoMantenedoraEmec: null,
        Situacao: null,
        AtoCredenciamento: null,
        AtoRecredenciamento: null,
        ConceitoInstitucional: null,
        Igc: null,
        Website: null,
        Endereco: null,
        CidadeCodigoIbge: null,
        CidadeNome: null,
        CidadeUf: null,
        UnidadeRaizId: null);

    public CriarInstituicaoCommand Build() => _command;
}
