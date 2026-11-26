namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Coleta;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal sealed record UnicidadeEntreFormularios(
    ConfiguracaoDeclarada configuracao, FormularioProcesso formulario,
    IReadOnlyList<FatoColetado> fatos, IReadOnlyList<GrupoColetado> grupos)
{
    internal Result ConferirCodigos()
    {
        HashSet<string> deOutros = new(configuracao.Coleta.Campos.Where(DeOutroFormulario).Select(static f => f.FatoCodigo)
            .Concat(GruposDeOutros.Select(static g => g.Codigo)), StringComparer.Ordinal);
        HashSet<string> gruposDeOutros = new(GruposDeOutros.Select(static g => g.Codigo), StringComparer.Ordinal);
        string? repetido = grupos.Select(static g => g.Codigo).FirstOrDefault(deOutros.Contains)
            ?? fatos.Concat(grupos.SelectMany(static g => g.Subitens)).Select(static f => f.FatoCodigo).FirstOrDefault(gruposDeOutros.Contains);
        return repetido is null ? Result.Success() : Result.Failure(CodigoDeOutroFormulario(repetido));
    }

    private IEnumerable<GrupoColetado> GruposDeOutros => configuracao.Coleta.GruposColetados.Where(g => g.FormularioId != formulario.Id);

    private bool DeOutroFormulario(FatoColetado fato) => fato.FormularioId != formulario.Id;

    internal static DomainError CodigoDeOutroFormulario(string codigo) => new(
        FatoColetadoErrorCodes.FatoDuplicado, $"O código '{codigo}' já é usado por um fato ou grupo de outro formulário do processo.");

    internal Result ConferirProdutor()
    {
        HashSet<string> novos = new(fatos.Concat(grupos.SelectMany(static g => g.Subitens)).Select(static f => f.FatoCodigo), StringComparer.Ordinal);
        if (configuracao.Coleta.Campos.FirstOrDefault(f => DeOutroFormulario(f) && novos.Contains(f.FatoCodigo)) is not { } produzido)
            return Result.Success();
        string outro = configuracao.Coleta.CodigoDoDono(produzido) ?? string.Empty;
        return Result.Failure(new DomainError(FatoColetadoErrorCodes.FatoDuplicado,
            configuracao.EhDaInscricao(formulario.Declarado) && ConjuntoBasicoDaInscricao.Fatos.Contains(produzido.FatoCodigo)
                ? $"O fato '{produzido.FatoCodigo}' é dos dados básicos do candidato, que o formulário de inscrição coleta; tire-o do formulário de {outro} antes."
                : $"O fato '{produzido.FatoCodigo}' já é coletado pelo formulário de {outro}."));
    }
}
