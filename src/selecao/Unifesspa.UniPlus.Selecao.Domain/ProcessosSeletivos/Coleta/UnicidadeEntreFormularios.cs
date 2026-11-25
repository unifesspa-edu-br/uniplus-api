namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal sealed record UnicidadeEntreFormularios(
    ConfiguracaoDeclarada configuracao, FinalidadeFormulario finalidade,
    IReadOnlyList<FatoColetado> fatos, IReadOnlyList<GrupoColetado> grupos)
{
    internal Result ConferirCodigos()
    {
        HashSet<string> deOutros = new(configuracao.Coleta.Campos.Where(f => f.Finalidade != finalidade).Select(static f => f.FatoCodigo)
            .Concat(configuracao.Coleta.GruposColetados.Where(g => g.Finalidade != finalidade).Select(static g => g.Codigo)), StringComparer.Ordinal);
        HashSet<string> gruposDeOutros = new(configuracao.Coleta.GruposColetados.Where(g => g.Finalidade != finalidade)
            .Select(static g => g.Codigo), StringComparer.Ordinal);
        string? repetido = grupos.Select(static g => g.Codigo).FirstOrDefault(deOutros.Contains)
            ?? fatos.Concat(grupos.SelectMany(static g => g.Subitens)).Select(static f => f.FatoCodigo).FirstOrDefault(gruposDeOutros.Contains);
        return repetido is null ? Result.Success() : Result.Failure(CodigoDeOutroFormulario(repetido));
    }

    internal static DomainError CodigoDeOutroFormulario(string codigo) => new(
        FatoColetadoErrorCodes.FatoDuplicado, $"O código '{codigo}' já é usado por um fato ou grupo de outro formulário do processo.");

    internal Result ConferirProdutor()
    {
        HashSet<string> novos = new(fatos.Concat(grupos.SelectMany(static g => g.Subitens)).Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        if (configuracao.Coleta.Campos.FirstOrDefault(f => f.Finalidade != finalidade && novos.Contains(f.FatoCodigo)) is not { } produzido)
            return Result.Success();
        string outro = produzido.Finalidade!.Value.Codigo();
        return Result.Failure(new DomainError(FatoColetadoErrorCodes.FatoDuplicado,
            finalidade == FinalidadeFormulario.Inscricao && ConjuntoBasicoDaInscricao.Fatos.Contains(produzido.FatoCodigo)
                ? $"O fato '{produzido.FatoCodigo}' é dos dados básicos do candidato, que o formulário de inscrição coleta; tire-o do formulário de {outro} antes."
                : $"O fato '{produzido.FatoCodigo}' já é coletado pelo formulário de {outro}."));
    }
}
