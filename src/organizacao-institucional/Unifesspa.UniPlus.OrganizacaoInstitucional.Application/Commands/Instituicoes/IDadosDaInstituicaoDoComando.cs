namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;

/// <summary>
/// Os dados da Instituição que o comando de criação e o de atualização trazem iguais. Os comandos
/// são o corpo da requisição e mantêm o contrato plano; esta interface deixa um só conjunto de
/// extensões (<see cref="DadosDaInstituicaoDoComando"/>) servir aos dois.
/// </summary>
internal interface IDadosDaInstituicaoDoComando
{
    string? CodigoEmec { get; }

    string? Nome { get; }

    string? Sigla { get; }

    string? OrganizacaoAcademica { get; }

    string? CategoriaAdministrativa { get; }

    string? Cnpj { get; }

    string? Mantenedora { get; }

    string? CodigoMantenedoraEmec { get; }

    string? Situacao { get; }

    string? AtoCredenciamento { get; }

    string? AtoRecredenciamento { get; }

    string? ConceitoInstitucional { get; }

    string? Igc { get; }

    string? Website { get; }

    EnderecoGeoInput? Endereco { get; }

    string? CidadeCodigoIbge { get; }

    string? CidadeNome { get; }

    string? CidadeUf { get; }
}
