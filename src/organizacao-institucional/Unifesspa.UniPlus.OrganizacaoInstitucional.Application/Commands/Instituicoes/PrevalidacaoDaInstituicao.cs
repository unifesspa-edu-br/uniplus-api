namespace Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Instituicoes;

using Unifesspa.UniPlus.Kernel.Domain.Enderecos;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Application.Commands.Enderecos;
using Unifesspa.UniPlus.OrganizacaoInstitucional.Domain.Entities;

/// <summary>
/// A validação do payload inteiro da Instituição antes de qualquer I/O (validação sempre vence I/O e
/// 404), que a criação e a atualização repetem: resolve o endereço, valida os campos e junta o erro
/// de formato do endereço ao fim da lista (ADR-0125).
/// </summary>
internal static class PrevalidacaoDaInstituicao
{
    /// <returns>
    /// A validação e o endereço resolvido, nulo quando o payload não traz endereço ou o traz inválido.
    /// </returns>
    internal static (Result Validacao, ReferenciaEnderecoGeo? Endereco) Executar(
        IDadosDaInstituicaoDoComando dados,
        DateTimeOffset agora)
    {
        (DomainError? enderecoErro, ReferenciaEnderecoGeo? endereco) =
            EnderecoGeoInputMapping.Resolver(dados.Endereco, existente: null, agora);

        Result campos = Instituicao.ValidarCampos(
            dados.Identificacao(), dados.Cidade(), enderecoErro is null ? endereco : null);

        List<FieldError> erros = [.. campos.Errors];
        if (enderecoErro is not null)
        {
            erros.Add(new FieldError("endereco", enderecoErro));
        }

        Result validacao = erros.Count == 0 ? Result.Success() : Result.ValidationFailure(erros);
        return (validacao, enderecoErro is null ? endereco : null);
    }
}
