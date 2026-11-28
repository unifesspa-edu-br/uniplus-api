namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A unidade responsável da fase, resolvida pelo código que a fase cita. A fase sem código fica sem unidade, e a completude a
/// recusa; o código que o cadastro não tem deixa a fase sem unidade e vira parte recusada, que a completude não repete.
/// </summary>
internal static class LeituraDaUnidadeResponsavel
{
    internal static ValorLido<UnidadeResponsavelSnapshot?> Ler(FaseNoDocumento fase, CadastrosCitadosNoDocumento cadastros)
    {
        string? codigo = fase.Referencias.UnidadeResponsavelCodigo;
        if (codigo is null)
        {
            return new(null, []);
        }

        UnidadeCitada? unidade = cadastros.Unidade(codigo);

        return unidade is null ? new(null, [NaoEncontrada(fase)]) : new(unidade.ComoResponsavel(), []);
    }

    private static RecusaDaParte NaoEncontrada(FaseNoDocumento fase) => new(
        FaseDeclarada.CampoDaUnidadeResponsavel,
        "unidadeResponsavel",
        fase.PosicaoNoDocumento,
        null,
        new DomainError(
            FaseCronogramaErrorCodes.UnidadeResponsavelNaoEncontrada,
            "A unidade responsável citada pela fase não existe no cadastro de unidades ou foi removida."));
}
