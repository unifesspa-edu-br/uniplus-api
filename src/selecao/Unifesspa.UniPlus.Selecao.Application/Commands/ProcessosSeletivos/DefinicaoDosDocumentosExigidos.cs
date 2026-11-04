namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using Kernel.Results;

/// <summary>
/// Constrói a árvore de exigências a partir das entradas e a define no processo, sem gravar: quem chama
/// fecha a transação. As raízes mantidas seguem como estão, na frente das novas, e a aplicação de um modelo
/// de formulário usa isso para trazer o seu conjunto sem tirar o resto.
/// </summary>
internal static class DefinicaoDosDocumentosExigidos
{
    public static async Task<Result> DefinirAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<NoExigenciaInput> entradas,
        IReadOnlyList<NoExigencia> mantidas,
        PrecondicaoIfMatch precondicao,
        CadastrosDosDocumentosExigidos cadastros,
        CancellationToken cancellationToken)
    {
        // A forma da árvore inteira vence qualquer leitura externa (ADR-0125). O grupo da repetição vem antes
        // dos gatilhos: um gatilho que cita campo de grupo inexistente seria recusado como fato fora do
        // processo, e a recusa que orienta é a do grupo.
        List<FieldError> formaErros = EscritaDaArvoreDeExigencias.Conferir(entradas);
        if (formaErros.Count > 0)
        {
            return Result.ValidationFailure(formaErros);
        }

        DomainError? semGrupo = ValidadorRepeticaoPorGrupo.PrimeiroGrupoInexistente(
            ArvoreDeEntradaDasExigencias.GruposDaRepeticao(entradas), processo.GruposColetados);
        if (semGrupo is not null)
        {
            return Result.Failure(semGrupo);
        }

        ContextoDaMontagemDasExigencias contexto = await LerContextoAsync(processo, entradas, cadastros, cancellationToken).ConfigureAwait(false);
        Result<List<NoExigencia>> raizes = await new MontagemDaArvoreDeExigencias(new MontagemDoDocumentoExigido(contexto))
            .MontarRaizesAsync(entradas, ProximaOrdemDaRaiz(mantidas), cancellationToken).ConfigureAwait(false);
        return raizes.IsFailure
            ? Result.ValidationFailure(raizes.Errors)
            : DefinirNoProcesso(processo, mantidas, raizes.Value!, contexto.Fatos, precondicao);
    }

    private static int ProximaOrdemDaRaiz(IReadOnlyList<NoExigencia> mantidas) =>
        mantidas.Count == 0 ? 0 : mantidas.Max(static r => r.Ordem) + 1;

    private static Result DefinirNoProcesso(
        ProcessoSeletivo processo,
        IReadOnlyList<NoExigencia> mantidas,
        List<NoExigencia> raizes,
        FatosDoGatilho? fatos,
        PrecondicaoIfMatch precondicao)
    {
        Result vinculoNovo = fatos is null ? Result.Success() : ConferenciaDosVinculosDasExigencias.Conferir(processo, fatos, raizes);
        if (vinculoNovo.IsFailure)
        {
            return vinculoNovo;
        }

        Result definida = processo.DefinirDocumentosExigidos([.. mantidas, .. raizes], precondicao);
        return definida.IsFailure ? Result.Failure(definida.Error!) : Result.Success();
    }

    // Os modelos citados pelas folhas são lidos numa consulta só, e só os deste processo: o de outro processo
    // cai na mesma recusa do inexistente. O catálogo de fatos só é lido (I/O cross-módulo) quando alguma
    // folha declara gatilho ou fato comprovado, que também é vínculo novo a conferir.
    private static async Task<ContextoDaMontagemDasExigencias> LerContextoAsync(
        ProcessoSeletivo processo,
        IReadOnlyList<NoExigenciaInput> entradas,
        CadastrosDosDocumentosExigidos cadastros,
        CancellationToken cancellationToken)
    {
        HashSet<Guid> idsDosModelos = ArvoreDeEntradaDasExigencias.ModelosCitados(entradas);
        IReadOnlyDictionary<Guid, ModeloDeDocumento> modelos = idsDosModelos.Count == 0
            ? new Dictionary<Guid, ModeloDeDocumento>()
            : (await cadastros.Modelos.ListarDoProcessoAsync(processo.Id, idsDosModelos, cancellationToken).ConfigureAwait(false))
                .ToDictionary(static m => m.Id);
        bool existeGatilho = ArvoreDeEntradaDasExigencias.ExisteGatilho(entradas) || ArvoreDeEntradaDasExigencias.ExisteFatoComprovado(entradas);
        FatosDoGatilho? fatos = existeGatilho
            ? await FatosDoGatilho.LerAsync(processo, cadastros.Fatos, cancellationToken).ConfigureAwait(false)
            : null;
        return new ContextoDaMontagemDasExigencias(processo, cadastros.TiposDeDocumento, modelos, fatos);
    }
}
