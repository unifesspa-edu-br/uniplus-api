namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Unidades;

using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.ValueObjects;

// Os comandos são o corpo da requisição e mantêm o contrato plano; o agregado recebe os
// assuntos separados.
internal static class DadosDaUnidadeDoComando
{
    internal static IdentificadoresDaUnidade Identificadores(this CriarUnidadeCommand command) =>
        new(command.Slug, command.Sigla, command.Codigo, command.Alias);

    internal static IdentificadoresDaUnidade Identificadores(this AtualizarUnidadeCommand command) =>
        new(command.Slug, command.Sigla, command.Codigo, command.Alias);

    internal static CidadeDaUnidade Cidade(this CriarUnidadeCommand command) =>
        new(command.CidadeCodigoIbge, command.CidadeNome, command.CidadeUf);

    internal static CidadeDaUnidade Cidade(this AtualizarUnidadeCommand command) =>
        new(command.CidadeCodigoIbge, command.CidadeNome, command.CidadeUf);

    internal static VigenciaDaUnidade Vigencia(this CriarUnidadeCommand command) =>
        new(command.VigenciaInicio, command.VigenciaFim);
}
