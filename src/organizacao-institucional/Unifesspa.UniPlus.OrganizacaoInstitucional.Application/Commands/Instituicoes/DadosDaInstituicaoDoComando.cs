namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

// O agregado recebe os assuntos separados; o comando segue plano, como o corpo da requisição.
internal static class DadosDaInstituicaoDoComando
{
    internal static IdentificacaoRegulatoriaDaInstituicao Identificacao(this IDadosDaInstituicaoDoComando dados) => new(
        dados.CodigoEmec,
        dados.Nome,
        dados.Sigla,
        dados.OrganizacaoAcademica,
        dados.CategoriaAdministrativa,
        dados.Cnpj,
        dados.Mantenedora,
        dados.CodigoMantenedoraEmec,
        dados.Situacao,
        dados.AtoCredenciamento,
        dados.AtoRecredenciamento,
        dados.ConceitoInstitucional,
        dados.Igc,
        dados.Website);

    /// <summary>A cidade como o cliente a informa: sem proveniência nem carimbo, que o servidor acrescenta.</summary>
    internal static CidadeDaSede Cidade(this IDadosDaInstituicaoDoComando dados) =>
        new(dados.CidadeCodigoIbge, dados.CidadeNome, dados.CidadeUf, null, null);
}
